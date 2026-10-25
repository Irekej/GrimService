using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace GrimService.Infrastructure.Security
{
    public static class AuthenticationRegistration
    {
        public static IServiceCollection AddGameAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(jwt =>
            {
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = GameSigningKey.Read(configuration),
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"],
                    ValidateLifetime = true,
                };
                jwt.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        context.Token = context.Request.Cookies["X-gt-token"];
                        return Task.CompletedTask;
                    },
                    OnAuthenticationFailed = context =>
                    {
                        context.HttpContext.RequestServices.GetService<ILoggerFactory>()?
                            .CreateLogger("GrimService.GameAuthentication")
                            .LogWarning("Game JWT rejected: path={Path}, reason={Reason}", context.Request.Path,
                                context.Exception.GetType().Name);
                        return Task.CompletedTask;
                    },
                    OnChallenge = context =>
                    {
                        var cookie = context.Request.Cookies["X-gt-token"];
                        context.HttpContext.RequestServices.GetService<ILoggerFactory>()?
                            .CreateLogger("GrimService.GameAuthentication")
                            .LogWarning("Game authentication challenge: path={Path}, cookiePresent={Present}, cookieLength={Length}",
                                context.Request.Path, !string.IsNullOrEmpty(cookie), cookie?.Length ?? 0);
                        return Task.CompletedTask;
                    },
                };
            });
            return services;
        }

        public static IServiceCollection AddAdminAuthentication(this IServiceCollection services)
        {
            services.AddAuthentication().AddCookie("AdminCookie", options =>
            {
                options.LoginPath = "/admin/login";
                options.AccessDeniedPath = "/admin/login";
                options.Cookie.Name = "GrimAdmin";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Events.OnValidatePrincipal = AdminPrincipalValidation.ValidateAsync;
            });
            return services;
        }
    }
}
