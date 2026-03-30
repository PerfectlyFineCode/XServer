using System;
using CrossServer.Shared;
using CrossServer.Shared.Attributes;
using CrossServer.Shared.Structs;

namespace SetInStone.Packets
{
    [PacketId(102)]
    public struct ConnResponsePacket : IPacket
    {
        public bool IsRejected;
        public string Reason;
        
        public ConnResponsePacket(bool isRejected, string reason)
        {
            IsRejected = isRejected;
            Reason = reason;
        }
        
        /// <inheritdoc />
        public void Serialize(PacketWriter writer)
        {
            writer.Write(IsRejected);
            writer.WriteString(Reason);
        }

        /// <inheritdoc />
        public void Deserialize(ref PacketReader reader)
        {
            IsRejected = reader.ReadBool();
            Reason = reader.ReadString();
        }

        public ConnectionResponse ToConnectionResponse()
        { 
            return new ConnectionResponse(IsRejected, Reason);
        }
    }
}