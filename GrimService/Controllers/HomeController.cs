using GrimService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace GrimService.Controllers
{
    [GrimService.Filters.WebsiteIdentity]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            _logger.LogDebug("API Request => Index");
            return View();
        }

        public IActionResult PlayerList()
        {
            _logger.LogDebug("API Request => PlayerList");
            return View();
        }

        [HttpGet("/seasonals")]
        public IActionResult Seasonals() => View();

        [HttpGet("/tutorial")]
        public IActionResult Tutorial(string game, string platform, string installation, string region,
            [FromServices] Microsoft.AspNetCore.Hosting.IWebHostEnvironment environment)
        {
            var model = new TutorialViewModel
            {
                Game = string.Equals(game, "GT5", StringComparison.OrdinalIgnoreCase) ? "GT5"
                    : string.Equals(game, "GT6", StringComparison.OrdinalIgnoreCase) ? "GT6" : null
            };
            if (model.Game != null)
            {
                model.Platform = string.Equals(platform, "RPCS3", StringComparison.OrdinalIgnoreCase) ? "RPCS3"
                    : string.Equals(platform, "PS3", StringComparison.OrdinalIgnoreCase) ? "PS3"
                    : string.Equals(platform, "PS3-CROSSPLAY", StringComparison.OrdinalIgnoreCase) ? "PS3-CROSSPLAY" : null;
            }
            if (model.Platform == "PS3" || model.Platform == "RPCS3")
            {
                model.Installation = model.Platform == "RPCS3" ? "manual"
                    : string.Equals(installation, "automatic", StringComparison.OrdinalIgnoreCase) ? "automatic"
                    : string.Equals(installation, "manual", StringComparison.OrdinalIgnoreCase) ? "manual" : null;
                if (model.Installation != null)
                    model.Region = GrimService.Services.TutorialAssets.NormalizeRegion(model.Game, region);
                var package = GrimService.Services.TutorialAssets.PackagePath(environment.ContentRootPath, model.Game, model.Region);
                model.PackageAvailable = package != null && System.IO.File.Exists(package);
                var manual = GrimService.Services.TutorialAssets.ManualPath(environment.ContentRootPath, model.Game, model.Region);
                model.ManualAvailable = manual != null && System.IO.File.Exists(manual);
            }
            else if (model.Platform == "PS3-CROSSPLAY")
            {
                foreach (var allowed in GrimService.Services.TutorialAssets.CrossplayRegions(model.Game))
                    if (string.Equals(allowed, region, StringComparison.OrdinalIgnoreCase)) model.Region = allowed;
                var crossplay = GrimService.Services.TutorialAssets.CrossplayPath(environment.ContentRootPath, model.Game, model.Region);
                model.CrossplayAvailable = crossplay != null && System.IO.File.Exists(crossplay);
            }
            return View(model);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
