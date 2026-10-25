using System;
using System.IO;
using System.Xml;
using GrimService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GrimService.Controllers.Admin
{
    [Authorize(AuthenticationSchemes = "AdminCookie", Roles = "Administrator,EventEditor")]
    [Route("admin/events")]
    public sealed class SeasonalController : Controller
    {
        private readonly SeasonalEventService events;
        private readonly ILogger<SeasonalController> logger;
        public SeasonalController(SeasonalEventService events, ILogger<SeasonalController> logger)
        { this.events = events; this.logger = logger; }
        [HttpGet("")]
        public IActionResult Index() => View("~/Views/Admin/Seasonal.cshtml");
        [HttpGet("servers")]
        public IActionResult Servers() => Json(events.Servers());
        private IActionResult Execute(Func<object> action)
        {
            try { return Json(action()); }
            catch (InvalidOperationException e) { return Conflict(new { error = e.Message }); }
            catch (Exception e) when (e is ArgumentException || e is XmlException || e is IOException || e is UnauthorizedAccessException)
            {
                logger.LogWarning(e, "Seasonal editor operation failed for {User}", User.Identity.Name);
                return BadRequest(new { error = e is IOException || e is UnauthorizedAccessException ? "Nie można odczytać/zapisać zasobów. Sprawdź uprawnienia i log serwera." : e.Message });
            }
        }
        [HttpGet("list")]
        public IActionResult List(string server, string game) => Execute(() => events.List(server, game));
        [HttpGet("load")]
        public IActionResult Load(string server, string game, int folder) => Execute(() => events.Load(server, game, folder));
        [HttpGet("catalog")]
        public IActionResult Catalog(string server, string game) => Execute(() => events.Catalog(server, game));
        [HttpGet("template")]
        public IActionResult Template(string server, string game, int folder) => Execute(() => events.Clone(server, game, folder));
        [HttpGet("create")]
        public IActionResult Create(string server, string game, string category, string kind) => Execute(() => events.Create(server, game, category, kind));
        [HttpPost("validate")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public IActionResult Validate([FromBody] SeasonalDocument document) => Execute(() => events.Normalize(document ?? throw new ArgumentException("Brak danych.")));
        [HttpPost("publish")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public IActionResult Publish([FromBody] SeasonalDocument document) => Execute(() => {
            if (document == null) throw new ArgumentException("Brak danych.");
            var backup = events.Publish(document, User.Identity.Name);
            logger.LogInformation("Seasonal publish: user={User} game={Game} server={Server} folder={Folder} backup={Backup}", User.Identity.Name, document.Game, document.Server, document.Folder, backup);
            return new { backup, document = events.Load(document.Server, document.Game, document.Folder) };
        });
    }
}
