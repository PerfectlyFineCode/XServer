using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CrossServer.Shared;
using CrossServer.Shared.Extensions;
using CrossServer.Shared.Interfaces;

namespace CrossServer.Net;

public sealed class XClient
{
	public event Action Connected;
	public event Action Disconnected;
	public event Action Reconnecting;
	
	private readonly IXLogger? _logger;
	private readonly PacketListener _packetListener;
	private readonly ConcurrentQueue<IPacket> _incomingPackets = new();
	
	private Socket _socket = null!;
	private Guid? _id;
	private string _host = null!;
	private int _port;
	private ClientContext _context = null!;
	private CancellationTokenSource _cancellationTokenSource = null!;
	
	public XClient(PacketListener packetListener, IXLogger? logger = null)
	{
		_packetListener = packetListener;
		_logger = logger ?? new ConsoleXLogger();
	}
	
	/// <summary>
	/// Starts listening for incoming packets from the server. This method runs in a loop and processes packets as they arrive.<br/>
	/// Do not use in Unity, as it will block the main thread. Use StartListeningManual and call Update() in your main loop instead.<br/>
	/// </summary>
	public async void StartListening()
	{
		try
		{
			while (_socket.Connected && !_cancellationTokenSource.Token.IsCancellationRequested)
			{
				(byte id, byte[]? payload, int payloadLength, bool success) =
					await PacketReaderUtility.ReadIncomingPacket(_socket, _cancellationTokenSource.Token);

				if (!success || payload == null)
				{
					break;
				}

				try
				{
					IPacket packet = PacketParser.ParsePacket(_packetListener.PacketRegistry, id, payload, payloadLength);
					_packetListener.OnPacketReceived(_context, packet);
				}
				finally
				{
					ArrayPool<byte>.Shared.Return(payload);
				}
			}
		}
		catch (Exception ex)
		{
			_logger?.LogInfo($"Client Receive Error: {ex.Message}");
		}
	}

	/// <summary>
	/// Starts listening for incoming packets from the server. This method runs in a loop and enqueues packets as they arrive.<br/>
	/// This is designed for use in Unity or other environments where you want to process packets on the main thread.<br/>
	/// Call Update() in your main loop to process the queued packets.
	/// </summary>
	public async void StartListeningManual()
	{
		try
		{
			while (true)
			{
				if (!_socket.Connected)
				{
					OnDisconnected();
					await ReconnectAsync(_host, _port);
				}
				
				(byte id, byte[]? payload, int payloadLength, bool success) =
					await PacketReaderUtility.ReadIncomingPacket(_socket, _cancellationTokenSource.Token);

				if (!success || payload == null)
				{
					break;
				}
				
				try
				{
					IPacket packet = PacketParser.ParsePacket(_packetListener.PacketRegistry, id, payload, payloadLength);
					_incomingPackets.Enqueue(packet);
				}
				finally
				{
					ArrayPool<byte>.Shared.Return(payload);
				}
			}
		}
		catch (Exception ex)
		{
			_logger?.LogInfo($"Client Receive Error: {ex.Message}");
		}
	}
	
	private bool _isReconnecting;

	private async Task ReconnectAsync(string host, int port)
	{
		if (_isReconnecting) return;
		_isReconnecting = true;
		OnReconnecting();

		int retryCount = 0;
		while (!_socket.Connected)
		{
			try
			{
				retryCount++;
				_logger?.LogInfo($"Attempting to reconnect ({retryCount})...");
            
				if (Connect(host, port))
				{
					_logger?.LogInfo("Reconnected successfully.");
					_isReconnecting = false;
					OnConnected();
                
					StartListeningManual();
					return;
				}
			}
			catch (Exception ex)
			{
				_logger?.LogInfo($"Reconnect attempt failed: {ex.Message}");
			}

			await Task.Delay(5000);
		}
		_isReconnecting = false;
	}
	
	public void Update()
	{
		while (_incomingPackets.TryDequeue(out IPacket? packet))
		{
			_packetListener.OnPacketReceived(_context, packet);
		}
	}
	
	public bool Connect(string host, int port)
	{
		try
		{
			_host = host;
			_port = port;
			_id	??= Guid.NewGuid();
			_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			_socket.Connect(host, port);
		
			_cancellationTokenSource = new CancellationTokenSource();
			_context = new ClientContext(_id.Value, _socket);
		
			Task.Factory.StartNew(() => ProcessClientSenderLoop(_context), TaskCreationOptions.LongRunning);
			return true;
		}
		catch
		{
			
			_cancellationTokenSource.Cancel();
			return false;
		}
	}
	
	private void ProcessClientSenderLoop(ClientContext context)
	{
		try
		{
			foreach (byte[] data in context.SendQueue.GetConsumingEnumerable())
			{
				if (!context.Socket.Connected) break;
				context.Socket.Send(data);
			}
		}
		catch (Exception ex)
		{
			_logger?.LogInfo($"Client Send Error: {ex.Message}");
		}
	}

	
	public void Disconnect()
	{
		_cancellationTokenSource.Cancel();
		if (_socket.Connected)
		{
			_socket.Shutdown(SocketShutdown.Both);
			_socket.Close();
		}
	}
	
	public void Send<T>(T packet) where T : IPacket
	{
		_context.Reply(packet); 
	}

	private void OnConnected()
	{
		Connected?.Invoke();
	}

	private void OnDisconnected()
	{
		Disconnected?.Invoke();
	}

	private void OnReconnecting()
	{
		Reconnecting?.Invoke();
	}
	
	private class ConsoleXLogger : IXLogger
	{
		public void LogInfo(string msg) => Console.WriteLine($"[INFO] {msg}");
		public void LogWarning(string msg) => Console.WriteLine($"[WARN] {msg}");
		public void LogError(string msg) => Console.WriteLine($"[ERROR] {msg}");
	}
}