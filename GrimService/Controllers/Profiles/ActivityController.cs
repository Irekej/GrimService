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
using GrimService.Controllers.Services;

namespace GrimService.Controllers.Profiles
{



    [ApiController]
    [PDIClient]
    [Route("/ap/[controller]/")]
    [Produces("application/xml")]
    [Authorize]
    public class ActivityController : GrimControllerBase
    {
        private readonly ILogger<ActivityController> _logger;
        private readonly GameServerOptions _gsOptions;

        private readonly FriendDBManager _friendDB;
        private readonly UserDBManager _userDB;
        private readonly PlayerManager _players;
        private readonly UserSpecialDBManager _userSpecialDB;
        private readonly GT6SocialDBManager _social;

        public ActivityController(PlayerManager players,
            UserDBManager userDB,
            FriendDBManager friendDB,
            UserSpecialDBManager userSpecialDB,
            IOptions<GameServerOptions> gsOptions,
            ILogger<ActivityController> logger, GT6SocialDBManager social = null)
            : base(players)
        {
            _logger = logger;
            _gsOptions = gsOptions.Value;
            _players = players;
            _userDB = userDB;
            _friendDB = friendDB;
            _userSpecialDB = userSpecialDB;
            _social = social;
        }

        [HttpPost]
        public async Task<ActionResult> Post()
        {
            var player = Player;
            if (Player == null)
            {
                string sessionTicket = Request.Cookies["X-gt-token"];
                if (sessionTicket == null)
                {
                    return NotFound();
                }
                var sessionRecover = new SessionRecover(_userDB, _players);
                bool playerExists = await sessionRecover.CheckIfPlayerExist(sessionTicket);
                if (!playerExists)
                {
                    return Unauthorized();
                }
            }

            player = Player;
            if(player==null)return Unauthorized();

            GrimRequest gRequest;
            try { gRequest = await GrimRequest.Deserialize(Request.Body); }
            catch(InvalidOperationException) { return BadRequest(); }
            if (gRequest is null)
            {

                var badReq = GrimResult.FromInt(-1);
                return BadRequest(badReq);
            }

            _logger.LogDebug("<- {command}", gRequest.Command);

            switch (gRequest.Command)
            {
                case "activity.get":
                    return await OnActivityGet(player, gRequest);
                case "activity.put":
                    if(player.Data.LastGame!="GT6"||_social==null||gRequest.Params?.ParamList==null)return BadRequest();


                    if(!gRequest.TryGetParameterByKey("value0",out var kind)||kind.Text!="RU"||
                        !gRequest.TryGetParameterByKey("value1",out var user)||user.Text!=player.Data.PSNUserId||
                        !gRequest.TryGetParameterByKey("value2",out var recent)||recent.Text.Length>400||recent.Text!=player.Data.Recent)
                        return BadRequest(GrimResult.FromInt(-1));
                    await _social.AddActivityAsync(player.Data.Id,null,new[]{"RU",player.Data.PSNUserId,recent.Text});
                    return Ok(GrimResult.FromInt(1));
            }

            _logger.LogDebug("Received unimplemented profile call: {command}", gRequest.Command);
            var res = GrimResult.FromInt(-1);
            return BadRequest(res);
        }

        private async Task<ActionResult> OnActivityGet(Player player, GrimRequest request)
        {
            if(player.Data.LastGame=="GT6" && _social!=null)
                return Content(GT6SocialXml.Activity(await _social.ActivityAsync(player.Data.Id)),"application/xml");
            /*
            if (!request.TryGetParameterByKey("user_id", out var userIdParam) || userIdParam.Text.Length > 32)
            {
                _logger.LogWarning("Got activity get request without 'user_id'");
                return BadRequest();
            }
            */

            return Ok();
        }

    }

}
