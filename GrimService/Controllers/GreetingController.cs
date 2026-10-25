using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using GrimService.Filters;
using GrimService.Config;
using GrimService.Features.Resources;

namespace GrimService.Controllers;

[ApiController]
[Authorize]
[PDIClient]
[Route("/[controller]/")]
[Produces("application/xml")]
public class GreetingController : ControllerBase
{
    private readonly GameServerOptions options;
    public GreetingController(IOptions<GameServerOptions> options, ILogger<GreetingController> logger) => this.options = options.Value;

    [HttpGet("{server}/{region}/greeting_list.xml")]
    public Task GetGreetingList(string server, string region) => new GreetingResources(options.XmlResourcePath).CopyAsync(HttpContext, server, region);
    [HttpGet("{server}/{region}/{imageName}.img")]
    public Task GetGreetingCardImage(string server, string region, string imageName) => new GreetingResources(options.XmlResourcePath).CopyAsync(HttpContext, server, region, imageName);
}
