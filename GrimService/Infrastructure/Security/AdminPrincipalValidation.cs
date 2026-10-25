using System.Security.Claims;
using System.Threading.Tasks;
using GrimService.Database.Controllers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;

namespace GrimService.Infrastructure.Security
{
    internal static class AdminPrincipalValidation
    {
        public static async Task ValidateAsync(CookieValidatePrincipalContext context)
        {
            var username = context.Principal?.Identity?.Name;
            if (string.IsNullOrEmpty(username))
            {
                context.RejectPrincipal();
                return;
            }

            var database = context.HttpContext.RequestServices.GetRequiredService<AdminUserDBManager>();
            var account = await database.FindAsync(username);
            if (account == null)
            {
                context.RejectPrincipal();
                return;
            }

            var identity = new ClaimsIdentity("AdminCookie");
            identity.AddClaim(new Claim(ClaimTypes.Name, account.Username));
            identity.AddClaim(new Claim(ClaimTypes.Role, account.IsAdmin ? "Administrator" : "EventEditor"));
            context.ReplacePrincipal(new ClaimsPrincipal(identity));
        }
    }
}
