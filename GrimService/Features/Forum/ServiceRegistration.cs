using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GrimService.Features.Forum
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddForumFeatures(this IServiceCollection services)
        {
            services.TryAddTransient<IClubForumRepository, ClubForumRepository>();
            services.TryAddTransient<ClubForumService>();
            return services;
        }
    }
}
