using System;
using System.Collections.Generic;
using CrossServer.Shared.Interfaces;

namespace CrossServer.Shared;

public sealed class PacketDispatcher
{
	private readonly Dictionary<Type, Action<ClientContext, IPacket>> _packetHandlers = new();

	public PacketRegistry PacketRegistry { get; }
	
	public PacketDispatcher(IPacketProvider packetProvider)
	{
		PacketRegistry = new PacketRegistry(packetProvider);
	}

	public void RegisterHandler<T>(Action<ClientContext, T> handler) where T : IPacket
	{
		Type packetType = typeof(T);
		if (_packetHandlers.ContainsKey(packetType))
			throw new InvalidOperationException($"Packet type {packetType.Name} is already registered.");

		_packetHandlers[packetType] = (socket, packet) => handler(socket, (T)packet);
		PacketRegistry.RegisterPacket<T>();
	}
	
	public void UnregisterHandler<T>() where T : IPacket
	{
		Type packetType = typeof(T);
		if (!_packetHandlers.Remove(packetType))
			throw new InvalidOperationException($"Packet type {packetType.Name} is not registered.");

		PacketRegistry.UnregisterPacket<T>();
	}
	
	public void Dispatch(ClientContext context, IPacket packet)
	{
		Type packetType = packet.GetType();
		if (!_packetHandlers.TryGetValue(packetType, out Action<ClientContext, IPacket>? handler))
			throw new InvalidOperationException($"No handler registered for packet type {packetType.Name}.");
		
		handler(context, packet);
	}
}