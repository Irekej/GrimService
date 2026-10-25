using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using GrimService.Filters;
using GrimService.Config;
using GrimService.Features.Resources;

namespace GrimService.Controllers;

[ApiController]
[PDIClient]
[Authorize]
[Route("/data/[controller]/")]
[Route("/data2/[controller]/")]
[Produces("application/xml")]
public class TVController : ControllerBase
{
    private readonly GameServerOptions options;
    public TVController(IOptions<GameServerOptions> options, ILogger<TVController> logger) => this.options = options.Value;

    [HttpGet("/data2/[controller]/{server}/{region}/tv2_{fileRegion}_{arg}.xml")]
    public Task GetTV(string server, string region, string fileRegion, string arg) => Send(ClientResourceRoutes.Television(server, region, fileRegion, arg));
    [HttpGet("/data/[controller]/common/tv2_l_{type}_{fileId:int}.img")]
    public Task GetTVListImage(string type, int fileId) => Send(ClientResourceRoutes.TelevisionImage(type, fileId));

    [NonAction]
    public Task GetCategoryRoot(string server, string region, string region2) => GetTV(server, region, region2, "root");
    [NonAction]
    public Task GetTVList(string server, string region, string fileRegion, int listId) => GetTV(server, region, fileRegion, "l_" + listId);
    [NonAction]
    public Task GetTVSet(string server, string region, string fileRegion, int setId) => GetTV(server, region, fileRegion, "s_" + setId);
    [NonAction]
    public Task GetTVItem(string server, string region, string fileRegion, int itemId) => GetTV(server, region, fileRegion, itemId.ToString());

    private Task Send(string path)
    {
        if (path != null) return new GameResourceStore(options.XmlResourcePath).CopyAsync(HttpContext, path);
        Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }
}
