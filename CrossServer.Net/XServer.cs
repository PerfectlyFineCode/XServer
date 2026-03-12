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
		_logger?.LogInfo("XServer initialized.");
	}
	
	public async Task Start(CancellationToken cancellationToken)
	{ 
		_serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
		_serverSocket.Bind(new System.Net.IPEndPoint(System.Net.IPAddress.Parse(_ipAddress), _port));
		_serverSocket.Listen(100);
		_logger?.LogInfo("XServer started.");
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
				_logger?.LogInfo($"Client connected: {clientSocket.RemoteEndPoint}");

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
				_logger?.LogInfo("Server socket closed, stopping accept loop.");
				break;
			}
			catch (Exception ex)
			{
				if (cancellationToken.IsCancellationRequested)
				{
					_logger?.LogInfo("Shutdown requested, stopping accept loop.");
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
				_logger?.LogInfo($"Data Available from client {context.Id} - Available: {clientSocket.Available} bytes");
				#endif
				(byte id, byte[]? payload, int length, bool success) = await PacketReaderUtility.ReadIncomingPacket(clientSocket);
            
				if (!success || payload == null)
				{
					#if DEBUG
					_logger?.LogInfo($"Failed to read packet from client {context.Id}. Disconnecting.");
					#endif
					break;
				}
				
				try
				{
					if (!ProcessPacket(context, id, payload, length))
					{
						#if DEBUG
						_logger?.LogInfo($"Failed to process packet from client {context.Id}. Disconnecting.");
						#endif
						break;
					}
				}
				finally 
				{
					ArrayPool<byte>.Shared.Return(payload);
				}
			}
		}
		catch (Exception ex)
		{
			#if DEBUG
			_logger?.LogInfo($"Error handling client: {ex.Message}");
			#endif
		}
		finally
		{
			#if DEBUG
			_logger?.LogInfo($"Cleaning up client {context.Id}.");
			#endif
			context.SendQueue.CompleteAdding(); 
			_clients.TryRemove(clientId, out _);
		}
	}
	
	private void StartSenderLoop(ClientContext context)
	{
		try
		{
			foreach (byte[] packetData in context.SendQueue.GetConsumingEnumerable())
			{
				if (!context.IsConnected) break;

				try
				{
					context.Socket.Send(packetData);
				}
				catch (SocketException ex)
				{
					_logger?.LogInfo($"Send error for client {context.Id}: {ex.Message}");
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
			_logger?.LogInfo($"client {context.Id} resources cleaned up.");
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
							_logger?.LogInfo($"Client {context.Id} timed out.");
							DisconnectClient(context.Socket);
							continue;
						}

						_logger?.LogInfo($"Sending heartbeat to client {context.Id}.");
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

	private bool ProcessPacket(ClientContext context, byte id, byte[] payload, int payloadLength)
	{
		try
		{
			IPacket packet = PacketParser.ParsePacket(_packetListener.PacketRegistry, id, payload, payloadLength);
			_packetListener.OnPacketReceived(context, packet);
			
			_logger?.LogInfo("Packet received from client.");
			return true;
		}
		catch (Exception ex)
		{
			_logger?.LogInfo($"Error processing packet: {ex.Message}");
			return false;
		}
	}
	
	private void SendPacket(ClientContext context, byte[] packetData)
	{
		if (!context.SendQueue.TryAdd(packetData))
		{
			_logger?.LogInfo($"[Warning] Send queue full for {context.Id}. Packet dropped.");
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
			_logger?.LogInfo("Client disconnected.");
		}
		catch (ObjectDisposedException)
		{
		
		}
		catch (Exception ex)
		{
			_logger?.LogInfo($"Error disconnecting client: {ex.Message}");
		}
	}

	public void Stop()
	{
		_serverSocket.Close();
		foreach (ClientContext? client in _clients.Values)
		{
			DisconnectClient(client.Socket);
		}
    
		_logger?.LogInfo("XServer stopped.");
	}
	
	private class ConsoleXLogger : IXLogger
	{
		public void LogInfo(string msg) => Console.WriteLine($"[INFO] {msg}");
		public void LogWarning(string msg) => Console.WriteLine($"[WARN] {msg}");
		public void LogError(string msg) => Console.WriteLine($"[ERROR] {msg}");
	}
}