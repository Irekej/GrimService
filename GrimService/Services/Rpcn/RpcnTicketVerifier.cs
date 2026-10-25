using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GrimService.Config;
using GrimService.Sony;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

namespace GrimService.Services.Rpcn
{
    public sealed class RpcnTicketVerifier
    {
        private readonly ConcurrentDictionary<string, ulong> used = new();
        public static bool IsRpcn(byte[] data) => data.Length >= 4 &&
            BinaryPrimitives.ReadUInt32BigEndian(data) == 0x21010000;
        public static bool IsEnvelope(byte[] data) => data.Length >= 8 &&
            data.AsSpan(0, 8).SequenceEqual(Encoding.ASCII.GetBytes("GRIMRPC2"));



        public bool TryConsume(byte[] input, RpcnBridgeOptions options, DateTimeOffset now,
            out NPTicket identity, out string platform, out string reason)
            => TryConsumeWithService(input, options, now, out identity, out platform, out reason, out _);



        public bool TryConsumeWithService(byte[] input, RpcnBridgeOptions options, DateTimeOffset now,
            out NPTicket identity, out string platform, out string reason, out string signedServiceId)
        {
            identity = null; platform = "RPCS3"; reason = "invalid-ticket"; signedServiceId = null;
            try {
                byte[] ticket = input;
                string country = null;
                if (IsEnvelope(input)) {
                    if (input.Length < 14 || input[11] != 0 || (input[10] != 1 && input[10] != 2)) return false;
                    int length = BinaryPrimitives.ReadUInt16BigEndian(input.AsSpan(12, 2));
                    if (input.Length != 14 + length || !IsCountry(input[8], input[9])) return false;
                    country = Encoding.ASCII.GetString(input, 8, 2);
                    platform = input[10] == 1 ? "PS3" : "RPCS3";
                    ticket = input.AsSpan(14).ToArray();
                }
                if (!IsRpcn(ticket) || ticket.Length < 20 || ticket.Length >= 0x300 ||
                    BinaryPrimitives.ReadUInt32BigEndian(ticket.AsSpan(4, 4)) != ticket.Length - 8) return false;
                int offset = 8;
                byte[] ReadField(ushort expected)
                {
                    if (ticket.Length - offset < 4) throw new InvalidDataException();
                    int type = BinaryPrimitives.ReadUInt16BigEndian(ticket.AsSpan(offset, 2));
                    int length = BinaryPrimitives.ReadUInt16BigEndian(ticket.AsSpan(offset + 2, 2));
                    offset += 4;
                    if (type != expected || length > ticket.Length - offset) throw new InvalidDataException();
                    var bytes = ticket.AsSpan(offset, length).ToArray(); offset += length; return bytes;
                }
                var body = ReadField(0x3000);
                int signedLength = offset - 8;
                var signatureBlob = ReadField(0x3002);
                if (offset != ticket.Length || signatureBlob.Length < 12 ||
                    BinaryPrimitives.ReadUInt16BigEndian(signatureBlob) != 8 ||
                    BinaryPrimitives.ReadUInt16BigEndian(signatureBlob.AsSpan(2)) != 4 ||
                    !signatureBlob.AsSpan(4, 4).SequenceEqual(Encoding.ASCII.GetBytes("RPCN")) ||
                    BinaryPrimitives.ReadUInt16BigEndian(signatureBlob.AsSpan(8)) != 8 ||
                    BinaryPrimitives.ReadUInt16BigEndian(signatureBlob.AsSpan(10)) != signatureBlob.Length - 12) return false;

                var publicKey = new PemReader(new StringReader(options.TicketPublicKeyPem)).ReadObject() as AsymmetricKeyParameter;
                if (publicKey == null || publicKey.IsPrivate) return false;
                var signer = SignerUtilities.GetSigner("SHA-224withECDSA");
                signer.Init(false, publicKey); signer.BlockUpdate(ticket, 8, signedLength);
                if (!signer.VerifySignature(signatureBlob.AsSpan(12).ToArray())) { reason = "invalid-signature"; return false; }

                int position = 0;
                byte[] Field(ushort expected, int size)
                {
                    if (body.Length - position < 4 || BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(position)) != expected)
                        throw new InvalidDataException();
                    int length = BinaryPrimitives.ReadUInt16BigEndian(body.AsSpan(position + 2)); position += 4;
                    if ((size >= 0 && length != size) || length > body.Length - position) throw new InvalidDataException();
                    var bytes = body.AsSpan(position, length).ToArray(); position += length; return bytes;
                }
                Field(8, 20); Field(1, 4);
                ulong issued = BinaryPrimitives.ReadUInt64BigEndian(Field(7, 8));
                ulong expires = BinaryPrimitives.ReadUInt64BigEndian(Field(7, 8));
                ulong subject = BinaryPrimitives.ReadUInt64BigEndian(Field(2, 8));
                string name = Encoding.ASCII.GetString(Field(4, 32)).TrimEnd('\0');
                var region = Field(8, 4);
                string domain = Encoding.ASCII.GetString(Field(4, 4)).TrimEnd('\0');
                var service = Field(8, 24);
                string serviceName = Encoding.ASCII.GetString(service).TrimEnd('\0');
                signedServiceId = new string(serviceName.Select(c => char.IsControl(c) ? '?' : c).ToArray());
                if (name.Length < 3 || name.Length > 16 || name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-') ||
                    subject == 0 || subject > long.MaxValue) return false;
                ulong current = (ulong)now.ToUnixTimeMilliseconds();
                if (issued > current + 30000 || expires <= current || expires <= issued || expires - issued > 900000) {
                    reason = "expired-or-invalid-time";
                }
                if (options.AllowedTicketServiceIds == null || !options.AllowedTicketServiceIds.Contains(serviceName, StringComparer.Ordinal)) {
                    reason = "wrong-service";
                }
                foreach (var previous in used.Where(pair => pair.Value <= current)) used.TryRemove(previous.Key, out _);


                if (used.Count >= 100000 || !used.TryAdd(Convert.ToHexString(SHA256.HashData(ticket.AsSpan(8, signedLength))), expires)) {
                    reason = "replayed-ticket";
                }
                identity = new NPTicket { OnlineId = name, UserId = subject, IssuedDate = issued, ExpiryDate = expires,
                    Region = country ?? Encoding.ASCII.GetString(region, 0, 2).ToUpperInvariant(), Domain = domain,
                    ServiceID = service, ConsoleID = null, VersionMajor = 2, VersionMinor = 1 };
                reason = null; return true;
            } catch (Exception error) when (error is ArgumentException || error is IOException || error is CryptoException ||
                error is InvalidOperationException || error is NullReferenceException) { return false; }
        }

        private static bool IsCountry(byte a, byte b) => a >= 'A' && a <= 'Z' && b >= 'A' && b <= 'Z';
    }
}
