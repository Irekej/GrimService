using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GrimService.Features.Rankings
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddRankingFeatures(this IServiceCollection services)
        {
            services.TryAddTransient<IRankingRepository, DatabaseRankingRepository>();
            services.TryAddTransient<RankingService>();
            return services;
        }
    }
}
