using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.IO;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using System.IO;
using GrimService.Startup;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using GrimService.Services;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using System.Security.Authentication;
using GrimService.Infrastructure.Http;

namespace Grim
{
    public class Program
    {
        public static readonly RecyclableMemoryStreamManager StreamManager = new();

        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Logger(lc => lc.Filter.ByExcluding(logEvent => !IsGrimServiceLog(logEvent))
                .MinimumLevel.Override("Microsoft.AspNetCore.Server.Kestrel.Https", LogEventLevel.Warning)
                .WriteTo.Console()
                .WriteTo.File("Logs/log.txt"))
                .WriteTo.Conditional(hostConfig => HostConfig.LoggerDebug == "True", wt => wt.File(new JsonFormatter(),
                    "Logs/debug.txt",
                    restrictedToMinimumLevel: LogEventLevel.Verbose,
                    shared: true,
                    flushToDiskInterval: TimeSpan.FromDays(1),
                    rollOnFileSizeLimit: true,
                    fileSizeLimitBytes: 104857600,
                    retainedFileCountLimit: null))
                .CreateLogger();
            Console.WriteLine("///// GrimService Server [Build 26.10.2026] V. 0.9.6 /////");
            Console.WriteLine("Init: Starting...");
            CreateHostBuilder(args).Build().Run();
        }

        private static bool IsGrimServiceLog(LogEvent logEvent)
        {
            if (logEvent.Properties.TryGetValue("SourceContext", out var sourceContextValue))
            {
                var sourceContext = sourceContextValue.ToString();
                return sourceContext.Contains("GrimService") ||
                    sourceContext.Contains("Microsoft.AspNetCore.Server.Kestrel.Https.Internal.HttpsConnectionMiddleware");
            }
            return false;
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    HostConfig.CertificateFileLocation =
                        context.Configuration["CertificateFileLocation"];
                    HostConfig.CertificatePassword =
                        context.Configuration["CertPassword"];
                    HostConfig.LoggerDebug =
                        context.Configuration["LoggerDebug"];

                    Console.WriteLine("Init: SSL Config Loaded");
                })

                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddSerilog();
                })
                    .ConfigureWebHostDefaults(webBuilder =>
                    {
                        webBuilder.UseStartup<Startup>();
                        webBuilder.ConfigureKestrel(options =>
                        {
                            options.Limits.MaxRequestBodySize = 900_000;
                            options.Limits.MaxRequestBufferSize = 900_000;

                            options.ConfigureHttpsDefaults(GameHttps.Configure);

                            options.ListenAnyIP(443, listenOpt =>
                            {
                                var certificate = new X509Certificate2(HostConfig.CertificateFileLocation,
                                    HostConfig.CertificatePassword, X509KeyStorageFlags.Exportable);
                                GameHttps.ConfigureEndpoint(listenOpt, certificate,
                                    options.ApplicationServices.GetRequiredService<ILoggerFactory>().CreateLogger("GrimService.Infrastructure.Http.GameHttps"));
                            });
                            options.ListenAnyIP(80);
                            Console.WriteLine("Init: SSL Certificate Loaded (443)");
                            //Console.WriteLine("Init: HTTPS protocols: SSL 3.0 (GT5 compatibility), TLS 1.0, TLS 1.2, TLS 1.3");
                            Console.WriteLine("Init: HTTPS protocols: TLS 1.2, TLS 1.3");
                            //if (OperatingSystem.IsLinux() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENSSL_CONF")))
                                //Console.WriteLine("Init: TLS 1.0 also requires the OpenSSL policy; start with sh start-grimservice.sh");
                        });
                    })
                .UseSerilog();
    }

    public static class HostConfig
    {
        public static string CertificateFileLocation { get; set; }
        public static string CertificatePassword { get; set; }
        public static string LoggerDebug { get; set; }
    }
}
