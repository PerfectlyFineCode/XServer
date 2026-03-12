using System;
using System.Collections.Generic;
using System.Linq;
using CrossServer.Shared.Attributes;
using CrossServer.Shared.Interfaces;

namespace CrossServer.Shared;

public partial class PacketRegistry
{
	private IPacketProvider _provider;
	private readonly Dictionary<byte, Type> _packetTypes = new();
	private readonly Dictionary<Type, byte> _packetIds = new();

	public PacketRegistry(IPacketProvider provider)
	{
		_provider = provider;
	}
	
	
	public PacketRegistry RegisterPacket<T>() where T : IPacket
	{
		byte packetId = GetPacketId<T>();
		if (_packetTypes.ContainsKey(packetId))
			throw new InvalidOperationException($"Packet type {typeof(T).Name} is already registered.");

		_packetTypes[packetId] = typeof(T);
		_packetIds[typeof(T)] = packetId;
		
		return this;
	}
	
	public IPacket CreatePacket(byte packetId)
	{
		return _packetTypes.ContainsKey(packetId)
			? _provider.CreatePacket(packetId)
			: throw new InvalidOperationException($"Packet ID {packetId} is not registered.");
	}
	
	public PacketRegistry UnregisterPacket<T>() where T : IPacket
	{
		byte packetId = GetPacketId<T>();
		if (!_packetTypes.Remove(packetId))
			throw new InvalidOperationException($"Packet type {typeof(T).Name} is not registered.");

		_packetIds.Remove(typeof(T));
		
		return this;
	}
	
	internal static byte GetPacketId(Type packetType)
	{
		return packetType
			.GetCustomAttributes(typeof(PacketIdAttribute), false)
			.FirstOrDefault() is not PacketIdAttribute attribute 
			? throw new InvalidOperationException($"Packet type {packetType.Name} is missing PacketIdAttribute.") 
			: attribute.Id;
	}
	
	internal static byte GetPacketId<T>() where T : IPacket
	{
		return typeof(T)
			.GetCustomAttributes(typeof(PacketIdAttribute), false)
			.FirstOrDefault() is not PacketIdAttribute attribute 
			? throw new InvalidOperationException($"Packet type {typeof(T).Name} is missing PacketIdAttribute.") 
			: attribute.Id;
	}
	
	public bool Valid(byte packetId)
	{
		return _packetTypes.ContainsKey(packetId);
	}

	public bool TryGetPacketType(byte packetId, out Type packetType)
	{
		return _packetTypes.TryGetValue(packetId, out packetType);
	}
}