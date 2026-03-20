using System;

namespace CrossServer.Shared;

public static class PacketParser
{
	public static IPacket? ParsePacket(PacketRegistry registry, byte id, byte[]? data, int length)
    {
        if (!registry.Valid(id))
        {
            return null;
        }
		
		IPacket? packet = registry.CreatePacket(id);
        if (packet == null) return null;
        if (length == 0 || data == null) return packet;
        
		PacketReader reader = new(data.AsSpan(0, length));
		packet.Deserialize(ref reader);
		return packet;
	}
}