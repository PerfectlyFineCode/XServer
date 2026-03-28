using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CrossServer.Shared;
using CrossServer.Shared.Heartbeat;
using CrossServer.Shared.Interfaces;

namespace CrossServer.Net;

public class XServer
{
	private readonly IXLogger? _logger;
	
	private readonly int _port;
	private readonly string _ipAddress;
	private readonly ConcurrentDictionary<Guid, ClientContext> _clients = new();
	private readonly SemaphoreSlim _connectionSemaphore = new(100);

	private Socket _serverSocket = null!;
	
	private readonly PacketListener _packetListener;
	
	public XServer(int port, string ipAddress, PacketListener packetListener, IXLogger? logger = null)
	{
		_logger = logger ?? new ConsoleXLogger();
		_port = port;
		_ipAddress = ipAddress;
		_packetListener = packetListener;
		_logger?.LogTrace("XServer initialized.");
	}
	
	public async Task Start(CancellationToken cancellationToken)
	{ 
		_serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		_serverSocket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Parse(_ipAddress), _port));
		_serverSocket.Listen(100);
		_logger?.LogTrace("XServer started.");
		await AcceptClients(cancellationToken);
	}
	
	public async Task AcceptClients(CancellationToken cancellationToken)
	{
		StartHeartbeatMonitor(cancellationToken);
		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				Socket clientSocket = await _serverSocket.AcceptAsync();
				_logger?.LogTrace($"Client connected: {clientSocket.RemoteEndPoint}");

				await _connectionSemaphore.WaitAsync(cancellationToken);

				_ = Task.Run(async () =>
				{
					try
					{
						await HandleClient(clientSocket);
					}
					finally
					{
						_connectionSemaphore.Release();
					}
				}, cancellationToken);
			}
			catch (ObjectDisposedException)
			{
				_logger?.LogTrace("Server socket closed, stopping accept loop.");
				break;
			}
			catch (Exception ex)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					_logger?.LogTrace("Shutdown requested, stopping accept loop.");
					break;
				}

				_logger?.LogError($"Error accepting client: {ex.Message}");
			}
		}
	}
	
	private async Task HandleClient(Socket clientSocket)
	{
		Guid clientId = Guid.NewGuid();
		ClientContext context = new(clientId, clientSocket, _logger);
		_clients[clientId] = context;
		
		context.OnBroadcastRequest += BroadcastPacket;
		
		_ = Task.Factory.StartNew(() => StartSenderLoop(context), TaskCreationOptions.LongRunning);
		_packetListener.OnClientConnected(context);
		try
		{
			while (context.IsConnected)
			{
				#if DEBUG
				_logger?.LogTrace($"Data Available from client {context.Id} - Available: {clientSocket.Available} bytes");
				#endif
				(byte id, byte[]? payload, int length, bool success) = await PacketReaderUtility.ReadIncomingPacket(clientSocket);
            
				if (!success)
				{
					#if DEBUG
					_logger?.LogTrace($"Failed to read packet from client {context.Id}. Disconnecting.");
					#endif
					break;
				}
				
				try
				{
					if (!ProcessPacket(context, id, payload, length))
					{
						#if DEBUG
						_logger?.LogTrace($"Failed to process packet from client {context.Id}. Disconnecting.");
						#endif
						break;
					}
				}
				finally
				{
                    if (payload != null)
                    {
                        _logger?.LogTrace("Returning array to pool");
					    ArrayPool<byte>.Shared.Return(payload);
                    }
				}
			}
		}
		catch (Exception ex)
		{
			#if DEBUG
			_logger?.LogTrace($"Error handling client: {ex.Message}");
			#endif
		}
		finally
		{
			#if DEBUG
			_logger?.LogTrace($"Cleaning up client {context.Id}.");
			#endif
			context.SendQueue?.CompleteAdding(); 
			_clients.TryRemove(clientId, out _);
		}
	}
	
	private void StartSenderLoop(ClientContext context)
	{
		try
		{
			foreach (byte[] packetData in context.SendQueue?.GetConsumingEnumerable()!)
			{
				if (!context.IsConnected) break;

				try
				{
					context.Socket.Send(packetData);
				}
				catch (SocketException ex)
				{
					_logger?.LogTrace($"Send error for client {context.Id}: {ex.Message}");
					break;
				}
			}
		}
		catch (ObjectDisposedException)
		{
			
		}
		finally
		{
			_packetListener.OnClientDisconnected(context);
			DisconnectClient(context.Socket);
			context.Dispose();
			_logger?.LogTrace($"client {context.Id} resources cleaned up.");
		}
	}
	
	public void StartHeartbeatMonitor(CancellationToken cancellationToken)
	{
		Task.Factory.StartNew(async () => 
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				try
				{
					DateTime now = DateTime.UtcNow;
					foreach (ClientContext? context in _clients.Values)
					{
						if ((now - context.LastHeartbeat).TotalSeconds > 30)
						{
							_logger?.LogTrace($"Client {context.Id} timed out.");
							DisconnectClient(context.Socket);
							continue;
						}

						_logger?.LogTrace($"Sending heartbeat to client {context.Id}.");
						context.Reply(new PingPacket());
					}
					
					await Task.Delay(5000, cancellationToken);
				}
				catch (OperationCanceledException)
				{
					
				}
			}
		}, cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);
	}

	private bool ProcessPacket(ClientContext context, byte id, byte[]? payload, int payloadLength)
	{
		try
		{
            IPacket? packet =
                PacketParser.ParsePacket(_packetListener.PacketRegistry, id, payload, payloadLength);
            if (packet == null)
            {
                _logger?.LogWarning($"Unhandled packet by ID: {id}");
                return false;
            }
            
			_packetListener.OnPacketReceived(context, packet);
			
			_logger?.LogTrace("Packet received from client.");
			return true;
		}
		catch (Exception ex)
		{
			_logger?.LogTrace($"Error processing packet: {ex.Message}");
			return false;
		}
	}
	
	private void SendPacket(ClientContext context, byte[] packetData)
	{
		if (context.SendQueue?.TryAdd(packetData) is not true)
		{
			_logger?.LogTrace($"[Warning] Send queue full for {context.Id}. Packet dropped.");
		}
	}
	
	public void BroadcastPacket(byte[] packetData)
	{
		foreach (ClientContext? client in _clients.Values)
		{
			SendPacket(client, packetData);
		}
	}
	
	public void DisconnectClient(Socket clientSocket)
	{
		try
		{
			clientSocket.Shutdown(SocketShutdown.Both);
			clientSocket.Close();
			_logger?.LogTrace("Client disconnected.");
		}
		catch (ObjectDisposedException)
		{
		
		}
		catch (Exception ex)
		{
			_logger?.LogTrace($"Error disconnecting client: {ex.Message}");
		}
	}

	public void Stop()
	{
		_serverSocket.Close();
		foreach (ClientContext? client in _clients.Values)
		{
			DisconnectClient(client.Socket);
		}
    
		_logger?.LogTrace("XServer stopped.");
	}
	
	private class ConsoleXLogger : IXLogger
	{
		public void LogTrace(string msg) => Console.WriteLine($"[TRACE] {msg}");
		public void LogInfo(string msg) => Console.WriteLine($"[INFO] {msg}");
		public void LogWarning(string msg) => Console.WriteLine($"[WARN] {msg}");
		public void LogError(string msg) => Console.WriteLine($"[ERROR] {msg}");
	}
}