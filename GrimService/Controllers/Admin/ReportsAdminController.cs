using System;
using System.Threading.Tasks;
using GrimService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GrimService.Controllers.Admin
{
    [Authorize(AuthenticationSchemes = "AdminCookie", Roles = "Administrator")]
    [Route("admin/reports")]
    public sealed class ReportsAdminController : Controller
    {
        private readonly ReportService reports;
        private readonly WebText text;
        public ReportsAdminController(ReportService reports, WebText text) { this.reports = reports; this.text = text; }
        [HttpGet("")]
        public async Task<IActionResult> Index(string game = "GT6", int page = 1)
        {
            if (game != "GT5" && game != "GT6") return BadRequest();
            page = Math.Clamp(page, 1, 100000);
            ViewData["Game"] = game; ViewData["Page"] = page;
            return View("~/Views/Admin/Reports.cshtml", await reports.List(game, page));
        }
        [HttpPost("review"), ValidateAntiForgeryToken]
        public async Task<IActionResult> Review(long id, int version, string status, string note, string game)
        {
            if (!ModelState.IsValid || (game != "GT5" && game != "GT6")) return BadRequest();
            try
            {
                if (!await reports.Review(id, version, status, note, User.Identity.Name))
                    return Conflict(text["Zgłoszenie zostało zmienione lub nie istnieje. Odśwież listę."]);
                return RedirectToAction(nameof(Index), new { game });
            }
            catch (ArgumentException) { return BadRequest(); }
        }
        [HttpGet("{id:long}/history")]
        public async Task<IActionResult> History(long id) => View("~/Views/Admin/ReportHistory.cshtml", await reports.History(id));
    }
}
