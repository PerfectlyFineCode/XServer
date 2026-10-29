using System;
using System.Buffers.Binary;
using System.Collections.Generic;
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
		ulong length = ReadVarLength();
		
		if ((uint)Position + length > (ulong)_data.Length)
		{
			throw new InvalidOperationException("Not enough data to read the byte array.");
		}
		
		return _data.Slice(Position, (int)length);
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

    /// <summary>
    /// Based on <see href="https://en.wikipedia.org/wiki/LEB128">LEB128</see> encoding.
    /// </summary>
    public ulong ReadVarLength()
    {
	    ulong result = 0;
		int shift = 0;

		while (true)
		{
			if (Position >= _data.Length)
				throw new InvalidOperationException("End of stream reached while reading varint.");
			
			byte b = ReadByte();
			result |= (ulong)(b & 0x7F) << shift;

			if ((b & 0x80) == 0)
				break;

			shift += 7;
			if (shift > 63)
				throw new InvalidOperationException("Varint is too long.");
		}

		return result;
    }

	public int ReadInt()
	{
		int value = BinaryPrimitives.ReadInt32BigEndian(_data.Slice(Position, sizeof(int)));
		Position += sizeof(int);
		return value;
	}
	
	public long ReadInt64()
	{
		long value = BinaryPrimitives.ReadInt64BigEndian(_data.Slice(Position, sizeof(long)));
		Position += sizeof(long);
		return value;
	}
	
	public uint ReadUInt()
	{
		uint value = BinaryPrimitives.ReadUInt32BigEndian(_data.Slice(Position, sizeof(uint)));
		Position += sizeof(uint);
		return value;
	}
	
	public short ReadShort()
	{
		short value = BinaryPrimitives.ReadInt16BigEndian(_data.Slice(Position, sizeof(short)));
		Position += sizeof(short);
		return value;
	}
    
    public float ReadFloat()
    {
        float value = BinaryPrimitives.ReadInt32BigEndian(_data.Slice(Position, sizeof(int)));
        Position += sizeof(float);
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
    
    public int[] ReadIntArray()
    {
        ulong length = ReadVarLength();
        if (length == 0) return [];
        
        int[] array = new int[length];
        for (uint i = 0; i < length; i++)
        {
            array[i] = ReadInt();
        }
        return array;
    }
    
    public T[] ReadStructArray<T>() where T : struct, ISerializableData
    {
        ulong length = ReadVarLength();
        if (length == 0) return [];
        
        var array = new T[length];
        for (uint i = 0; i < length; i++)
        {
            array[i] = ReadStruct<T>();
        }
        return array;
    }

    public List<T> ReadStructList<T>() where T : struct, ISerializableData
    {
        ulong length = ReadVarLength();
        if (length == 0) return [];
        
        var array = new List<T>();
        for (uint i = 0; i < length; i++)
        {
            array.Add(ReadStruct<T>());
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
		ulong length = ReadVarLength();
		if (length <= 0) return string.Empty;
		
		if ((uint)Position + length > (ulong)_data.Length)
		{
			throw new InvalidOperationException("Not enough data to read the string.");
		}
        
		string result = Encoding.UTF8.GetString(_data.Slice(Position, (int)length));
		Position += (int)length;
		return result;
	}
	
	public void Advance(int count) => Position += count;
	public void Rewind(int count) => Position -= count;
}