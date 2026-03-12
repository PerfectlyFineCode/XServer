using CrossServer.Shared;

namespace CrossServer.PacketGenerator.Sample;

public partial struct SamplePacket : IPacket
{
	public SamplePacket()
	{
	}

	public int Id { get; set; } = 42;
	public string? Name { get; set; } = "Sample";

	/// <inheritdoc />
	public void Serialize(PacketWriter writer)
	{
		throw new System.NotImplementedException();
	}

	/// <inheritdoc />
	public void Deserialize(ref PacketReader reader)
	{
		throw new System.NotImplementedException();
	}
}