using System;

namespace CrossServer.Shared;

public static class PacketParser
{
	public static IPacket ParsePacket(PacketRegistry registry, byte id, byte[]? data, int length)
	{
		if (!registry.Valid(id)) throw new Exception($"Unknown Packet ID {id}");
		
		IPacket? packet = registry.CreatePacket(id);
        if (packet == null) throw new Exception($"Failed to create packet for ID {id}");
        if (length == 0 || data == null) return packet;
        
		PacketReader reader = new(data.AsSpan(0, length));
		packet.Deserialize(ref reader);
		return packet;
	}
}