using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using System.Threading.Tasks;

namespace CrossServer.Shared;

public ref struct PacketReader
{
	
	private readonly ReadOnlySpan<byte> _data;
	private int _position;

	public int Position => _position;
	public int Length => _data.Length;
	
	public PacketReader(ReadOnlySpan<byte> data)
	{
		_data = data;
		_position = 0;
	}

	public byte ReadByte()
	{
		return _position >= _data.Length 
			? throw new InvalidOperationException("End of stream reached.") 
			: _data[_position++];
	}
	
	public ReadOnlySpan<byte> ReadBytesVarLength()
	{
		int length = ReadVariableLength();
		if (length < 0) throw new InvalidOperationException("Invalid byte array length.");
		
		if (_position + length > _data.Length)
		{
			throw new InvalidOperationException("Not enough data to read the byte array.");
		}
		
		return _data.Slice(_position, length);
	}
	
	public bool ReadBool()
	{
		byte value = ReadByte();
		if (value > 1) throw new InvalidOperationException($"Invalid boolean value: {value}");
		return value == 1;
	}

	public int ReadVariableLength()
	{
		int length = ReadByte();
		if (length != byte.MaxValue) return length;
		_position -= 1;
		length = ReadShort();

		if (length != short.MaxValue) return length;
		_position -= 2;
		length = ReadInt();

		return length;
	}

	public int ReadInt()
	{
		int value = BinaryPrimitives.ReadInt32BigEndian(_data.Slice(_position, sizeof(int)));
		_position += sizeof(int);
		return value;
	}
	
	public long ReadInt64()
	{
		long value = BinaryPrimitives.ReadInt64BigEndian(_data.Slice(_position, sizeof(long)));
		_position += sizeof(long);
		return value;
	}
	
	public uint ReadUInt()
	{
		uint value = BinaryPrimitives.ReadUInt32BigEndian(_data.Slice(_position, sizeof(uint)));
		_position += sizeof(uint);
		return value;
	}
	
	public short ReadShort()
	{
		short value = BinaryPrimitives.ReadInt16BigEndian(_data.Slice(_position, sizeof(short)));
		_position += sizeof(short);
		return value;
	}
	
	public T ReadEnum<T>() where T : Enum
	{
		return (T)(object)ReadInt();
	}
	
	public string ReadString()
	{
		int length = ReadInt();
		if (length <= 0) return string.Empty;
		
		if (_position + length > _data.Length)
		{
			throw new InvalidOperationException("Not enough data to read the string.");
		}
        
		string result = Encoding.UTF8.GetString(_data.Slice(_position, length));
		_position += length;
		return result;
	}
	
	public void Advance(int count) => _position += count;
	public void Rewind(int count) => _position -= count;
}