using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using GrimService.Features.Bootstrap;
using GrimService.Filters;

namespace GrimService.Controllers
{
    [ApiController]
    [Produces("application/xml")]
    [PDIClient]
    public class ServerListController : ControllerBase
    {
        private readonly BootstrapSettingsService settings;
        public ServerListController(BootstrapSettingsService settings) => this.settings = settings;

        [HttpGet]
        [Route("/init/{region}/serverlist.xml")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Get(string region)
        {
            var document = ServerListCatalog.Select(Request.Host.Host, region);
            if (document.InvalidRegion)
            {
                return BadRequest();
            }

            if (document.RelativePath == null) return new EmptyResult();
            var bytes = await settings.ServerList(document.RelativePath, Request.IsHttps);
            return bytes == null ? NotFound() : File(bytes, "application/xml; charset=utf-8");
        }
    }
}
