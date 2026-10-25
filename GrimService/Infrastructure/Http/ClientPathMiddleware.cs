using System.Threading.Tasks;
using GrimService.Utils;
using Microsoft.AspNetCore.Http;

namespace GrimService.Infrastructure.Http
{
    public sealed class ClientPathMiddleware
    {
        private readonly RequestDelegate next;

        public ClientPathMiddleware(RequestDelegate next) => this.next = next;

        public Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value;
            var normalized = InitPathNormalizer.Normalize(path);
            if (path != normalized)
                context.Request.Path = normalized;

            return next(context);
        }
    }
}
