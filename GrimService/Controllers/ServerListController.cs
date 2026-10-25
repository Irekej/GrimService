using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using GrimService.Config;
using GrimService.Features.Bootstrap;
using GrimService.Filters;
using GrimService.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GrimService.Controllers
{
    [ApiController]
    [Produces("application/xml")]
    [PDIClient]
    public class ServerListController : ControllerBase
    {
        private readonly string resourceRoot;

        public ServerListController(IOptions<GameServerOptions> options, ILogger<ServerListController> logger)
        {
            resourceRoot = options.Value.XmlResourcePath;
        }

        [HttpGet]
        [Route("/init/{region}/serverlist.xml")]
        public async Task Get(string region)
        {
            var document = ServerListCatalog.Select(Request.Host.Host, region);
            if (document.InvalidRegion)
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            if (document.RelativePath != null)
                await this.SendFile(resourceRoot, document.RelativePath);
        }
    }
}
