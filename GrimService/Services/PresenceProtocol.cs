using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Runtime.CompilerServices;
using GrimService.Models;
using GrimService.Models.Xml;

namespace GrimService.Services
{
    public sealed class PresenceSnapshot
    {
        public string Game { get; }
        public string Stats { get; }
        public DateTime UpdatedUtc { get; }
        public PresenceSnapshot(string game, string stats, DateTime updatedUtc)
        { Game = game; Stats = stats; UpdatedUtc = updatedUtc; }
    }

    public static class PresenceProtocol
    {

        private sealed class State { public volatile PresenceSnapshot Value; }
        private static readonly ConditionalWeakTable<Player, State> States = new();

        public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

        public static bool Set(Player player, string stats, DateTime nowUtc)
        {
            if (player?.Token == null || player.Token.ExpiryDate.ToUniversalTime() <= nowUtc ||
                (player.Data.LastGame != "GT5" && player.Data.LastGame != "GT6") ||
                stats == null || stats.Length > 256) return false;
            try { XmlConvert.VerifyXmlChars(stats); }
            catch (XmlException) { return false; }
            States.GetOrCreateValue(player).Value = new PresenceSnapshot(player.Data.LastGame, stats, nowUtc);
            return true;
        }

        public static PresenceList Read(IEnumerable<Player> sessions, string game,
            Func<Player, bool> include, DateTime nowUtc)
        {

            var rows = sessions.Where(p => p?.Data != null && p.Token != null &&
                    p.Token.ExpiryDate.ToUniversalTime() > nowUtc && p.Data.LastGame == game && include(p))
                .Select(p => new { Name = p.Data.PSNUserId, Value = States.TryGetValue(p, out var state) ? state.Value : null })
                .Where(p => p.Value != null && p.Value.Game == game &&
                    p.Value.UpdatedUtc <= nowUtc && nowUtc - p.Value.UpdatedUtc < Lifetime)
                .GroupBy(p => p.Name, StringComparer.Ordinal)
                .Select(g => g.OrderByDescending(p => p.Value.UpdatedUtc).First())
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => new Presence { Id = p.Name, Stats = p.Value.Stats, Type = 0,
                    UpdateTime = p.Value.UpdatedUtc }).ToList();
            return new PresenceList { Presences = rows };
        }
    }
}
