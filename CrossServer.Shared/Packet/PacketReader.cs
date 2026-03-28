using System;
using System.Buffers.Binary;
using System.Text;
using CrossServer.Shared.Interfaces;

namespace CrossServer.Shared;

public ref struct PacketReader(ReadOnlySpan<byte> data)
{
	
	private readonly ReadOnlySpan<byte> _data = data;

    public int Position { get; private set; } = 0;

    public int Length => _data.Length;

    public byte ReadByte()
	{
		return Position >= _data.Length 
			? throw new InvalidOperationException("End of stream reached.") 
			: _data[Position++];
	}
    
	public ReadOnlySpan<byte> ReadBytesFixedLength()
	{
		int length = ReadFixedLength();
		if (length < 0) throw new InvalidOperationException("Invalid byte array length.");
		
		if (Position + length > _data.Length)
		{
			throw new InvalidOperationException("Not enough data to read the byte array.");
		}
		
		return _data.Slice(Position, length);
	}
	
	public bool ReadBool()
	{
		byte value = ReadByte();
		if (value > 1) throw new InvalidOperationException($"Invalid boolean value: {value}");
		return value == 1;
	}

    public int ReadFixedLength()
    {
        return ReadInt();
    }

	public int ReadInt()
	{
		int value = BinaryPrimitives.ReadInt32LittleEndian(_data.Slice(Position, sizeof(int)));
		Position += sizeof(int);
		return value;
	}
	
	public long ReadInt64()
	{
		long value = BinaryPrimitives.ReadInt64LittleEndian(_data.Slice(Position, sizeof(long)));
		Position += sizeof(long);
		return value;
	}
	
	public uint ReadUInt()
	{
		uint value = BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(Position, sizeof(uint)));
		Position += sizeof(uint);
		return value;
	}
	
	public short ReadShort()
	{
		short value = BinaryPrimitives.ReadInt16LittleEndian(_data.Slice(Position, sizeof(short)));
		Position += sizeof(short);
		return value;
	}

    public Guid ReadGuid()
    {
        Guid guid = new(_data.Slice(Position, 16));
        Position += 16;
        return guid;
    }
	
	public T ReadEnum<T>() where T : Enum
	{
		return (T)(object)ReadByte();
	}
    
    public T[] ReadStructArray<T>() where T : struct, ISerializableData
    {
        int length = ReadFixedLength();
        if (length < 0) throw new InvalidOperationException("Invalid array length.");
        if (length == 0) return [];
        
        var array = new T[length];
        for (int i = 0; i < length; i++)
        {
            array[i] = ReadStruct<T>();
        }
        return array;
    }
    
    public T ReadStruct<T>() where T : struct, ISerializableData
    {
        T value = default;
        value.Deserialize(ref this);
        return value;
    }
	
	public string ReadString()
	{
		int length = ReadFixedLength();
		if (length <= 0) return string.Empty;
		
		if (Position + length > _data.Length)
		{
			throw new InvalidOperationException("Not enough data to read the string.");
		}
        
		string result = Encoding.UTF8.GetString(_data.Slice(Position, length));
		Position += length;
		return result;
	}
	
	public void Advance(int count) => Position += count;
	public void Rewind(int count) => Position -= count;
}