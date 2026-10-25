using Microsoft.AspNetCore.Server.Kestrel.Https;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;

namespace GrimService.Infrastructure.Http
{
    public static class GameHttps
    {
        public static void ConfigureEndpoint(ListenOptions listener, X509Certificate2 certificate, ILogger logger)
        {
            listener.Use(next => new Ssl3ConnectionMiddleware(next, certificate, logger).OnConnectionAsync);
            listener.UseHttps(certificate, Configure);
        }

        public static void Configure(HttpsConnectionAdapterOptions options)
        {


#pragma warning disable SYSLIB0039 // TLS 1.0 is required by the supported PS3 client.
            options.SslProtocols = SslProtocols.Tls | SslProtocols.Tls12 | SslProtocols.Tls13;
#pragma warning restore SYSLIB0039
        }
    }
}
