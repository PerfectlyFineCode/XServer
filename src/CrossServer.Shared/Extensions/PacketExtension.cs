namespace CrossServer.Shared.Extensions;

public static class PacketExtension
{
	public static byte[] Build<T>(this T packet) where T : IPacket
	{
		using PacketWriter payloadWriter = new();
		packet.Serialize(payloadWriter);
		byte[] payload = payloadWriter.ToArray();

		PacketHeader header = new(PacketRegistry.GetPacketId(packet.GetType()), payload.Length);

		using PacketWriter finalWriter = new();
		header.Write(finalWriter);
		finalWriter.WriteBytes(payload);

		return finalWriter.ToArray();
	}
}