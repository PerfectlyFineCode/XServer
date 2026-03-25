using System;
using System.Buffers.Binary;
using System.Threading.Tasks;
using CrossServer.Shared.Interfaces;

namespace CrossServer.Shared;

public class PacketWriter : IDisposable, IAsyncDisposable
{
	private readonly System.IO.MemoryStream _stream = new();
	
	public int Position => (int)_stream.Position;
	public int Length => (int)_stream.Length;
	
	public void Write(int value)
	{
		Span<byte> buffer = stackalloc byte[sizeof(int)];
		BinaryPrimitives.WriteInt32BigEndian(buffer, value);
		_stream.Write(buffer);
	}
	
	public void Write(bool value)
	{
		_stream.WriteByte((byte)(value ? 1 : 0));
	}
	
	public void Write(long value)
	{
		Span<byte> buffer = stackalloc byte[sizeof(long)];
		BinaryPrimitives.WriteInt64BigEndian(buffer, value);
		_stream.Write(buffer);
	}
	
	public void Write(uint value)
	{
		Span<byte> buffer = stackalloc byte[sizeof(uint)];
		BinaryPrimitives.TryWriteUInt32BigEndian(buffer, value);
		_stream.Write(buffer);
	}
	
	public void Write(short value)
	{
		Span<byte> buffer = stackalloc byte[sizeof(short)];
		BinaryPrimitives.WriteInt16BigEndian(buffer, value);
		_stream.Write(buffer);
	}
	
	public void Write(byte value)
	{
		_stream.WriteByte(value);
	}
    
    public void Write(Guid value)
    {
        WriteBytes(value.ToByteArray());
    }
    
    public void Write<T>(T value) where T : struct, ISerializableData
    {
        value.Serialize(this);
    }
    
    public void WriteEnum<T>(T value) where T : Enum
    {
        Write((byte)(object)value);
    }
    
    public void WriteArray<T>(T[] array) where T : struct, ISerializableData
    {
        WriteFixedLength(array.Length);
        foreach (T item in array)
        {
            Write(item);
        }
    }

    public void WriteFixedLength(int value)
    {
        Write(value);
    }

	public void WriteString(string value)
	{
		Span<byte> bytes = System.Text.Encoding.UTF8.GetBytes(value);
		WriteFixedLength(bytes.Length);
		_stream.Write(bytes);
	}
	
	public void WriteBytesWithFixedLength(byte[] payload)
	{
        WriteFixedLength(payload.Length);
		_stream.Write(payload);
	}
	
	public void WriteBytes(byte[] payload)
	{
		_stream.Write(payload);
	}
    
    public void WriteBytes(ReadOnlySpan<byte> payload)
    {
        _stream.Write(payload);
    }
	
	public byte[] ToArray() => _stream.ToArray();

	/// <inheritdoc />
	public void Dispose()
	{
		_stream.Dispose();
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		await _stream.DisposeAsync();
	}
}