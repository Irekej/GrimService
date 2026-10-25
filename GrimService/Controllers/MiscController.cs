using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.Xml.Serialization;
using System.IO;

using GrimService.Database.Controllers;
using GrimService.Database.Tables;
using GrimService.Filters;
using GrimService.Models;
using GrimService.Models.Xml;
using GrimService.Utils;
using GrimService.Config;
using GrimService.Services;

namespace GrimService.Controllers
{



    [ApiController]
    [PDIClient]
    [Produces("application/xml")]
    [Authorize]
    public class MiscController : GrimControllerBase
    {
        private readonly ILogger<MiscController> _logger;
        private readonly GameServerOptions _gsOptions;

        private readonly FriendDBManager _friendDB;
        private readonly UserDBManager _userDB;
        private readonly UserSpecialDBManager _userSpecialDB;

        public MiscController(PlayerManager players,
            UserDBManager userDB,
            FriendDBManager friendDB,
            UserSpecialDBManager userSpecialDB,
            IOptions<GameServerOptions> gsOptions,
            ILogger<MiscController> logger)
            : base(players)
        {
            _logger = logger;
            _gsOptions = gsOptions.Value;

            _userDB = userDB;
            _friendDB = friendDB;
            _userSpecialDB = userSpecialDB;
        }

        [Route("/ap/misc/{sub}")]
        [HttpPost]
        public async Task<ActionResult> Post()
        {
            var player = Player;
            if (player is null)
            {
                _logger.LogWarning("Could not get current player. IP: {RemoteIpAddress}", Request.HttpContext.Connection.RemoteIpAddress.ToString());
                return Unauthorized();
            }

            GrimRequest gRequest = await GrimRequest.Deserialize(Request.Body);
            if (gRequest is null)
            {

                var badReq = GrimResult.FromInt(-1);
                return BadRequest(badReq);
            }

            _logger.LogDebug("<- {command}", gRequest.Command);

            switch (gRequest.Command)
            {
                case "lock.lock":
                    return await LockUser(player, gRequest);
                case "lock.unlock":
                    return await UnLockUser(player, gRequest);
            }

            _logger.LogDebug("Received unimplemented Misc call: {command}", gRequest.Command);
            var res = GrimResult.FromInt(-1);
            return BadRequest(res);
        }

        private async Task<ActionResult> LockUser(Player player, GrimRequest gRequest)
        {

            if (!gRequest.TryGetParameterByKey("key", out var key))
            {
                _logger.LogWarning("Got lock.lock with missing key parameter");
                return BadRequest();
            }

            return Ok(GrimResult.FromBool(true));
        }

        private async Task<ActionResult> UnLockUser(Player player, GrimRequest gRequest)
        {

            if (!gRequest.TryGetParameterByKey("key", out var key))
            {
                _logger.LogWarning("Got lock.lock with missing key parameter");
                return BadRequest();
            }

            return Ok(GrimResult.FromBool(true));
        }

    }

}
