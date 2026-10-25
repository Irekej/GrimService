using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using GrimService.Config;
using GrimService.Filters;
using GrimService.Utils;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GrimService.Controllers
{
    [ApiController]
    [PDIClient]
    [Route("/init/regionlist.xml")]
    [Produces("application/xml")]
    public class RegionListController : ControllerBase
    {
        private readonly string resourceRoot;

        public RegionListController(IOptions<GameServerOptions> options, ILogger<RegionListController> logger)
        {
            resourceRoot = options.Value.XmlResourcePath;
        }

        [HttpGet]
        public Task Get() => this.SendFile(resourceRoot, "regionlist.xml");
    }
}
