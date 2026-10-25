using System.IO;
using GrimService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace GrimService.Controllers
{
    [AllowAnonymous]
    public sealed class TutorialDownloadsController : Controller
    {
        private readonly IWebHostEnvironment environment;
        public TutorialDownloadsController(IWebHostEnvironment environment) => this.environment = environment;

        [HttpGet("/tutorial/download/rpcn.yml")]
        public IActionResult RpcnTemplate()
        {
            var path = TutorialAssets.RpcnTemplatePath(environment.ContentRootPath);
            if (!System.IO.File.Exists(path)) return NotFound();
            return PhysicalFile(path, "application/yaml", "rpcn.yml");
        }

        [HttpGet("/tutorial/download/{game}/{region}")]
        public IActionResult Package(string game, string region)
        {
            var path = TutorialAssets.PackagePath(environment.ContentRootPath, game?.ToUpperInvariant(), region);
            if (path == null || !System.IO.File.Exists(path)) return NotFound();
            return PhysicalFile(path, "application/octet-stream", Path.GetFileName(path), enableRangeProcessing: true);
        }

        [HttpGet("/tutorial/download/{game}/{region}/manual")]
        public IActionResult Manual(string game, string region)
        {
            var path = TutorialAssets.ManualPath(environment.ContentRootPath, game?.ToUpperInvariant(), region);
            if (path == null || !System.IO.File.Exists(path)) return NotFound();
            return PhysicalFile(path, "application/zip", Path.GetFileName(path), enableRangeProcessing: true);
        }

        [HttpGet("/tutorial/download/{game}/{region}/crossplay")]
        public IActionResult Crossplay(string game, string region)
        {
            var path = TutorialAssets.CrossplayPath(environment.ContentRootPath, game?.ToUpperInvariant(), region);
            if (path == null || !System.IO.File.Exists(path)) return NotFound();
            return PhysicalFile(path, "application/zip", Path.GetFileName(path), enableRangeProcessing: true);
        }
    }
}
