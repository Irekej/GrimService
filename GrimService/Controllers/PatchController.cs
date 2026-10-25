using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using GrimService.Filters;
using GrimService.Config;
using GrimService.Features.Resources;

namespace GrimService.Controllers;

[ApiController]
[Route("/[controller]/")]
[Produces("application/xml")]
[PDIClient]
public class PatchController : ControllerBase
{
    private readonly GameServerOptions options;
    public PatchController(IOptions<GameServerOptions> options, ILogger<PatchController> logger) => this.options = options.Value;
    [HttpGet("patchinfo.xml")]
    public Task Get(string region) => new GameResourceStore(options.XmlResourcePath).CopyAsync(HttpContext, ClientResourceRoutes.PatchInfo);
}
