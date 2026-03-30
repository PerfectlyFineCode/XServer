using System;
using CrossServer.Shared.Extensions;
using CrossServer.Shared.Heartbeat;
using CrossServer.Shared.Interfaces;
using CrossServer.Shared.Structs;
using SetInStone.Packets;

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
        
        Dispatcher.RegisterAuthHandler<PingPacket>((context, ping, _) => 
        {
            context.Reply(new PongPacket { Timestamp = ping.Timestamp });
        });

        Dispatcher.RegisterAuthHandler<PongPacket>((context, pong, _) => 
        {
            context.UpdateLatency(pong.Timestamp);
        });
	}

    /// <summary>
    /// Registers the neccessary handlers for client to function without any extra set-up
    /// </summary>
    public void RegisterClientDispatchers()
    {
        Dispatcher.RegisterHandler<ConnResponsePacket>((context, response) =>
        {
            if (response.IsRejected)
            {
                OnClientRejected(context, response.ToConnectionResponse());
                return;
            }

            OnClientAccepted(context, response.ToConnectionResponse());
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

    public virtual void OnClientRequest(ClientContext context, IPacket request, ConnectionRequest connectionRequest)
    {
		Dispatcher.DispatchAuthorization(context, request, connectionRequest);
    }

    public virtual void OnClientAccepted(ClientContext context, ConnectionResponse connectionResponse)
    {
        
    }
    
    public virtual void OnClientRejected(ClientContext context, ConnectionResponse connectionResponse)
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