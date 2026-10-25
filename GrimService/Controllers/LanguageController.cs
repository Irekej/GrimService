using System;
using GrimService.Services;
using GrimService.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GrimService.Controllers
{
    [WebsiteIdentity]
    [Route("language")]
    public sealed class LanguageController : Controller
    {
        [HttpPost("set")]
        [ValidateAntiForgeryToken]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public IActionResult Set([FromForm] string language, [FromForm] string returnUrl)
        {
            if (!WebText.Supported(language)) return BadRequest();
            Response.Cookies.Append(WebText.CookieName, language, new CookieOptions {




                HttpOnly = true, Secure = false, SameSite = SameSiteMode.Lax,
                Path = "/", MaxAge = TimeSpan.FromDays(365), IsEssential = true
            });
            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
        }
    }
}
