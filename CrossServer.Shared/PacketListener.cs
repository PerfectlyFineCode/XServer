using System;
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
			context.Reply(new PongPacket { Timestamp = ping.Timestamp });
		});

		Dispatcher.RegisterHandler<PongPacket>((context, pong) => 
		{
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
}