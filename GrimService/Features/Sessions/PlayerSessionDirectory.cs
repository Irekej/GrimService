using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using GrimService.Models;

namespace GrimService.Features.Sessions;

public sealed class PlayerSessionDirectory(TimeProvider clock)
{
    private readonly object gate = new();
    public ConcurrentDictionary<string, Player> Authenticated { get; set; } = new();
    public ConcurrentDictionary<string, Player> Active { get; set; } = new();
    private DateTime Now => DateTime.SpecifyKind(clock.GetLocalNow().DateTime, DateTimeKind.Local);
    private static bool HasKey(string key) => !string.IsNullOrWhiteSpace(key);

    public Player Find(string key) => HasKey(key) && Authenticated.TryGetValue(key, out var player) ? player : null;
    public Player FindName(string name) => name == null ? null : Authenticated.Values.FirstOrDefault(player =>
        player != null && string.Equals(player.Data.PSNUserId, name, StringComparison.Ordinal));
    public Player FindActive(Player player) => player == null ? null : Active.Values.FirstOrDefault(value => ReferenceEquals(value, player));
    public List<Player> Snapshot() => Active.Values.ToList();

    public bool Add(string key, Player player, bool active)
    {
        if (!HasKey(key) || player == null) return false;
        lock (gate) return (active ? Active : Authenticated).TryAdd(key, player);
    }
    public bool Remove(string key, bool active)
    {
        if (!HasKey(key)) return false;
        lock (gate) return (active ? Active : Authenticated).TryRemove(key, out _);
    }
    public void Touch(Player player)
    {
        if (player == null) return;
        lock (gate) player.LastUpdate = Now;
    }
    public void EnsureActive(Player player)
    {
        if (player?.Token == null) return;
        lock (gate)
            if (FindActive(player) == null) Add(player.Token.Token, player, true);
    }

    public bool Rotate(Player player, SessionToken next)
    {
        if (player?.Token == null || next == null || !HasKey(next.Token)) return false;
        lock (gate)
        {
            string previous = player.Token.Token;
            if (!HasKey(previous) || !Authenticated.TryGetValue(previous, out var owner) || !ReferenceEquals(owner, player)) return false;
            bool tracked = Active.TryGetValue(previous, out var active) && ReferenceEquals(active, player);
            if (next.Token != previous && tracked && Active.TryGetValue(next.Token, out var occupied) && !ReferenceEquals(occupied, player)) return false;
            if (next.Token != previous && !Authenticated.TryAdd(next.Token, player)) return false;
            player.Token = next;
            if (next.Token != previous)
            {
                RemoveExact(Authenticated, previous, player);
                if (tracked)
                {
                    Active.TryAdd(next.Token, player);
                    RemoveExact(Active, previous, player);
                }
            }
            return true;
        }
    }

    public int EvictInactive(TimeSpan idle)
    {
        if (idle < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(idle));
        lock (gate)
        {
            int removed = 0;
            var cutoff = Now - idle;
            foreach (var pair in Active.ToArray())
                if ((pair.Value == null || pair.Value.LastUpdate <= cutoff) && RemoveExact(Active, pair.Key, pair.Value)) removed++;
            return removed;
        }
    }

    private static bool RemoveExact(ConcurrentDictionary<string, Player> map, string key, Player value)
        => ((ICollection<KeyValuePair<string, Player>>)map).Remove(new(key, value));
}
