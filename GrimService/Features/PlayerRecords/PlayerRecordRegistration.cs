using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace GrimService.Features.PlayerRecords;

public static class PlayerRecordRegistration
{
    public static IServiceCollection AddPlayerRecords(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddTransient<IPlayerBoardRepository, PlayerBoardRepository>();
        services.TryAddTransient<IActionHistoryRepository, ActionHistoryRepository>();
        services.TryAddTransient<PlayerBoardService>();
        services.TryAddTransient<ActionHistoryService>(provider => new ActionHistoryService(
            provider.GetRequiredService<IActionHistoryRepository>(),
            provider.GetRequiredService<ILogger<ActionHistoryService>>(),
            provider.GetRequiredService<TimeProvider>()));
        return services;
    }
}
