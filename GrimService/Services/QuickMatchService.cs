using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using GrimService.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrimService.Services
{

    public sealed class QuickMatchService
    {
        private readonly object _gate = new();
        private readonly Dictionary<int, Entry> _entries = new();
        private readonly Dictionary<long, Game> _games = new();
        private readonly PlayerManager _players;
        private readonly TimeProvider _clock;
        private const int JoinHandshakeSeconds = 30;


        private const int JoinRetentionSeconds = 150;
        private readonly ILogger<QuickMatchService> _logger;
        private long _nextGame = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        private DateTimeOffset Now => _clock.GetUtcNow();
        private sealed class Entry
        {
            public Player Player;
            public ulong Event;
            public int Max, Nat, Send, Receive, Wait, Timeout, SettleSeconds;
            public string Pdi;
            public DateTimeOffset Entered, Seen, Assigned;
            public DateTimeOffset? JoinOffered;
            public DateTimeOffset? MissingSince;
            public bool Confirmed;
            public long Game;
        }
        private sealed class Game
        {
            public long Id;
            public ulong Event, Room;
            public int Host, Max;
            public bool Joinable = true, Started;
            public DateTimeOffset Seen;
            public Guid Round = Guid.NewGuid();
            public int Starters;
        }
        public QuickMatchService(PlayerManager players, TimeProvider clock, ILogger<QuickMatchService> logger = null)
        { _players = players; _clock = clock; _logger = logger ?? NullLogger<QuickMatchService>.Instance; }
        private bool Connecting(Entry e) => e.JoinOffered.HasValue &&
            Now - e.JoinOffered.Value < TimeSpan.FromSeconds(JoinRetentionSeconds);
        private bool SessionValid(Entry e) => e.Player.Token != null &&
            e.Player.Token.ExpiryDate.ToUniversalTime() > Now.UtcDateTime && e.Player.Data.LastGame == "GT6" &&
            ReferenceEquals(_players.GetPlayerByToken(e.Player.Token.Token), e.Player);
        private Entry Find(Player p, ulong eventId) => _entries.TryGetValue(p.Data.Id, out var e) &&
            ReferenceEquals(e.Player, p) && e.Event == eventId ? e : null;
        private List<Entry> Members(Game g) => _entries.Values.Where(e => e.Game == g.Id)
            .OrderBy(e => e.Player.Data.Id == g.Host ? 0 : 1).ThenBy(e => e.Entered).ThenBy(e => e.Player.Data.Id).ToList();
        private Game Owned(Entry e, ulong? room = null) => e != null && _games.TryGetValue(e.Game, out var g) &&
            g.Host == e.Player.Data.Id && (!room.HasValue || g.Room == room.Value) ? g : null;
        private bool Reserve(Entry e, Game g)
        {
            if (e.Game != 0 || e.Event != g.Event || !g.Joinable || g.Started ||
                Now - e.Entered >= TimeSpan.FromSeconds(e.Timeout) ||
                Members(g).Count >= Math.Min(g.Max, e.Max)) return false;
            e.Game = g.Id; e.Assigned = Now; g.Max = Math.Min(g.Max, e.Max);
            _logger.LogDebug("Quick Match reserved: user={User} host={Host} game={Game} room={Room} users={Users}",
                e.Player.Data.PSNUserId, g.Host, g.Id, g.Room, Members(g).Count);
            return true;
        }
        private bool ReserveAvailable(Entry e)
        {
            foreach (var g in _games.Values.Where(g => g.Event == e.Event)
                .OrderByDescending(g => g.Room != 0).ThenBy(g => g.Id))
                if (Reserve(e, g)) return true;
            return false;
        }
        private void Dissolve(Game g)
        {
            _games.Remove(g.Id);
            foreach (var e in _entries.Values.Where(e => e.Game == g.Id))
            { e.Game = 0; e.Entered = Now; e.JoinOffered = null; e.Confirmed = false; e.MissingSince = null; }
        }
        private void Remove(Entry e, string reason = "leave-or-search-ended")
        {
            _games.TryGetValue(e.Game, out var current);
            _logger.LogDebug("Quick Match removed: user={User} event={Event} game={Game} room={Room} confirmed={Confirmed} reason={Reason}",
                e.Player.Data.PSNUserId, e.Event, e.Game, current?.Room, e.Confirmed, reason);
            if (_games.TryGetValue(e.Game, out var g) && g.Host == e.Player.Data.Id) Dissolve(g);
            _entries.Remove(e.Player.Data.Id);
        }
        private void Prune()
        {
            foreach (var e in _entries.Values.ToArray())
                if (!SessionValid(e)) Remove(e, "session-invalid");
                else if (!e.Confirmed && e.JoinOffered.HasValue && !Connecting(e)) Remove(e, "join-retention-expired");
                else if (!Connecting(e) && Now - e.Seen > TimeSpan.FromSeconds(e.Game == 0 ? Math.Max(30, e.Timeout) : 120)) Remove(e, "presence-expired");
            foreach (var g in _games.Values.ToArray())
                if (Now - g.Seen > TimeSpan.FromSeconds(g.Room == 0 ? 30 : 90))
                {
                    _entries.TryGetValue(g.Host, out var host);
                    Dissolve(g);
                    if (host != null) _entries.Remove(host.Player.Data.Id);
                }
        }
        private string Reply(Entry e, string state, int result = 1, bool advertiseRoom = true)
        {
            Game g = null;
            if (e != null) _games.TryGetValue(e.Game, out g);
            var members = g == null ? new List<Entry>() : Members(g);
            return new XElement("matching2", new XElement("state", state), new XElement("result", result),
                new XElement("room_id", advertiseRoom ? g?.Room ?? 0 : 0), new XElement("grim_game_id", g?.Id ?? 0),


                new XElement("remaining_time", e == null ? 0 : Math.Max(0,
                    e.JoinOffered.HasValue ? JoinHandshakeSeconds - (int)(Now - e.JoinOffered.Value).TotalSeconds :
                    e.Timeout - (int)(Now - e.Entered).TotalSeconds)),
                new XElement("polling_time", 2), new XElement("network_topology", 1),
                new XElement("vehicle_packet_interval", 100),
                new XElement("user_list", members.Select((m, index) => new XElement("user",
                    new XAttribute("user_id", m.Player.Data.PSNUserId), new XAttribute("user_no", m.Player.Data.Id),
                    new XAttribute("pdi_user_id", m.Pdi), new XAttribute("eligible_migration", m.Nat == 3 ? 0 : 1),
                    new XAttribute("grid_order", index))))).ToString(SaveOptions.DisableFormatting);
        }
        public string Enter(Player p, ulong eventId, int max, int nat, int send, int receive, string pdi, int wait, int timeout, int settleSeconds = 0)
        {
            lock (_gate)
            {
                Prune();
                var e = Find(p, eventId);
                if (e != null) { e.Seen = Now; ReserveAvailable(e); return Reply(e, "matching"); }
                if (_entries.TryGetValue(p.Data.Id, out var old)) Remove(old);
                e = new Entry
                {
                    Player = p,
                    Event = eventId,
                    Max = max,
                    Nat = nat,
                    Send = send,
                    Receive = receive,
                    Pdi = pdi,
                    Wait = wait,
                    SettleSeconds = Math.Clamp(settleSeconds, 0, wait),
                    Timeout = timeout,
                    Entered = Now,
                    Seen = Now
                };
                _entries.Add(p.Data.Id, e);


                ReserveAvailable(e);
                return Reply(e, "matching");
            }
        }
        public string Join(Player p, ulong eventId)
        {
            lock (_gate)
            {
                Prune(); var e = Find(p, eventId);
                if (e == null) return Reply(null, "nohost", -1);
                e.Seen = Now;
                var isHost = _games.TryGetValue(e.Game, out var currentGame) && currentGame.Host == p.Data.Id;
                if (!isHost && !e.Confirmed && !e.JoinOffered.HasValue && Now - e.Entered >= TimeSpan.FromSeconds(e.Timeout))
                { Remove(e); return Reply(null, "nohost", -1); }
                if (e.Game == 0)
                {


                    if (!ReserveAvailable(e))
                    {
                        foreach (var candidate in _games.Values.Where(g => g.Event == eventId))
                            _logger.LogDebug("Quick Match candidate unavailable: user={User} game={Game} room={Room} joinable={Joinable} started={Started} users={Users} max={Max}",
                                p.Data.PSNUserId, candidate.Id, candidate.Room, candidate.Joinable, candidate.Started, Members(candidate).Count, Math.Min(candidate.Max, e.Max));
                        var waiting = _entries.Values.Where(x => x.Event == eventId && x.Game == 0 &&
                            Now - x.Entered < TimeSpan.FromSeconds(x.Timeout)).OrderBy(x => x.Entered).ThenBy(x => x.Player.Data.Id).ToList();
                        var host = waiting.Where(x => x.Nat != 3).OrderBy(x => x.Nat == 1 ? 0 : 1).ThenByDescending(x => x.Send).ThenBy(x => x.Entered).ThenBy(x => x.Player.Data.Id).FirstOrDefault();




                        var first = waiting.FirstOrDefault();
                        var age = first == null ? 0 : (Now - first.Entered).TotalSeconds;
                        var quiet = first == null ? 0 : (Now - waiting.Max(x => x.Entered)).TotalSeconds;
                        bool collected = first != null &&
                            (age >= first.Wait ||
                             (waiting.Count >= 2 && first.SettleSeconds > 0 &&
                              age >= first.Wait - first.SettleSeconds && quiet >= first.SettleSeconds));
                        _logger.LogDebug("Quick Match collecting: event={Event} users={Users} elapsed={Elapsed} quiet={Quiet} ready={Ready}",
                            eventId, waiting.Count, (int)age, (int)quiet, collected);
                        if (host != null && collected)
                        {
                            var g = new Game { Id = ++_nextGame, Event = eventId, Host = host.Player.Data.Id, Max = host.Max, Seen = Now };
                            _games.Add(g.Id, g); host.Game = g.Id; host.Assigned = Now;
                            int count = 1;
                            foreach (var other in waiting.Where(x => x != host))
                            {
                                if (count >= Math.Min(g.Max, other.Max)) continue;
                                other.Game = g.Id; other.Assigned = Now; g.Max = Math.Min(g.Max, other.Max); count++;
                            }
                            _logger.LogDebug("Quick Match group ready: event={Event} game={Game} host={Host} users={Users} collectedSeconds={Elapsed}",
                                eventId, g.Id, host.Player.Data.PSNUserId, count, (int)age);
                        }
                    }
                }
                if (e.Game != 0 && _games.TryGetValue(e.Game, out var game))
                {
                    if (game.Host == p.Data.Id) return Reply(e, "create");




                    if (game.Room == 0 || (game.Started && !e.Confirmed && !e.JoinOffered.HasValue))
                    {
                        if (Now - e.Entered >= TimeSpan.FromSeconds(e.Timeout))
                        { Remove(e); return Reply(null, "nohost", -1); }
                        _logger.LogDebug("Quick Match waiting: user={User} game={Game} actualRoom={Room} joinable={Joinable} started={Started} reason={Reason}",
                            p.Data.PSNUserId, game.Id, game.Room, game.Joinable, game.Started,
                            game.Room == 0 ? "room-unpublished" : "race-started");
                        return Reply(e, "matching", advertiseRoom: false);
                    }
                    if (!e.JoinOffered.HasValue)
                        _logger.LogDebug("Quick Match room offered: user={User} game={Game} room={Room} joinable={Joinable} waitSeconds={Wait}",
                            p.Data.PSNUserId, game.Id, game.Room, game.Joinable, (Now - e.Entered).TotalSeconds);
                    e.JoinOffered ??= Now;
                    return Reply(e, "join");
                }
                if (Now - e.Entered >= TimeSpan.FromSeconds(e.Timeout)) { Remove(e); return Reply(null, "nohost", -1); }
                return Reply(e, "matching");
            }
        }
        public string Create(Player p, ulong eventId, ulong room)
        {
            lock (_gate)
            {
                Prune(); var e = Find(p, eventId); var g = Owned(e);
                if (g == null || (g.Room != 0 && g.Room != room) || _games.Values.Any(x => x.Id != g.Id && x.Room == room)) return Reply(null, "nohost", -1);
                if (g.Room == 0)
                    _logger.LogDebug("Quick Match room published: host={Host} game={Game} room={Room} users={Users}",
                        p.Data.PSNUserId, g.Id, room, Members(g).Count);
                g.Room = room; g.Seen = Now; e.Seen = Now;
                return Reply(e, "create");
            }
        }
        public bool Start(Player p, ulong eventId)
        {
            lock (_gate)
            {
                Prune(); var e = Find(p, eventId); var g = Owned(e);
                if (g == null || g.Room == 0) return false;
                if (!g.Started) g.Starters = Members(g).Count;
                g.Started = true; g.Joinable = false; g.Seen = Now; e.Seen = Now; return true;
            }
        }
        public string SetJoinable(Player p, ulong eventId, ulong room, bool joinable)
        {
            lock (_gate)
            {
                Prune(); var e = Find(p, eventId); var g = Owned(e, room);
                if (g == null || room == 0) return Reply(null, "nohost", -1);
                g.Joinable = joinable;
                if (joinable && g.Started) { g.Started = false; g.Round = Guid.NewGuid(); g.Starters = 0; }



                if (joinable)
                    foreach (var waiting in _entries.Values.Where(x => x.Game == 0 && x.Event == eventId)
                        .OrderBy(x => x.Entered).ThenBy(x => x.Player.Data.Id))
                        Reserve(waiting, g);
                g.Seen = Now; e.Seen = Now;
                return Reply(e, "create");
            }
        }
        public bool Heartbeat(Player p, ulong eventId, ulong room, string[] users)
        {
            lock (_gate)
            {


                var e = Find(p, eventId); var g = Owned(e, room);
                if (g == null || room == 0 || !SessionValid(e) || Now - g.Seen > TimeSpan.FromSeconds(90))
                { Prune(); return false; }
                var members = Members(g);
                g.Seen = Now; e.Seen = Now;
                foreach (var m in members.Where(m => m != e))
                    if (SessionValid(m) && users.Contains(m.Player.Data.PSNUserId, StringComparer.Ordinal))
                    {
                        if (!m.Confirmed || m.MissingSince.HasValue)
                            _logger.LogDebug("Quick Match host confirmed: host={Host} user={User} room={Room} joinAgeSeconds={Age}",
                                p.Data.PSNUserId, m.Player.Data.PSNUserId, room, m.JoinOffered.HasValue ? (Now - m.JoinOffered.Value).TotalSeconds : 0);
                        m.Seen = Now; m.Confirmed = true; m.MissingSince = null;
                    }
                    else if (m.Confirmed)
                    {


                        m.MissingSince ??= Now;
                        if (!Connecting(m) && Now - m.MissingSince.Value >= TimeSpan.FromSeconds(30)) Remove(m, "missing-from-host");
                    }


                Prune();
                return true;
            }
        }
        public bool Leave(Player p, ulong eventId, ulong room) => Cancel(p, eventId, room);



        public bool Cancel(Player p, ulong? eventId, ulong? room)
        {
            lock (_gate)
            {
                Prune();
                if (!_entries.TryGetValue(p.Data.Id, out var e) || !ReferenceEquals(e.Player, p)) return true;
                if (eventId.HasValue && eventId.Value != 0 && e.Event != eventId.Value) return false;
                if (_games.TryGetValue(e.Game, out var g))
                {
                    if (room.HasValue && room.Value != 0 && g.Room != room.Value) return false;



                    if (g.Host == p.Data.Id && g.Room != 0 && (!room.HasValue || room.Value == 0) &&
                        (!eventId.HasValue || eventId.Value == 0)) return false;
                }
                else if (room.HasValue && room.Value != 0) return false;
                Remove(e); return true;
            }
        }
        public (Guid Round, ulong Event, int Entries)? StartedRound(Player player, ulong? eventId = null)
        {
            lock (_gate)
            {
                Prune();
                if (!_entries.TryGetValue(player.Data.Id, out var e) || !ReferenceEquals(e.Player, player) ||
                    (eventId.HasValue && e.Event != eventId.Value)) return null;
                var g = Owned(e);
                if (g == null || !g.Started) return null;
                return (g.Round, (g.Event >> 32) & 0xFFFFFF, g.Starters);
            }
        }
        public string LobbyStatus()
        {
            lock (_gate)
            {
                Prune();
                return new XElement("matching2", _entries.Values.GroupBy(e => e.Event).OrderBy(g => g.Key).Select(group =>
                    new XElement("room_list", new XAttribute("event_id", group.Key),
                        new XAttribute("room", _games.Values.Count(g => g.Event == group.Key && g.Room != 0)),
                        new XAttribute("user", group.Count()),
                        new XAttribute("room_all", _games.Values.Count(g => g.Event == group.Key)),
                        new XAttribute("user_all", group.Count())))).ToString(SaveOptions.DisableFormatting);
            }
        }
    }
}
