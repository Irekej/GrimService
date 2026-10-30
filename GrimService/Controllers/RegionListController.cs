using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using GrimService.Features.Bootstrap;
using GrimService.Filters;

namespace GrimService.Controllers
{
    [ApiController]
    [PDIClient]
    [Route("/init/regionlist.xml")]
    [Produces("application/xml")]
    public class RegionListController : ControllerBase
    {
        private readonly BootstrapSettingsService settings;
        public RegionListController(BootstrapSettingsService settings) => this.settings = settings;

        [HttpGet]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> Get() => File(await settings.RegionList(), "application/xml; charset=utf-8");
    }
}
