using SetInStone.Packets;

namespace CrossServer.Shared.Structs;

public readonly struct ConnectionResponse(bool isRejected, string errorMessage)
{
    public bool IsRejected { get; } = isRejected;
    public string ErrorMessage { get; } = errorMessage;
    
    public ConnResponsePacket ToPacket()
    {
        return new ConnResponsePacket(IsRejected, ErrorMessage);
    } 
}