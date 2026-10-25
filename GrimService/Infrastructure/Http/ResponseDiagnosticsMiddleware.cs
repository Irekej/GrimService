using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Connections.Features;

namespace GrimService.Infrastructure.Http
{
    public sealed class ResponseDiagnosticsMiddleware
    {
        private readonly RequestDelegate next;
        private readonly ILogger logger;

        public ResponseDiagnosticsMiddleware(RequestDelegate next, ILogger logger)
        {
            this.next = next;
            this.logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await next(context);

            if (context.Request.Path.StartsWithSegments("/init"))
            {
                var tls = context.Features.Get<ITlsHandshakeFeature>();
                logger.LogInformation("Game bootstrap response: path={Path}, host={Host}, scheme={Scheme}, httpProtocol={HttpProtocol}, tls={TlsProtocol}, http={Status}, contentLength={ContentLength}",
                    context.Request.Path, context.Request.Host.Value, context.Request.Scheme,
                    context.Request.Protocol, tls?.Protocol, context.Response.StatusCode, context.Response.ContentLength);
            }

            if (context.Request.Path.StartsWithSegments("/ap/ticket/login"))
            {
                logger.LogDebug("Game login HTTP response: path={Path}, http={Status}, contentType={ContentType}, contentLength={ContentLength}, setCookieCount={CookieCount}",
                    context.Request.Path, context.Response.StatusCode, context.Response.ContentType,
                    context.Response.ContentLength, context.Response.Headers.SetCookie.Count);
            }

            switch (context.Response.StatusCode)
            {
                case StatusCodes.Status404NotFound:
                    logger.LogDebug("[:] 404 -> IP: {RemoteIp} -> {RequestPath}",
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", context.Request.Path);
                    break;
                case StatusCodes.Status500InternalServerError:
                    logger.LogDebug("[:] Internal Server Error (500): {RequestPath}", context.Request.Path);
                    break;
                case StatusCodes.Status400BadRequest:
                    logger.LogDebug("[:] Bad Request (400): {RequestPath}", context.Request.Path);
                    break;
            }
        }
    }
}
