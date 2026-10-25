using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;
using GrimService.Config;
using GrimService.Services;
using GrimService.Models.Xml;
using GrimService.Filters;
using GrimService.Database.Controllers;
using GrimService.Features.PlayerRecords;

namespace GrimService.Controllers;

[ApiController]
[PDIClient]
[Authorize]
[Route("/ap/[controller]/")]
[Produces("application/xml")]
public class ActionLogController : GrimControllerBase
{
    private readonly PlayerManager players;
    private readonly UserDBManager users;
    private readonly ActionHistoryService history;
    private readonly ILogger<ActionLogController> logger;

    public ActionLogController(PlayerManager players, IOptions<GameServerOptions> options,
        ILogger<ActionLogController> logger, ActionLogDBManager actionLogDb, PlayerManager player,
        UserDBManager users, ActionHistoryService history = null) : base(players)
    {
        this.players = player;
        this.users = users;
        this.logger = logger;
        this.history = history ?? new ActionHistoryService(new ActionHistoryRepository(actionLogDb), logger, TimeProvider.System);
    }

    [HttpPost]
    public async Task<ActionResult> Post(string server)
    {
        var player = await PlayerRecordHttp.Recover(this, users, players);
        if (player == null)
            return Request.Cookies["X-gt-token"] == null ? NotFound() : Unauthorized();
        var request = await PlayerRecordHttp.Read(this);
        if (request == null) return BadRequest(GrimResult.FromInt(-1));
        logger.LogDebug("[{name}] Log: <- {command}", player.Data.PSNUserId, request.Command);
        if (request.Command != "actionlog.putActionLog")
        {
            logger.LogDebug("Got unimplemented actionlog call: {command}", request.Command);
            players.CheckIfNotConnected(player);
            players.UpdatePlayerActivity(player);
            return BadRequest();
        }
        var status = await history.Record(player, request, () => players.UpdatePlayerActivity(player));
        switch (status)
        {
            case ActionHistoryStatus.Saved: return Ok(GrimResult.FromBool(true));
            case ActionHistoryStatus.Ignored: return Ok();
            default:
                logger.LogWarning("Invalid action history parameters: {command}", request.Command);
                return BadRequest();
        }
    }

    [HttpGet]
    [Route("{userNumber}.xml")]
    public async Task GetList(int userNumber)
    {
        var player = await PlayerRecordHttp.Recover(this, users, players);
        if (player == null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        players.UpdatePlayerActivity(player);
        if (!await history.WriteList(userNumber, Response.Body, HttpContext.RequestAborted))
            Response.StatusCode = StatusCodes.Status404NotFound;
    }
}
