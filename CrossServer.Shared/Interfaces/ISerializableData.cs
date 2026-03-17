namespace CrossServer.Shared.Interfaces;

public interface ISerializableData
{
    public void Serialize(PacketWriter writer);
    public void Deserialize(ref PacketReader reader);
}