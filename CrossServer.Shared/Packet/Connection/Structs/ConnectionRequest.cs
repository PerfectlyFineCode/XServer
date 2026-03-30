using CrossServer.Shared.Structs;

namespace CrossServer.Shared;

public readonly struct ConnectionRequest(ClientContext client, PacketListener packetListener)
{
    public void Accept()
    {
        client.Accept();
        packetListener.OnClientAccepted(client, new ConnectionResponse(false, string.Empty));
    }

    public void Reject(string reason)
    {
        client.Reject();
        packetListener.OnClientRejected(client, new ConnectionResponse(true, reason));
    }
}