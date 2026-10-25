using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;
using GrimService.Controllers;
using GrimService.Services;
using GrimService.Filters;
using GrimService.Database.Controllers;
using GrimService.Features.Diagnostics;
using GrimService.Infrastructure.Sessions;

namespace GrimService.Helpers;

[ApiController]
[PDIClient]
[Authorize]
[Route("/log/{server}/")]
[Produces("application/xml")]
public class LogController : GrimControllerBase
{
    private readonly ILogger<LogController> logger;
    private readonly PlayerManager players;
    private readonly GameRequestSession sessions;

    public LogController(ILogger<LogController> logger, PlayerManager players, UserDBManager users,
        GameRequestSession sessions = null) : base(players)
    {
        this.logger = logger;
        this.players = players;
        this.sessions = sessions ?? new GameRequestSession(players, new DatabaseGameRequestRecovery(users, players));
    }

    [HttpGet]
    public async Task<ActionResult> Get(string server)
    {
        var session = await sessions.Resolve(Request.Cookies["X-gt-token"]);
        if (session.Status == GameSessionStatus.MissingToken) return NotFound();
        if (session.Status != GameSessionStatus.Authenticated) return Unauthorized();
        if (!Request.Headers.TryGetValue("X-gt-log", out var values) ||
            !ClientLogDecoder.TryDecode(values.FirstOrDefault(), LogHelper.GT6Codes, out var message)) return BadRequest();

        var player = session.Player;
        if (message.IsCrash)
        {
            logger.LogInformation("[{name}] received ADHOC crash: {argStr}", player.Data.PSNUserId, message.Text);
            return Ok();
        }
        logger.LogInformation("[{name}] Log: {args}", player.Data.PSNUserId, message.Text);
        players.CheckIfNotConnected(player);
        players.UpdatePlayerActivity(player);
        return Ok();
    }
}
