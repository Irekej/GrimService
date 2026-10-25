using GrimService.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace GrimService.Features.Replays
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddReplayFeatures(this IServiceCollection services)
        {
            services.TryAddTransient<IReplayRepository, DatabaseReplayRepository>();
            services.TryAddTransient<ReplayTransferService>(provider => new ReplayTransferService(
                provider.GetRequiredService<IReplayRepository>(),
                provider.GetRequiredService<IOptions<GameServerOptions>>().Value.XmlResourcePath));
            return services;
        }
    }
}
