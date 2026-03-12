namespace CrossServer.Shared.Interfaces;

public interface IPacketProvider
{
    IPacket? CreatePacket(byte packetId);
}