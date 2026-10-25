using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GrimService.Features.Moderation
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddModerationFeatures(this IServiceCollection services)
        {
            services.TryAddTransient<IReportRepository, PostgresReportRepository>();
            services.TryAddTransient<GrimService.Services.ReportService>();
            return services;
        }
    }
}
