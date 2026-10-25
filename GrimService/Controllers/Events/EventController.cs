using System.Threading.Tasks;
using GrimService.Features.EventCatalog;
using GrimService.Filters;
using GrimService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GrimService.Controllers.Events
{

    [ApiController]
    [PDIClient]
    [Authorize]
    [Route("/[controller]")]
    [Produces("application/xml")]
    public class EventController : GrimControllerBase
    {
        private readonly IEventCatalog catalog;

        public EventController(PlayerManager players, IEventCatalog catalog) : base(players) =>
            this.catalog = catalog;

        [HttpGet]
        [Route("{server}/{fileName}")]
        public Task GetImageFile(string server, string fileName) =>
            SendResourceAsync(catalog.FindImage(server, fileName));

        [HttpGet]
        [Route("{server}/setting.xml")]
        public async Task GetSettings(string server)
        {
            var player = Player;
            if (player == null) { Response.StatusCode = StatusCodes.Status401Unauthorized; return; }
            await SendResourceAsync(catalog.FindSettings(server, player.Data.LastGame));
        }

        [HttpGet]
        [Route("{server}/event_list.xml")]
        public async Task<ActionResult> GetOnlineEventList(string server)
        {
            var player = Player;
            if (player == null) return Unauthorized();
            return await XmlResourceAsync(catalog.FindList(server, player.Data.LastGame));
        }

        [HttpGet]
        [Route("{server}/event_{folderId:int}.xml")]
        public async Task<IActionResult> GetOnlineEvent(string server, int folderId)
        {
            var player = Player;
            if (player == null) return Unauthorized();
            return await XmlResourceAsync(catalog.FindEvent(server, player.Data.LastGame, folderId));
        }

        private async Task<ActionResult> XmlResourceAsync(EventResource resource)
        {
            if (resource == null) return NotFound();
            return Content(await resource.ReadXmlAsync(HttpContext.RequestAborted), "application/xml");
        }

        private async Task SendResourceAsync(EventResource resource)
        {
            if (resource == null) { Response.StatusCode = StatusCodes.Status404NotFound; return; }

            await resource.CopyToAsync(Response.Body, HttpContext.RequestAborted);
        }
    }
}
