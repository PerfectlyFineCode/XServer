namespace CrossServer.Shared;

public readonly struct PacketHeader(byte id, int payloadLength)
{
	public const int Size = sizeof(byte) + sizeof(int);
	
	public byte Id { get; } = id;
    
    public int PayloadLength { get; } = payloadLength;

    public static PacketHeader? Read(ref PacketReader reader)
	{
		byte id = reader.ReadByte();
        int length = reader.ReadInt();
		
		return length <= 0 
            ? new PacketHeader(id, 0) 
            : new PacketHeader(id, length);
    }

	public void Write(PacketWriter writer)
	{
		writer.Write(Id);
        writer.Write(PayloadLength);
    }
}