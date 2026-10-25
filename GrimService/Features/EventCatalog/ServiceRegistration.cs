using GrimService.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace GrimService.Features.EventCatalog
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddEventCatalogFeatures(this IServiceCollection services)
        {
            services.TryAddSingleton<IEventCatalog>(provider => new FileEventCatalog(
                provider.GetRequiredService<IOptions<GameServerOptions>>().Value.XmlResourcePath));
            return services;
        }
    }
}
