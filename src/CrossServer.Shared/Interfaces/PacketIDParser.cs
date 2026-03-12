namespace CrossServer.Shared.Interfaces;

public interface IPacketIdParser
{
    public byte GetPacketId<T>() where T : IPacket;
}