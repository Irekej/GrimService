using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using GrimService.Features.Sessions;
using GrimService.Models;

namespace GrimService.Services;

public class PlayerManager : IDisposable
{
    private static readonly TimeSpan ActiveTimeout = TimeSpan.FromMinutes(5);
    private readonly PlayerSessionDirectory directory;
    private readonly ITimer maintenance;

    public ConcurrentDictionary<string, Player> Players { get => directory.Authenticated; set => directory.Authenticated = value; }
    public ConcurrentDictionary<string, Player> Players1234 { get => directory.Active; set => directory.Active = value; }

    public PlayerManager() : this(TimeProvider.System) { }
    public PlayerManager(TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        directory = new PlayerSessionDirectory(clock);
        maintenance = clock.CreateTimer(_ => directory.EvictInactive(ActiveTimeout), null, ActiveTimeout, ActiveTimeout);
    }

    public void UpdatePlayerActivity(Player player) => directory.Touch(player);
    public bool RemoveByToken(SessionToken token) => directory.Remove(token?.Token, false);
    public bool RemoveByTokenTemp(SessionToken token) => directory.Remove(token?.Token, true);
    public bool AddByToken(SessionToken token, Player player) => directory.Add(token?.Token, player, false);
    public bool AddByTokenTemp(SessionToken token, Player player) => directory.Add(token?.Token, player, true);
    public bool RemovePlayer(Player player) => directory.Remove(player?.Token?.Token, false);
    public bool AddUser(Player player) => directory.Add(player?.Token?.Token, player, false);
    public bool AddUserTemp(Player player) => directory.Add(player?.Token?.Token, player, true);
    public Player GetPlayerByToken(string token) => directory.Find(token);
    public void UpdatePlayerToken(Player player, SessionToken next) => directory.Rotate(player, next);
    public Player GetPlayerByName(string name) => directory.FindName(name);
    public Player GetPlayerByPlayer1234(Player player) => directory.FindActive(player);
    public List<Player> GetConnectedPlayers() => directory.Snapshot();
    public void CheckIfNotConnected(Player player) => directory.EnsureActive(player);
    public void Dispose() => maintenance.Dispose();
}
