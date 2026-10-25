using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace GrimService.Infrastructure.Http
{
    public static class RequestPipeline
    {
        public static IApplicationBuilder UseClientAssets(this IApplicationBuilder app)
        {
            app.UseMiddleware<ClientPathMiddleware>();
            app.UseDefaultFiles();
            app.UseStaticFiles();
            return app;
        }

        public static IApplicationBuilder UseGameEndpoints(this IApplicationBuilder app, ILogger logger)
        {
            app.Use(next => new ResponseDiagnosticsMiddleware(next, logger).InvokeAsync);
            app.UseAuthentication();
            app.UseRouting();
            app.UseRateLimiter();
            app.UseWebSockets();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints.MapControllerRoute(
                name: "default", pattern: "{controller=Home}/{action=Index}/{id?}"));
            return app;
        }
    }
}
