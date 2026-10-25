using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GrimService.Features.ClubPhotos
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddClubPhotoFeatures(this IServiceCollection services)
        {
            services.TryAddTransient<IClubPhotoRepository, ClubPhotoRepository>();
            services.TryAddTransient<ClubPhotoService>();
            return services;
        }
    }
}
