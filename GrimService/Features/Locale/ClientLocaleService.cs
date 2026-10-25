using System;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GrimService.Features.Locale
{
    public enum LocaleStatus { Success, MissingLanguage, Unsupported }
    public readonly record struct LocaleReply(LocaleStatus Status, string Value = null);

    public sealed class ClientLocaleService
    {
        private readonly TimeProvider clock;
        public ClientLocaleService(TimeProvider clock) => this.clock = clock;

        public LocaleReply Execute(string command, string language)
        {
            return command switch {
                "servertime.get" => new(LocaleStatus.Success,
                    clock.GetLocalNow().ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture)),
                "language.set" when language != null => new(LocaleStatus.Success, language.ToLower(CultureInfo.CurrentCulture)),
                "language.set" => new(LocaleStatus.MissingLanguage),
                _ => new(LocaleStatus.Unsupported)
            };
        }
    }

    public static class ServiceRegistration
    {
        public static IServiceCollection AddClientLocale(this IServiceCollection services)
        {
            services.TryAddSingleton(TimeProvider.System);
            services.TryAddTransient<ClientLocaleService>();
            return services;
        }
    }
}
