using System;
using System.Collections.Generic;
using CrossServer.Shared.Interfaces;

namespace CrossServer.Shared;

public sealed class PacketDispatcher
{
	private readonly Dictionary<Type, Action<ClientContext, IPacket>> _packetHandlers = new();
    private readonly Dictionary<Type, Action<ClientContext, IPacket, ConnectionRequest>> _authorizationHandlers = new();

	public PacketRegistry? PacketRegistry { get; }

    private readonly bool _throwExceptionsForUnregisteredPackets;
	
	public PacketDispatcher(IPacketProvider packetProvider, bool throwExceptionsForUnregisteredPackets = true)
	{
		PacketRegistry = new PacketRegistry(packetProvider);
        _throwExceptionsForUnregisteredPackets = throwExceptionsForUnregisteredPackets;
	}
    
    public PacketDispatcher(bool throwExceptionsForUnregisteredPackets = true)
    {
        _throwExceptionsForUnregisteredPackets = throwExceptionsForUnregisteredPackets;
    }
    
    public PacketDispatcher()
    {
        
    }

    /// <summary>
    /// Registers handlers to be allowed post-auth
    /// </summary>
    /// <param name="handler"></param>
    /// <typeparam name="T"></typeparam>
    /// <exception cref="InvalidOperationException"></exception>
	public void RegisterHandler<T>(Action<ClientContext, T> handler) where T : IPacket
	{
		Type packetType = typeof(T);
        if (_packetHandlers.ContainsKey(packetType))
        {
            if (!_throwExceptionsForUnregisteredPackets)
            {
                return;
            }
            
			throw new InvalidOperationException($"Packet type {packetType.Name} is already registered.");
        }

		_packetHandlers[packetType] = (socket, packet) => handler(socket, (T)packet);
		PacketRegistry?.TryRegisterPacket<T>();
	}

    /// <summary>
    /// Registers handlers to be allowed pre-auth
    /// </summary>
    /// <param name="handler"></param>
    /// <typeparam name="T"></typeparam>
    /// <exception cref="InvalidOperationException"></exception>
    public void RegisterAuthHandler<T>(Action<ClientContext, T, ConnectionRequest> handler) where T : IPacket
    {
        Type packetType = typeof(T);
        if (_authorizationHandlers.ContainsKey(packetType))
        {
            if (!_throwExceptionsForUnregisteredPackets)
            {
                return;
            }
            
            throw new InvalidOperationException($"Packet type {packetType.Name} is already registered.");
        }

        _authorizationHandlers[packetType] = (socket, packet, connectionRequest) =>
        {
            handler(socket, (T)packet, connectionRequest);
        };
        
        PacketRegistry?.TryRegisterPacket<T>();
    }
	
	public void UnregisterHandler<T>() where T : IPacket
	{
		Type packetType = typeof(T);
        if (!_packetHandlers.Remove(packetType))
        {
            if (!_throwExceptionsForUnregisteredPackets)
            {
                return;
            }
            
			throw new InvalidOperationException($"Packet type {packetType.Name} is not registered.");
        }

		PacketRegistry?.UnregisterPacket<T>();
	}
    
    public void UnregisterAuthorizationHandler<T>() where T : IPacket
    {
        Type packetType = typeof(T);
        if (!_authorizationHandlers.Remove(packetType))
        {
            if (!_throwExceptionsForUnregisteredPackets)
            {
                return;
            }
            
            throw new InvalidOperationException($"Packet type {packetType.Name} is not registered.");
        }

        PacketRegistry?.UnregisterPacket<T>();
    }
	
    public bool Dispatch(ClientContext context, IPacket packet)
	{
		Type packetType = packet.GetType();
        if (!_packetHandlers.TryGetValue(packetType, out Action<ClientContext, IPacket>? handler))
        {
            if (!_throwExceptionsForUnregisteredPackets)
            {
                return false;
            }
            
			throw new InvalidOperationException($"No handler registered for packet type {packetType.Name}.");
        }
		
		handler(context, packet);
        return true;
	}
    
    internal void DispatchAuthorization(ClientContext context, IPacket packet, ConnectionRequest connectionRequest)
    {
        Type packetType = packet.GetType();
        if (!_authorizationHandlers.TryGetValue(packetType,
                out Action<ClientContext, IPacket, ConnectionRequest> handler))
        {
            if (!_throwExceptionsForUnregisteredPackets)
            {
                return;
            }
            
            throw new InvalidOperationException($"No Authorization handler registered for packet type {packetType.Name}.");
        }
		
        handler(context, packet, connectionRequest);
    }
}