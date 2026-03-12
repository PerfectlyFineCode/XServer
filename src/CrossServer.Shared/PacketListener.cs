using System;
using System.Net.Sockets;
using CrossServer.Shared.Extensions;
using CrossServer.Shared.Heartbeat;
using CrossServer.Shared.Interfaces;

namespace CrossServer.Shared;

public class PacketListener
{
	protected readonly PacketDispatcher Dispatcher;
	
	public PacketRegistry PacketRegistry => Dispatcher.PacketRegistry;

	protected PacketListener(IPacketProvider provider)
	{
		Dispatcher = new PacketDispatcher(provider);
		
		Dispatcher.RegisterHandler<PingPacket>((context, ping) => 
		{
			Console.WriteLine($"Received PingPacket with timestamp: {ping.Timestamp}");
			context.Reply(new PongPacket { Timestamp = ping.Timestamp });
		});

		Dispatcher.RegisterHandler<PongPacket>((context, pong) => 
		{
			Console.WriteLine($"Received PongPacket with timestamp: {pong.Timestamp} from client {context.Id}");
			context.UpdateLatency(pong.Timestamp);
		});
	}
	
	public virtual void OnPacketReceived(ClientContext context, IPacket packet)
	{
		Dispatcher.Dispatch(context, packet);
	}
	
	public virtual void OnClientConnected(ClientContext context)
	{
	}
	
	public virtual void OnClientDisconnected(ClientContext context)
	{
		
	}
	
	public virtual void OnRawPacketReceived(ClientContext context, byte id, byte[] payload)
	{
		
	}
	
	public byte[] SerializePacket<T>(T packet) where T : IPacket
	{
		return packet.Build();
	}
	
	private class ConsoleXLogger : IXLogger
	{
		public void LogInfo(string msg) => Console.WriteLine($"[INFO] {msg}");
		public void LogWarning(string msg) => Console.WriteLine($"[WARN] {msg}");
		public void LogError(string msg) => Console.WriteLine($"[ERROR] {msg}");
	}
}