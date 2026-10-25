using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using GrimService.Models.Xml;
using GrimService.Filters;
using GrimService.Services;
using GrimService.Database.Controllers;
using GrimService.Controllers;
using GrimService.Infrastructure.Sessions;

namespace GrimService.Helpers;

[ApiController]
[PDIClient]
[Authorize]
[Route("/ap/misc/extend/")]
[Produces("application/xml")]
public class ExtendSessionController : GrimControllerBase
{
    private readonly ILogger<ExtendSessionController> logger;
    private readonly PlayerManager players;
    private readonly GameRequestSession sessions;

    public ExtendSessionController(ILogger<ExtendSessionController> logger, PlayerManager players, UserDBManager users,
        GameRequestSession sessions = null) : base(players)
    {
        this.logger = logger;
        this.players = players;
        this.sessions = sessions ?? new GameRequestSession(players, new DatabaseGameRequestRecovery(users, players));
    }


    [HttpPost]
    public async Task<ActionResult> GrimResult234()
    {
        var session = await sessions.Resolve(Request.Cookies["X-gt-token"]);
        if (session.Status == GameSessionStatus.MissingToken) return NotFound();
        if (session.Status != GameSessionStatus.Authenticated) return Unauthorized();

        var player = session.Player;
        players.CheckIfNotConnected(player);
        players.UpdatePlayerActivity(player);
        logger.LogInformation("[{name}] Log: Got session extend request", player.Data.PSNUserId);
        return Ok(GrimResult.FromBool(true));
    }
}
