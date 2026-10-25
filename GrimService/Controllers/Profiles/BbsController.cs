using Microsoft.AspNetCore.Mvc;
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
[Route("/ap/[controller]")]
[Produces("application/xml")]
public class BbsController : GrimControllerBase
{
    private readonly PlayerManager players;
    private readonly UserDBManager users;
    private readonly PlayerBoardService board;
    private readonly ILogger<BbsController> logger;

    public BbsController(PlayerManager players, UserDBManager userDb, BbsBoardDBManager bbsDb,
        IOptions<GameServerOptions> options, ILogger<BbsController> logger, PlayerBoardService board = null)
        : base(players)
    {
        this.players = players;
        users = userDb;
        this.logger = logger;
        this.board = board ?? new PlayerBoardService(new PlayerBoardRepository(userDb, bbsDb), TimeProvider.System);
    }

    [HttpPost]
    public async Task<ActionResult> Post()
    {
        var player = await PlayerRecordHttp.Recover(this, users, players);
        if (player == null)
            return Request.Cookies["X-gt-token"] == null ? NotFound() : Unauthorized();
        var request = await PlayerRecordHttp.Read(this);
        if (request == null) return BadRequest(GrimResult.FromInt(-1));
        logger.LogDebug("<- {command}", request.Command);
        var reply = await board.Execute(player.Data.Id, player.Data.PSNUserId, request);
        switch (reply.Status)
        {
            case BoardStatus.Success:
                return reply.Comments == null ? Ok(GrimResult.FromBool(true)) : Ok(reply.Comments);
            case BoardStatus.Forbidden:
                return Forbid();
            case BoardStatus.Unsupported:
                logger.LogDebug("Received unimplemented bbs call: {command}", request.Command);
                return BadRequest(GrimResult.FromInt(-1));
            default:
                logger.LogWarning("Invalid board request: {command}", request.Command);
                return BadRequest();
        }
    }
}
