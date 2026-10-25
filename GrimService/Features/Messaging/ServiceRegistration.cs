using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GrimService.Features.Messaging
{
    public static class ServiceRegistration
    {
        public static IServiceCollection AddMessageRecipients(this IServiceCollection services)
        {
            services.TryAddTransient<IRecipientDirectory, RecipientDirectory>();
            return services;
        }

        public static IServiceCollection AddMessagingFeatures(this IServiceCollection services)
        {
            services.AddMessageRecipients();
            services.TryAddTransient<IGt5MailRepository, Gt5MailRepository>();
            services.TryAddTransient<IGt6MailRepository, Gt6MailRepository>();
            services.TryAddTransient<Gt5MailService>();
            services.TryAddTransient<Gt6MailService>();
            return services;
        }
    }
}
