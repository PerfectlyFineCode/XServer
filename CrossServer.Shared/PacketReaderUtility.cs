using System;
using System.Buffers;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CrossServer.Shared;

public static class PacketReaderUtility
{
	private const int MaxPacketSize = 1024 * 1024;
	
	public static async Task<(byte Id, byte[]? Payload, int Length, bool success)> ReadIncomingPacket(Socket socket, CancellationToken cancellationToken = default)
	{
		byte[] headerBuffer = new byte[PacketHeader.Size];
		int receivedFixed = await socket.ReceiveAsync(headerBuffer, SocketFlags.None, cancellationToken);
		if (receivedFixed == 0)
		{
			return (0, null, 0, false);
		}
        
        
		PacketReader headerReader = new(headerBuffer);
		PacketHeader? header = PacketHeader.Read(ref headerReader);
		if (header == null)
		{
			return (0, null, 0, false);
		}
		
		switch (header.Value.PayloadLength)
		{
			case < 0:
				return (0, null, 0, false);
			case 0:
				return (header.Value.Id, null, 0, true);
			case > MaxPacketSize:
				return (0, null, 0, false);
			default:
			{
				byte[] payloadBuffer = ArrayPool<byte>.Shared.Rent(header.Value.PayloadLength);
				try 
				{
					int receivedPayload = 0;
					while (receivedPayload < header.Value.PayloadLength)
					{
						int read = await socket.ReceiveAsync(
							new ArraySegment<byte>(payloadBuffer, receivedPayload, header.Value.PayloadLength - receivedPayload), 
							SocketFlags.None, 
							cancellationToken);

						if (read == 0)
						{
							ArrayPool<byte>.Shared.Return(payloadBuffer);
							return (0, null, 0, false);
						}
						receivedPayload += read;
					}

					return (header.Value.Id, payloadBuffer, header.Value.PayloadLength, true);
				}
				catch 
				{
					ArrayPool<byte>.Shared.Return(payloadBuffer);
					return (0, null, 0, false);
				}
			}
		}
	}
}