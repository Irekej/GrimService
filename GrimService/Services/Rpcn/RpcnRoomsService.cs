using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GrimService.Config;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GrimService.Services.Rpcn
{
    public sealed record RpcnPublicRoom(string Game, string Id, string Name, string Owner, int Players,
        int Capacity, string[] Members, bool MembersAvailable, string Track, int? TrackCode, bool FixedHost);
    public sealed record RpcnRoomsSnapshot(string Status, DateTimeOffset? UpdatedUtc, RpcnPublicRoom[] Rooms,
        bool Truncated, int RefreshSeconds);

    public sealed class RpcnRoomsService : BackgroundService
    {
        private readonly RpcnRoomsOptions cfg;
        private readonly ILogger<RpcnRoomsService> log;
        private readonly Dictionary<(string, int), string> tracks = new();
        private RpcnRoomsSnapshot snapshot;
        public RpcnRoomsSnapshot Snapshot => Volatile.Read(ref snapshot);
        public RpcnRoomsService(IOptions<RpcnRoomsOptions> options, IWebHostEnvironment env, ILogger<RpcnRoomsService> logger)
        {
            cfg = options.Value; log = logger;
            snapshot = new("disabled", null, Array.Empty<RpcnPublicRoom>(), false, Math.Clamp(cfg.RefreshSeconds, 30, 300));
            var path = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "data", "event-catalog.json");
            if (!File.Exists(path)) return;
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (!doc.RootElement.TryGetProperty("tracks", out var catalog) || catalog.ValueKind != JsonValueKind.Array) return;
                foreach (var course in catalog.EnumerateArray())
                    if (course.TryGetProperty("game", out var game) && game.ValueKind == JsonValueKind.String &&
                        course.TryGetProperty("code", out var code) && code.TryGetInt32(out var id) &&
                        course.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                        tracks.TryAdd((game.GetString(), id), name.GetString());
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            { log.LogWarning("RPCN rooms: track catalog unavailable; numeric track codes will be shown"); }
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!cfg.Enabled) return;
            if (!ValidConfiguration())
            {
                PublishStatus("configuration");
                log.LogWarning("RPCN rooms disabled: check RpcnRooms configuration (no credentials logged)");
                return;
            }
            RpcnRoomConnection connection = null;
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        using var poll = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        poll.CancelAfter(TimeSpan.FromSeconds(90));
                        if (connection == null)
                        {
                            PublishStatus("connecting");
                            connection = new();
                            using var handshake = CancellationTokenSource.CreateLinkedTokenSource(poll.Token);
                            handshake.CancelAfter(TimeSpan.FromSeconds(15));
                            await connection.ConnectAsync(cfg, handshake.Token);
                            if (connection.Protocol > 34) log.LogWarning("RPCN rooms: protocol {Protocol} is newer than verified version 34", connection.Protocol);
                        }
                        var rooms = new List<RpcnPublicRoom>();
                        bool truncated = await ReadGame(connection, "GT5", cfg.GT5CommunicationId, rooms, poll.Token);
                        truncated |= await ReadGame(connection, "GT6", cfg.GT6CommunicationId, rooms, poll.Token);
                        Volatile.Write(ref snapshot, new("ready", DateTimeOffset.UtcNow, rooms.ToArray(), truncated, Snapshot.RefreshSeconds));
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                    catch (Exception ex)
                    {
                        connection?.Dispose(); connection = null;
                        string status = ex is RpcnRoomException error ? error.Result == 6 ? "account-in-use" : "rejected" : "unavailable";
                        PublishStatus(status);
                        log.LogWarning("RPCN rooms refresh failed: type={ErrorType}, result={Result}; previous snapshot retained", ex.GetType().Name,
                            ex is RpcnRoomException rpcn ? rpcn.Result : (int?)null);
                    }
                    await Task.Delay(TimeSpan.FromSeconds(Snapshot.RefreshSeconds), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            finally { connection?.Dispose(); }
        }
        private bool ValidConfiguration() => !string.IsNullOrWhiteSpace(cfg.Host) && cfg.Port is > 0 and <= 65535 &&
            Regex.IsMatch(cfg.Username ?? "", "^[A-Za-z0-9_-]{3,16}$") && Regex.IsMatch(cfg.PasswordHash ?? "", "^[A-Fa-f0-9]{64}$") &&
            Regex.IsMatch(cfg.Token ?? "", "^([A-Fa-f0-9]{16})?$") &&
            (string.IsNullOrWhiteSpace(cfg.CertificateSha256) || Regex.IsMatch(cfg.CertificateSha256, "^[A-Fa-f0-9]{64}$")) &&
            Regex.IsMatch(cfg.GT5CommunicationId ?? "", "^[A-Z0-9]{9}_[0-9]{2}$") &&
            Regex.IsMatch(cfg.GT6CommunicationId ?? "", "^[A-Z0-9]{9}_[0-9]{2}$");
        private void PublishStatus(string status) => Volatile.Write(ref snapshot, Snapshot with { Status = status });
        private async Task<bool> ReadGame(RpcnRoomConnection connection, string game, string com, List<RpcnPublicRoom> rooms, CancellationToken ct)
        {
            var servers = await connection.CallAsync(12, Encoding.ASCII.GetBytes(com), ct);
            if (servers.Length < 2) throw new InvalidDataException("Truncated server list");
            int count = BinaryPrimitives.ReadUInt16LittleEndian(servers);
            if (count > 32 || servers.Length != 2 + 2 * count) throw new InvalidDataException("Invalid server list");
            var seen = new HashSet<ulong>();
            bool truncated = false;
            for (int i = 0; i < count; i++)
            {
                var server = servers.AsSpan(2 + i * 2, 2).ToArray();
                var worlds = await connection.CallAsync(13, Encoding.ASCII.GetBytes(com).Concat(server).ToArray(), ct);
                if (worlds.Length < 4) throw new InvalidDataException("Truncated world list");
                uint worldCount = BinaryPrimitives.ReadUInt32LittleEndian(worlds);
                if (worldCount > 64 || worlds.Length != 4 + 4 * worldCount) throw new InvalidDataException("Invalid world list");
                for (int j = 0; j < worldCount; j++)
                {
                    uint world = BinaryPrimitives.ReadUInt32LittleEndian(worlds.AsSpan(4 + j * 4));
                    for (uint start = 1; start <= 181; start += 20)
                    {
                        byte[] request = RoomProto.AddIds(RoomProto.Make((2, world), (4, start), (5, 20)), 10,
                            0x4c, 0x4d, 0x4e, 0x4f, 0x50, 0x51, 0x52, 0x53, 0x54, 0x55, 0x56);
                        var response = RoomProto.Response(await connection.CallAsync(17, RpcnRoomConnection.Pack(com, request), ct));
                        var page = response.Children(3).ToArray();
                        foreach (var room in page)
                        {
                            if ((room.Number(14) & 0x10000000UL) != 0) continue;
                            ulong id = room.Number(6);
                            if (!seen.Add(id) || id == 0) continue;
                            if (seen.Count > 200) return true;
                            string[] members = Array.Empty<string>(); bool membersAvailable = false;
                            try
                            {
                                var data = Encoding.ASCII.GetBytes(com).Concat(BitConverter.GetBytes(id)).ToArray();
                                var memberData = RoomProto.Response(await connection.CallAsync(62, data, ct));
                                members = memberData.Children(1).Select(m => m.Child(1).Text(1)).Where(n => n.Length > 0)
                                    .Distinct(StringComparer.Ordinal).Take(64).ToArray();
                                membersAvailable = true;
                            }
                            catch (RpcnRoomException error) when (error.Result is 14 or 23 or 26 or 33) { }
                            rooms.Add(DecodeRoom(game, room, members, membersAvailable));
                        }
                        if (start + 20 > response.Number(2) || page.Length == 0) break;
                        if (start == 181) truncated = true;
                    }
                }
            }
            return truncated;
        }
        internal RpcnPublicRoom DecodeRoom(string game, RoomProto room, string[] members, bool membersAvailable)
        {
            var integers = room.Children(15).ToDictionary(a => (int)a.Wrapped(1), a => a.Number(2));
            int? course = game == "GT5" && integers.TryGetValue(0x4e, out var code) && code <= int.MaxValue ? (int)code : null;
            string name = null;
            var metadata = room.Children(17).FirstOrDefault(a => a.Wrapped(1) == 0x55)?.Bytes(2);
            int titleOffset = 0;
            if (game == "GT5" && metadata?.Length == 256 && course.HasValue &&
                BinaryPrimitives.ReadUInt16BigEndian(metadata) == course && BinaryPrimitives.ReadUInt32BigEndian(metadata.AsSpan(36)) == 64)
                titleOffset = 40;
            if (game == "GT6" && metadata?.Length == 256 && BinaryPrimitives.ReadUInt32BigEndian(metadata.AsSpan(104)) == 64 &&
                BinaryPrimitives.ReadUInt32BigEndian(metadata.AsSpan(172)) == 32 && BinaryPrimitives.ReadUInt32BigEndian(metadata.AsSpan(224)) == 16)
            {
                int storedCourse = BinaryPrimitives.ReadInt32BigEndian(metadata.AsSpan(4));
                int realCourse = storedCourse == 543 ? BinaryPrimitives.ReadInt32BigEndian(metadata.AsSpan(8)) : storedCourse;
                if (realCourse >= 0) course = realCourse;
                titleOffset = 108;
            }
            if (titleOffset != 0)
            {
                var title = metadata.AsSpan(titleOffset, 64);
                int end = title.IndexOf((byte)0);
                try
                {
                    name = new UTF8Encoding(false, true).GetString(end >= 0 ? title[..end] : title);
                    if (name.Any(char.IsControl)) name = null;
                }
                catch (DecoderFallbackException) { }
            }
            string track = null;
            if (course.HasValue && (tracks.TryGetValue((game, course.Value), out var label) ||
                game == "GT6" && tracks.TryGetValue(("GT5", course.Value), out label))) track = label;
            return new(game, room.Number(6).ToString(System.Globalization.CultureInfo.InvariantCulture), name,
                room.Child(12).Text(1), checked((int)room.Wrapped(10)), checked((int)room.Wrapped(8)),
                members, membersAvailable, track, course, (room.Number(14) & 0x80000000UL) == 0);
        }
    }
}
