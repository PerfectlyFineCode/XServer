using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using CrossServer.Shared.Extensions;
using CrossServer.Shared.Interfaces;

namespace CrossServer.Shared;

public class ClientContext : IDisposable
{
	private readonly IXLogger? _logger;
	
	public Guid Id { get; }
	public Socket Socket { get; }
	
	public ClientContextDataBag DataBag { get; } = new();

	public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
	
	public long Latency { get; private set; }
	
	public bool IsConnected => Socket.Connected && (DateTime.UtcNow - LastHeartbeat).TotalSeconds < 40;
		
	public BlockingCollection<byte[]> SendQueue { get; }
	
	public Action<byte[]>? OnBroadcastRequest { get; set; }
	
	public object? this[string key]
	{
		get => DataBag[key];
		set => DataBag[key] = value;
	}

	public ClientContext(Guid id, Socket socket, IXLogger? logger = null)
	{
		Id = id;
		Socket = socket;
		_logger = logger ?? new ConsoleXLogger();

		SendQueue = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>(), 1024);
	}
	
	public void UpdateLatency(long sentTicks)
	{
		LastHeartbeat = DateTime.UtcNow;
		Latency = (DateTime.UtcNow.Ticks - sentTicks) / TimeSpan.TicksPerMillisecond;
	}
	
	public void Reply<T>(T packet) where T : IPacket
	{
		byte[] data = packet.Build();
        
		if (!SendQueue.TryAdd(data))
		{
			_logger?.LogInfo($"[Warning] Send queue full for {Id}. Packet dropped.");
		}
	}
	
	public void ReplyRaw(byte[] data)
	{
		if (!SendQueue.TryAdd(data))
		{
			_logger?.LogInfo($"[Warning] Send queue full for {Id}. Packet dropped.");
		}
	}
	
	public void Broadcast<T>(T packet) where T : IPacket
	{
		byte[] data = packet.Build();
		OnBroadcastRequest?.Invoke(data);
	}
	
	public T? GetData<T>(string key)
	{
		return (T?)DataBag[key];
	}
	
	public void Close()
	{
		try
		{
			Socket.Shutdown(SocketShutdown.Both);
		}
		catch (SocketException ex)
		{
			_logger?.LogError($"Error shutting down socket for {Id}: {ex.Message}");
		}
		finally
		{
			Socket.Close();
			SendQueue.CompleteAdding();
			_logger?.LogInfo($"Client {Id} disconnected.");
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		SendQueue.Dispose();
	}
	
	private class ConsoleXLogger : IXLogger
	{
		public void LogInfo(string msg) => Console.WriteLine($"[INFO] {msg}");
		public void LogWarning(string msg) => Console.WriteLine($"[WARN] {msg}");
		public void LogError(string msg) => Console.WriteLine($"[ERROR] {msg}");
	}
}