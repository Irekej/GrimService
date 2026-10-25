using System;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GrimService.Infrastructure.Database
{
    public sealed class DatabaseInitializer
    {
        private readonly IServiceProvider services;
        private readonly IConfiguration configuration;

        public DatabaseInitializer(IServiceProvider services, IConfiguration configuration)
        {
            this.services = services ?? throw new ArgumentNullException(nameof(services));
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public void Initialize() => Initialize(Thread.Sleep, Console.WriteLine);

        internal void Initialize(Action<TimeSpan> delay, Action<string> write)
        {
            Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    using (var scope = services.CreateScope())
                        DatabaseSchema.Initialize(scope.ServiceProvider, configuration);
                }
                catch (Exception exception)
                {
                    string reason = exception is DatabaseSchemaException
                        ? exception.Message
                        : "Database initialization scope failed.";
                    if (attempt == 3)
                        throw new InvalidOperationException("Unable to initialize database after 3 attempts. " + reason, exception);

                    write($"Could not initialize database, trying {3 - attempt} more times... {reason}");
                    delay(TimeSpan.FromSeconds(5));
                    continue;
                }

                write("Init: Database Tables Initialized");
                return;
            }
        }
    }
}
