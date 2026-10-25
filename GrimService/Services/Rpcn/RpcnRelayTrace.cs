using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace GrimService.Services.Rpcn
{


    public static class RpcnRelayTrace
    {
        public static string Describe(byte[] packet, bool fromClient, bool protocol32)
        {
            if (packet.Length != RpcnWire.Length(packet)) throw new InvalidDataException();
            var type = packet[0];
            var command = BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(1, 2));
            var request = BinaryPrimitives.ReadUInt64LittleEndian(packet.AsSpan(7, 8));
            int? result = !fromClient && type == 1 && packet.Length > 15 ? packet[15] : null;
            string payload = null;
            var detail = "payload omitted";
            if (protocol32 && command >= 12 && command <= 28 && command != 14 && command != 15 && packet.Length <= 131072)
            {
                payload = Convert.ToHexString(packet.AsSpan(15));
                detail = "matching payload hex (not protobuf-only)";
            }
            if (protocol32 && (command == 14 || command == 15) &&
                ((fromClient && type == 0) || (!fromClient && type == 1 && result == 0)))
            {


                var offset = fromClient ? 31 : 20;
                if (packet.Length >= offset && packet.Length - offset <= 131072 &&
                    BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(offset - 4, 4)) == packet.Length - offset)
                {
                    try
                    {
                        var redacted = RedactProto(packet.AsSpan(offset), fromClient ? (command == 14 ? 9 : 2) : -1);
                        payload = Convert.ToHexString(redacted);
                        detail = fromClient ? "protobuf hex; room password field removed" : "protobuf hex";
                    }
                    catch (InvalidDataException) { detail = "invalid protobuf; payload omitted"; }
                }
                else detail = "unsupported or oversized payload; omitted";
            }
            return JsonSerializer.Serialize(new {
                utc = DateTimeOffset.UtcNow, direction = fromClient ? "client->rpcn" : "rpcn->client",
                type, command, request, bytes = packet.Length, result, detail, protobufHex = payload
            });
        }


        public static byte[] RedactProto(ReadOnlySpan<byte> input, int passwordField)
        {
            using var output = new MemoryStream();
            int at = 0;
            while (at < input.Length)
            {
                int start = at;
                ulong tag = Varint(input, ref at);
                if (tag >> 3 == 0 || tag >> 3 > 0x1fffffff) throw new InvalidDataException();
                switch (tag & 7)
                {
                    case 0: Varint(input, ref at); break;
                    case 1: Skip(input, ref at, 8); break;
                    case 2: var n = Varint(input, ref at); if (n > int.MaxValue) throw new InvalidDataException(); Skip(input, ref at, (int)n); break;
                    case 5: Skip(input, ref at, 4); break;
                    default: throw new InvalidDataException();
                }
                if ((long)(tag >> 3) != passwordField) output.Write(input.Slice(start, at - start));
            }
            return output.ToArray();
        }
        private static void Skip(ReadOnlySpan<byte> data, ref int at, int n)
        {
            if (n < 0 || n > data.Length - at) throw new InvalidDataException(); at += n;
        }
        private static ulong Varint(ReadOnlySpan<byte> data, ref int at)
        {
            ulong result = 0;
            for (int shift = 0; shift < 70; shift += 7)
            {
                if (at >= data.Length) throw new InvalidDataException();
                byte b = data[at++]; if (shift == 63 && b > 1) throw new InvalidDataException();
                result |= (ulong)(b & 127) << shift;
                if ((b & 128) == 0) return result;
            }
            throw new InvalidDataException();
        }
    }
}
