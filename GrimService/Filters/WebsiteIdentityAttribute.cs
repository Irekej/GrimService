using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GrimService.Filters
{





    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class WebsiteIdentityAttribute : Attribute, IAsyncAuthorizationFilter, IOrderedFilter
    {

        public int Order => -1100;

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {


            context.HttpContext.Response.Headers["Cache-Control"] = "private, no-store";
            context.HttpContext.Response.Headers.Append("Vary", "Cookie, Accept-Language");
            var authentication = await context.HttpContext.AuthenticateAsync("AdminCookie");
            if (authentication.Succeeded && authentication.Principal != null)
                context.HttpContext.User = authentication.Principal;
        }
    }
}
