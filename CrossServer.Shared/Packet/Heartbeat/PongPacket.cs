using CrossServer.Shared.Attributes;

namespace CrossServer.Shared.Heartbeat;

[PacketId(101)]
public struct PongPacket : IPacket
{
	public long Timestamp { get; set; }

	public void Serialize(PacketWriter writer) => writer.Write(Timestamp);
	public void Deserialize(ref PacketReader reader) => Timestamp = reader.ReadInt64();
}