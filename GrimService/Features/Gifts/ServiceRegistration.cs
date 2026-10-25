using GrimService.Features.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GrimService.Features.Gifts
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddGiftFeatures(this IServiceCollection services)
        {
            services.AddMessageRecipients();
            services.TryAddTransient<IGt5GiftRepository, Gt5GiftRepository>();
            services.TryAddTransient<Gt5GiftService>();
            return services;
        }
    }
}
