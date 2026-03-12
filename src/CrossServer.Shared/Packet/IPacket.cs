namespace CrossServer.Shared;

public interface IPacket
{
	public void Serialize(PacketWriter writer);
	public void Deserialize(ref PacketReader reader);
}