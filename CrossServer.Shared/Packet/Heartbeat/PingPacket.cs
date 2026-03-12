using System;
using CrossServer.Shared.Attributes;

namespace CrossServer.Shared.Heartbeat;

[PacketId(100)]
public struct PingPacket : IPacket
{
	public long Timestamp { get; set; }

	public PingPacket() => Timestamp = DateTime.UtcNow.Ticks;

	public void Serialize(PacketWriter writer) => writer.Write(Timestamp);
	public void Deserialize(ref PacketReader reader) => Timestamp = reader.ReadInt64();
}