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
using System.Xml.Linq;
using System.Text.RegularExpressions;

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
    [Route("/ap/[controller]/")]
    [Produces("application/xml")]
    [Authorize]
    public class Misc2Controller : GrimControllerBase
    {
        private readonly ILogger<Misc2Controller> _logger;
        private readonly GameServerOptions _gsOptions;

        private readonly FriendDBManager _friendDB;
        private readonly UserDBManager _userDB;
        private readonly UserSpecialDBManager _userSpecialDB;
        private readonly QuickMatchDBManager _quickStats;
        private readonly QuickMatchService _quickMatch;
        private readonly ReportService _reports;

        public Misc2Controller(PlayerManager players,
            UserDBManager userDB,
            FriendDBManager friendDB,
            UserSpecialDBManager userSpecialDB,
            IOptions<GameServerOptions> gsOptions,
            ILogger<Misc2Controller> logger, QuickMatchDBManager quickStats = null, QuickMatchService quickMatch = null, ReportService reports = null)
            : base(players)
        {
            _logger = logger;
            _gsOptions = gsOptions.Value;

            _userDB = userDB;
            _friendDB = friendDB;
            _userSpecialDB = userSpecialDB;
            _quickStats=quickStats;_quickMatch=quickMatch;
            _reports = reports;
        }

        [HttpPost, RequestSizeLimit(65536)]
        public async Task<ActionResult> Post()
        {
            var player = Player;
            if (player is null)
            {
                _logger.LogWarning("Could not get current player. IP: {RemoteIpAddress}", Request.HttpContext.Connection.RemoteIpAddress);
                return Unauthorized();
            }

            GrimRequest gRequest;
            try { gRequest = await GrimRequest.Deserialize(Request.Body); }
            catch(InvalidOperationException) { return BadRequest(); }
            if (gRequest?.Params?.ParamList is null)
            {

                var badReq = GrimResult.FromInt(-1);
                return BadRequest(badReq);
            }

            _logger.LogDebug("<- {command}", gRequest.Command);

            switch (gRequest.Command)
            {
                case "counter.get":
                    return await GetCounter(player, gRequest);
                case "counter.increment":
                    return await IncrementQuickMatchCounter(player,gRequest);
                case "report.sendreport":
                case "report.send":
                    return await GetReport(player, gRequest);
                case "entitlement.checkEntitlement":
                    return await CheckEntitlementDLC(player, gRequest);
            }

            _logger.LogDebug("Received unimplemented Misc2 call: {command}", gRequest.Command);
            var res = GrimResult.FromInt(-1);
            return BadRequest(res);
        }

        private static bool QuickKey(string key) => Regex.IsMatch(key??"",@"\AQuickMatch_(RaceCount|EntryCount)_[0-9]{1,8}\z");
        private async Task<ActionResult> IncrementQuickMatchCounter(Player player, GrimRequest request)
        {
            if(player.Data.LastGame!="GT6" || _quickStats==null || _quickMatch==null || player.Token==null || player.Token.ExpiryDate<=DateTime.Now) return Forbid();
            if(!request.TryGetParameterByKey("id",out var id) || !QuickKey(id.Text))return BadRequest();
            var round=_quickMatch.StartedRound(player);
            if(!round.HasValue)return Forbid();
            var r=round.Value;
            bool race=id.Text=="QuickMatch_RaceCount_"+r.Event;
            if(!race && id.Text!="QuickMatch_EntryCount_"+r.Event)return Forbid();
            int delta=1;
            if(request.TryGetParameterByKey("counter",out var count) && !int.TryParse(count.Text,out delta))return BadRequest();
            if(delta!=(race?1:r.Entries))return BadRequest();

            await _quickStats.RecordAsync(r.Round,r.Event,r.Entries);
            return Ok(GrimResult.FromInt(1));
        }
        private async Task<ActionResult> GetCounter(Player player, GrimRequest gRequest)
        {

            if (!gRequest.TryGetParameterByKey("id_csv", out var idcsv))
            {
                _logger.LogWarning("Got GetCounter with missing id_csv parameter");
                return BadRequest();
            }

            if(player.Data.LastGame=="GT6" && _quickStats!=null)
            {
                if(player.Token==null || player.Token.ExpiryDate<=DateTime.Now)return Unauthorized();
                if((idcsv.Text?.Length ?? 0)>8192)return BadRequest();
                var keys=(idcsv.Text ?? "").Split(',',StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
                if(keys.Length>100 || keys.Any(k=>!QuickKey(k)))return BadRequest();
                var values=await _quickStats.GetCountersAsync(keys);
                return Content(new XElement("counter_list",values.Select(k=>new XElement("counter",new XAttribute("id",k.Key),new XAttribute("value",k.Value))))
                    .ToString(SaveOptions.DisableFormatting),"application/xml");
            }

            return Ok(GrimResult.FromBool(true));

        }

        private async Task<ActionResult> GetReport(Player player, GrimRequest gRequest)
        {
            if (_reports == null) return StatusCode(503, GrimResult.FromInt(0));
            try
            {
                var id = await _reports.Submit(player.Data.LastGame, player.Data.Id, player.Data.PSNUserId, gRequest);
                if (id < 0) return StatusCode(429, GrimResult.FromInt(0));
                _logger.LogInformation("Report accepted: game={Game}, reporter={Reporter}, report={Id}, command={Command}",
                    player.Data.LastGame, player.Data.PSNUserId, id, gRequest.Command);
                return Ok(GrimResult.FromBool(true));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning("Report rejected: reporter={Reporter}, reason={Reason}", player.Data.PSNUserId, ex.Message);
                return BadRequest(GrimResult.FromInt(0));
            }
        }

        private async Task<ActionResult> CheckEntitlementDLC(Player player, GrimRequest gRequest)
        {

            if (!gRequest.TryGetParameterByKey("entitlement_id", out var entitlementidParam))
            {
                _logger.LogWarning("error misc2 entitlement_id");
                return BadRequest();
            }

            if (!gRequest.TryGetParameterByKey("mac", out var macParam))
            {
                _logger.LogWarning("error misc2 mac");
                return BadRequest();
            }

            if (!gRequest.TryGetParameterByKey("remaining_count", out var remainingcountParam))
            {
                _logger.LogWarning("error misc2 remaining_count");
                return BadRequest();
            }

            return Ok(GrimResult.FromBool(true));
        }
    }

}
