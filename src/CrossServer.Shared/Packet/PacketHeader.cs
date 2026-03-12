using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Net.Sockets;

namespace CrossServer.Shared;

public struct PacketHeader
{
	public const int Size = sizeof(byte) + sizeof(byte);
	
	public byte Id { get; set; }
	public byte LengthType { get; set; }
	
	public int PayloadLength { get; set; }
	
	public PacketHeader(byte id, int payloadLength)
	{
		Id = id;
		PayloadLength = payloadLength;
		LengthType = payloadLength switch
		{
			< byte.MaxValue  => 0,
			< short.MaxValue => 1,
			_                => 2
		};
	}

	public static PacketHeader? Read(ref PacketReader reader)
	{
		byte id = reader.ReadByte();
		byte lengthType = reader.ReadByte();
		int length = lengthType switch
		{
			0 => reader.ReadByte(),
			1 => reader.ReadShort(),
			2 => reader.ReadInt(),
			_ => 0
		};
		
		if (length <= 0)
		{
			return null;
		}
		
		return new PacketHeader(id, length)
		{
			LengthType = lengthType
		};
	}

	public void Write(PacketWriter writer)
	{
		writer.Write(Id);
		writer.Write(LengthType);
		if (LengthType == 0) writer.Write((byte)PayloadLength);
		else if (LengthType == 1) writer.Write((short)PayloadLength);
		else writer.Write(PayloadLength);
	}
}