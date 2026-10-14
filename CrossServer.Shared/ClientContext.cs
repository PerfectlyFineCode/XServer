using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using CrossServer.Shared.Extensions;
using CrossServer.Shared.Interfaces;

namespace CrossServer.Shared;

public class ClientContext : IDisposable
{
	private readonly IXLogger? _logger;
    
    public ClientContext(Guid id, Socket socket, IXLogger? logger = null)
    {
        _logger = logger ?? new ConsoleXLogger();
        Id = id;
        Socket = socket;
    }
    
    public ClientContext(Guid id, IXLogger? logger = null)
    {
        _logger = logger ?? new ConsoleXLogger();
        Socket = null;
        Id = id;
    }

    public Guid Id { get; }
    public Socket? Socket { get; }
    
    public bool IsHuman => Socket != null;

    public bool? Accepted { get; private set; }

    public ClientContextDataBag DataBag { get; } = new();

	public DateTime LastHeartbeat { get; private set; } = DateTime.UtcNow;
	
	public long Latency { get; private set; }
	
	public bool IsConnected => (Socket?.Connected ?? true) && (DateTime.UtcNow - LastHeartbeat).TotalSeconds < 40;
		
	public BlockingCollection<byte[]>? SendQueue { get; private set; } = new(new ConcurrentQueue<byte[]>(), 1024);

    public Action<byte[]>? OnBroadcastRequest { get; set; }
	
	public object? this[string key]
	{
		get => DataBag[key];
		set => DataBag[key] = value;
	}

    public void UpdateLatency(long sentTicks)
	{
		LastHeartbeat = DateTime.UtcNow;
		Latency = (DateTime.UtcNow.Ticks - sentTicks) / TimeSpan.TicksPerMillisecond;
	}
	
	public virtual void Reply<T>(T packet) where T : IPacket
    {
        if (Socket == null) return;
        
        byte[] data = packet.Build();
        
		if (SendQueue?.TryAdd(data) is not true)
		{
			_logger?.LogTrace($"[Warning] Send queue full for {Id}. Packet dropped.");
		}
	}
    
    public virtual void Reply<T>() where T : IPacket, new()
    {
        if (Socket == null) return;
        
        
        byte[] data = new T().Build();
        
        if (SendQueue?.TryAdd(data) is not true)
        {
            _logger?.LogTrace($"[Warning] Send queue full for {Id}. Packet dropped.");
        }
    }
	
	public void ReplyRaw(byte[] data)
	{
		if (SendQueue?.TryAdd(data) is not true)
		{
			_logger?.LogTrace($"[Warning] Send queue full for {Id}. Packet dropped.");
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
    
    public bool TryGetData<T>(string key, [NotNullWhen(true)] out T? value)
    {
        return DataBag.TryGetValue(key, out value);
    }

    public void Accept()
    {
        Accepted = true;
    }

    public void Reject()
    {
        Accepted = false;
    }
	
	public void Close()
    {
        if (Socket == null) return;
        
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
			SendQueue?.CompleteAdding();
			_logger?.LogTrace($"Client {Id} disconnected.");
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		SendQueue?.Dispose();
        SendQueue = null;
    }
	
	private class ConsoleXLogger : IXLogger
	{
		public void LogTrace(string msg) => Console.WriteLine($"[TRACE] {msg}");
		public void LogInfo(string msg) => Console.WriteLine($"[INFO] {msg}");
		public void LogWarning(string msg) => Console.WriteLine($"[WARN] {msg}");
        public void LogError(string msg) => Console.WriteLine($"[ERROR] {msg}");
	}
}