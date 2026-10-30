using System;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using GrimService.Features.Bootstrap;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrimService.Controllers.Admin
{
    [Authorize(AuthenticationSchemes = "AdminCookie", Roles = "Administrator,Owner")]
    [Route("admin/server-settings")]
    public sealed class BootstrapAdminController : Controller
    {
        private readonly BootstrapSettingsService settings;
        public BootstrapAdminController(BootstrapSettingsService settings) => this.settings = settings;
        [HttpGet("")]
        public async Task<IActionResult> Index(string profile = "dev")
        {
            try { return View("~/Views/Admin/Bootstrap.cshtml", await settings.Load(profile)); }
            catch (ArgumentException) { return BadRequest(); }
        }
        [HttpPost("save")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(131072)]
        public async Task<IActionResult> Save(BootstrapSettingsModel model, bool reset = false)
        {
            try
            {
                if (!ModelState.IsValid) throw new ArgumentException("Nieprawidłowe dane formularza.");
                if (!await settings.Save(model, User.Identity?.Name, reset))
                {
                    Response.StatusCode = 409;
                    ViewData["Error"] = "Konfiguracja została zmieniona przez inną osobę. Wczytano aktualne wartości; sprawdź je przed ponownym zapisem.";
                    return View("~/Views/Admin/Bootstrap.cshtml", await settings.Load(model.Profile));
                }
                TempData["BootstrapSaved"] = true;
                return RedirectToAction(nameof(Index), new { profile = model.Profile });
            }
            catch (Exception error) when (error is ArgumentException or XmlException)
            {
                if (model.Profile != "regions" && !BootstrapDefaults.Profiles.Any(p => p.Id == model.Profile)) return BadRequest();
                var original = await settings.Load(model.Profile);
                foreach (var option in original.Options)
                    option.Value = model.Options?.FirstOrDefault(o => o.Key == option.Key)?.Value ?? option.Value;
                foreach (var region in original.Regions)
                {
                    var input = model.Regions?.FirstOrDefault(r => r.Key == region.Key);
                    if (input != null) { region.Name = input.Name; region.Enabled = input.Enabled; }
                }
                original.Revision = model.Revision;
                Response.StatusCode = 400;
                ViewData["Error"] = error is XmlException ? "Nieprawidłowa wartość opcji serwera." : error.Message;
                return View("~/Views/Admin/Bootstrap.cshtml", original);
            }
        }
        [HttpGet("preview")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Preview(string profile = "dev", bool https = false)
        {
            try
            {
                var model = await settings.Load(profile);
                return File(profile == "regions" ? BootstrapSettingsService.RenderRegionList(model)
                    : BootstrapSettingsService.RenderServerList(model, https), "application/xml; charset=utf-8");
            }
            catch (ArgumentException) { return BadRequest(); }
        }
    }
}
