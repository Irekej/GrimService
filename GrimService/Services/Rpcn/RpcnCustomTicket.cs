using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GrimService.Config;

namespace GrimService.Services.Rpcn
{


    public sealed class RpcnCustomTicket
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("GRIMKEY1");
        private readonly ConcurrentDictionary<string, long> used = new();
        public static bool Matches(byte[] ticket) => ticket.Length >= 8 && ticket.AsSpan(0, 8).SequenceEqual(Magic);

        public bool TryConsume(byte[] ticket, RpcnBridgeOptions options, DateTimeOffset now,
            out RpcnBridgeDevice identity)
        {
            identity = null;
            if (!options.Enabled || !Matches(ticket) || ticket.Length < 66 || ticket.Length > 81) return false;
            int length = ticket[32];
            if (length < 1 || length > 16 || ticket.Length != 65 + length) return false;
            long issued = BinaryPrimitives.ReadInt64BigEndian(ticket.AsSpan(8, 8));
            long current = now.ToUnixTimeSeconds();
            if (issued < current - 120 || issued > current + 30) return false;
            string name = Encoding.ASCII.GetString(ticket, 33, length);
            if (name.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-')) return false;
            foreach (var candidate in options.Devices ?? new())
            {
                if (!candidate.Enabled || candidate.Username != name || candidate.KeySha256?.Length != 64) continue;
                byte[] key;
                try { key = Convert.FromHexString(candidate.KeySha256); }
                catch (FormatException) { continue; }
                byte[] mac = HMACSHA256.HashData(key, ticket.AsSpan(0, 33 + length));
                CryptographicOperations.ZeroMemory(key);
                if (!CryptographicOperations.FixedTimeEquals(mac, ticket.AsSpan(33 + length, 32))) continue;
                foreach (var previous in used.Where(p => p.Value < current)) used.TryRemove(previous.Key, out _);
                string fingerprint = Convert.ToHexString(SHA256.HashData(ticket));
                if (!used.TryAdd(fingerprint, issued + 121)) return false;
                identity = candidate;
                return true;
            }
            return false;
        }
    }
}
