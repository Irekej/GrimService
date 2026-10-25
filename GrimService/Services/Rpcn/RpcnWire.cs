using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GrimService.Services.Rpcn
{

    public static class RpcnWire
    {
        public const int HeaderSize = 15;
        public const int MaximumPacketSize = 0x800000;
        public static int Length(byte[] header)
        {
            if (header.Length < HeaderSize) throw new InvalidDataException("Truncated RPCN header.");
            var length = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(3, 4));
            if (length < HeaderSize || length > MaximumPacketSize)
                throw new InvalidDataException("Invalid RPCN packet length.");
            return (int)length;
        }

        public static async Task<byte[]> ReadAsync(Stream stream, CancellationToken cancellation)
        {
            var header = new byte[HeaderSize];
            await stream.ReadExactlyAsync(header.AsMemory(), cancellation);
            var packet = new byte[Length(header)];
            header.CopyTo(packet, 0);
            await stream.ReadExactlyAsync(packet.AsMemory(HeaderSize), cancellation);
            return packet;
        }

        public static uint ServerVersion(byte[] packet)
        {
            if (packet.Length != 19 || Length(packet) != packet.Length || packet[0] != 3)
                throw new InvalidDataException("Expected RPCN ServerInfo.");
            return BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(HeaderSize, 4));
        }

        public static ushort ValidateRequest(byte[] packet, string username)
        {
            if (packet.Length != Length(packet) || packet[0] != 0)
                throw new InvalidDataException("Expected a complete RPCN request.");
            var command = BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(1, 2));


            if (command != 0 && command != 1 && command != 37 && command != 51 && command != 62 &&
                (command < 12 || command > 28))
                throw new InvalidDataException("RPCN command is outside the bridge contract.");
            if (command == 0)
            {
                var end = Array.IndexOf(packet, (byte)0, HeaderSize);
                if (end < HeaderSize || end - HeaderSize > 16 ||
                    Encoding.UTF8.GetString(packet, HeaderSize, end - HeaderSize) != username)
                    throw new InvalidDataException("RPCN login does not match the paired account.");
            }
            return command;
        }
    }
}
