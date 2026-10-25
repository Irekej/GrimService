using System.Threading.Tasks;
using GrimService.Controllers.Services;
using GrimService.Database.Controllers;
using GrimService.Models;
using GrimService.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GrimService.Infrastructure.Sessions;

public enum GameSessionStatus { Authenticated, MissingToken, InvalidToken }
public readonly record struct GameSessionReply(GameSessionStatus Status, Player Player = null);

public interface IGameRequestRecovery
{
    Task<bool> Recover(string token);
}

public sealed class DatabaseGameRequestRecovery(UserDBManager users, PlayerManager players) : IGameRequestRecovery
{
    public Task<bool> Recover(string token) => new SessionRecover(users, players).CheckIfPlayerExist(token);
}

public sealed class GameRequestSession(PlayerManager players, IGameRequestRecovery recovery)
{
    public async Task<GameSessionReply> Resolve(string token)
    {
        if (token == null) return new(GameSessionStatus.MissingToken);
        var player = players.GetPlayerByToken(token);
        if (player != null) return new(GameSessionStatus.Authenticated, player);
        if (string.IsNullOrWhiteSpace(token) || !await recovery.Recover(token))
            return new(GameSessionStatus.InvalidToken);
        player = players.GetPlayerByToken(token);
        return player == null ? new(GameSessionStatus.InvalidToken) : new(GameSessionStatus.Authenticated, player);
    }
}

public static class GameRequestSessionRegistration
{
    public static IServiceCollection AddGameRequestSessions(this IServiceCollection services)
    {
        services.TryAddTransient<IGameRequestRecovery, DatabaseGameRequestRecovery>();
        services.TryAddTransient<GameRequestSession>();
        return services;
    }
}
