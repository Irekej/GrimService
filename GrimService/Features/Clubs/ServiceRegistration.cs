using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GrimService.Features.Clubs
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddClubFeatures(this IServiceCollection services)
        {
            services.TryAddTransient<IClubAccessRepository, ClubAccessRepository>();
            services.TryAddTransient<ClubAccessService>();
            services.TryAddTransient<IClubOperationsRepository, ClubOperationsRepository>();
            services.TryAddTransient<ClubOperationsService>();
            services.TryAddTransient<IClubMembershipRepository, ClubMembershipRepository>();
            services.TryAddTransient<ClubMembershipService>();
            services.TryAddTransient<IClubFavoritesRepository, ClubFavoritesRepository>();
            services.TryAddTransient<ClubFavoritesService>();
            services.TryAddTransient<IClubReadRepository, ClubReadRepository>();
            services.TryAddTransient<ClubReadService>();
            services.TryAddTransient<IClubWriteRepository, ClubWriteRepository>();
            services.TryAddTransient<ClubWriteService>();
            services.TryAddTransient<IClubEventWriteRepository, ClubEventWriteRepository>();
            services.TryAddTransient<ClubEventWriteService>();
            services.TryAddTransient<IClubEventActionsRepository, ClubEventActionsRepository>();
            services.TryAddTransient<ClubEventActionsService>();
            services.TryAddTransient<IClubEventReadRepository, ClubEventReadRepository>();
            services.TryAddTransient<ClubEventReadService>();
            services.TryAddTransient<IClubTemplateRepository, ClubTemplateRepository>();
            services.TryAddTransient<ClubTemplateService>();
            return services;
        }
    }
}
