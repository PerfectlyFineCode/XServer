using System;

namespace CrossServer.Shared.Attributes;

public sealed class PacketIdAttribute : Attribute
{
	public byte Id { get; }
	
	public PacketIdAttribute(byte packetId)
	{
		Id = packetId;
	}
}