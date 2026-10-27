using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GrimService.Config;

namespace GrimService.Services.Rpcn
{
    public sealed class RpcnRoomException : IOException
    {
        public int Result { get; }
        public RpcnRoomException(int result) : base("RPCN request rejected") => Result = result;
    }

    internal sealed class RoomProto
    {
        internal sealed record Field(int Id, ulong Number, byte[] Data);
        private readonly List<Field> fields = new();
        public RoomProto(byte[] bytes)
        {
            int position = 0;
            while (position < bytes.Length)
            {
                if (fields.Count >= 4096) throw new InvalidDataException("Too many RPCN fields");
                ulong tag = Take(bytes, ref position);
                int id = checked((int)(tag >> 3));
                if (id == 0) throw new InvalidDataException("Invalid protobuf field");
                switch (tag & 7)
                {
                    case 0: fields.Add(new(id, Take(bytes, ref position), Array.Empty<byte>())); break;
                    case 2:
                        int size = checked((int)Take(bytes, ref position));
                        if (size > bytes.Length - position) throw new InvalidDataException("Truncated RPCN field");
                        fields.Add(new(id, 0, bytes.AsSpan(position, size).ToArray())); position += size; break;
                    case 1: Skip(8); break;
                    case 5: Skip(4); break;
                    default: throw new InvalidDataException("Invalid protobuf wire type");
                }
            }
            void Skip(int size)
            {
                if (size > bytes.Length - position) throw new InvalidDataException("Truncated RPCN field");
                position += size;
            }
        }
        public ulong Number(int field) => fields.FirstOrDefault(f => f.Id == field)?.Number ?? 0;
        public byte[] Bytes(int field) => fields.FirstOrDefault(f => f.Id == field)?.Data ?? Array.Empty<byte>();
        public RoomProto Child(int field) => new(Bytes(field));
        public IEnumerable<RoomProto> Children(int field) => fields.Where(f => f.Id == field).Select(f => new RoomProto(f.Data));
        public string Text(int field) => new UTF8Encoding(false, true).GetString(Bytes(field));
        public ulong Wrapped(int field) => Child(field).Number(1);
        public static byte[] Make(params (int Field, ulong Value)[] numbers)
        {
            var bytes = new List<byte>();
            foreach (var number in numbers) { Write(bytes, (ulong)number.Field << 3); Write(bytes, number.Value); }
            return bytes.ToArray();
        }
        public static byte[] AddIds(byte[] message, int field, params ushort[] ids)
        {
            var bytes = new List<byte>(message);
            foreach (var id in ids)
            {
                var value = Make((1, id)); Write(bytes, ((ulong)field << 3) | 2); Write(bytes, (ulong)value.Length); bytes.AddRange(value);
            }
            return bytes.ToArray();
        }
        private static void Write(List<byte> bytes, ulong value)
        {
            while (value >= 128) { bytes.Add((byte)((value & 127) | 128)); value >>= 7; }
            bytes.Add((byte)value);
        }
        private static ulong Take(byte[] bytes, ref int position)
        {
            ulong value = 0;
            for (int i = 0; i < 10; i++)
            {
                if (position >= bytes.Length) throw new InvalidDataException("Truncated protobuf integer");
                byte part = bytes[position++];
                if (i == 9 && part > 1) throw new InvalidDataException("Overflowed protobuf integer");
                value |= (ulong)(part & 127) << (7 * i);
                if (part < 128) return value;
            }
            throw new InvalidDataException("Invalid protobuf integer");
        }
        public static RoomProto Response(byte[] bytes)
        {
            if (bytes.Length < 4 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != bytes.Length - 4)
                throw new InvalidDataException("Invalid protobuf response length");
            return new(bytes.AsSpan(4).ToArray());
        }
    }

    internal sealed class RpcnRoomConnection : IDisposable
    {
        private readonly TcpClient tcp = new() { NoDelay = true };
        private SslStream tls;
        private ulong sequence;
        public uint Protocol { get; private set; }
        public async Task ConnectAsync(RpcnRoomsOptions cfg, CancellationToken stop)
        {
            await tcp.ConnectAsync(cfg.Host, cfg.Port, stop);
            tls = new SslStream(tcp.GetStream(), false, (_, cert, _, errors) =>
            {
                if (cert == null) return false;
                if (string.IsNullOrWhiteSpace(cfg.CertificateSha256)) return errors == SslPolicyErrors.None;
                var actual = SHA256.HashData(cert.GetRawCertData());
                return CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(cfg.CertificateSha256));
            });
            await tls.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = cfg.Host }, stop);
            Protocol = RpcnWire.ServerVersion(await RpcnWire.ReadAsync(tls, stop));
            if (Protocol < 32) throw new InvalidDataException("RPCN protocol older than 32 is unsupported");
            byte[] credentials = Encoding.UTF8.GetBytes(cfg.Username + "\0" + cfg.PasswordHash + "\0" + cfg.Token + "\0");
            try { await CallAsync(0, credentials, stop); }
            finally { CryptographicOperations.ZeroMemory(credentials); }
        }
        public async Task<byte[]> CallAsync(ushort command, byte[] body, CancellationToken stop)
        {
            if (command != 0 && command != 12 && command != 13 && command != 17 && command != 62)
                throw new InvalidOperationException("Only login and read-only room commands are allowed");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var ct = timeout.Token;
            ulong id = ++sequence;
            byte[] frame = new byte[15 + body.Length];
            BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(1), command);
            BinaryPrimitives.WriteUInt32LittleEndian(frame.AsSpan(3), (uint)frame.Length);
            BinaryPrimitives.WriteUInt64LittleEndian(frame.AsSpan(7), id);
            body.CopyTo(frame, 15);
            try { await tls.WriteAsync(frame, ct); }
            finally { if (command == 0) CryptographicOperations.ZeroMemory(frame); }
            for (int skipped = 0; skipped < 128; skipped++)
            {
                var packet = await RpcnWire.ReadAsync(tls, ct);
                if (packet[0] == 2) continue;
                if (packet.Length < 16 || packet[0] != 1 || BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(1)) != command ||
                    BinaryPrimitives.ReadUInt64LittleEndian(packet.AsSpan(7)) != id) throw new InvalidDataException("Unexpected RPCN response");
                if (packet[15] != 0) throw new RpcnRoomException(packet[15]);
                return packet.AsSpan(16).ToArray();
            }
            throw new InvalidDataException("Too many RPCN notifications");
        }
        public static byte[] Pack(string communication, byte[] message) => Encoding.ASCII.GetBytes(communication)
            .Concat(BitConverter.GetBytes(message.Length)).Concat(message).ToArray();
        public void Dispose() { tls?.Dispose(); tcp.Dispose(); }
    }
}
