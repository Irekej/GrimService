using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using GrimService.Filters;
using GrimService.Models.Xml;
using GrimService.Services;
using GrimService.Database.Controllers;
using GrimService.Controllers.Services;

namespace GrimService.Controllers
{
    [ApiController, PDIClient, Authorize, Route("/ap/matching2/"), Produces("application/xml")]
    public class Matching2Controller : GrimControllerBase
    {
        private readonly QuickMatchService _matching;
        private readonly ILogger<Matching2Controller> _logger;
        private readonly QuickMatchDBManager _stats;
        private string _command;
        private readonly UserDBManager _users;
        public Matching2Controller(PlayerManager players, QuickMatchService matching, ILogger<Matching2Controller> logger, QuickMatchDBManager stats = null, UserDBManager users = null) : base(players)
        { _matching = matching; _logger = logger; _stats = stats; _users = users; }

        [HttpPost, RequestSizeLimit(65536)]
        public async Task<ActionResult> Post()
        {
            var response = await HandleRequest();
            int status = response is ObjectResult obj ? obj.StatusCode ?? 200 :
                response is StatusCodeResult code ? code.StatusCode : response is ContentResult content ? content.StatusCode ?? 200 : 200;
            if (response is ContentResult xml && xml.ContentType == "application/xml")
            {
                var root = XElement.Parse(xml.Content);
                _logger.LogDebug("[{User}] Quick Match response: command={Command} http={Http} state={State} result={Result} room={Room} game={Game} remaining={Remaining} polling={Polling} users={Users}",
                    Player?.Data.PSNUserId, _command, status, (string)root.Element("state"), (string)root.Element("result"),
                    (string)root.Element("room_id"), (string)root.Element("grim_game_id"), (string)root.Element("remaining_time"),
                    (string)root.Element("polling_time"), root.Descendants("user").Count());
                if (_command == "matching.joinGame" && (string)root.Element("state") == "join")
                    _logger.LogDebug("[{User}] Quick Match XML response: command={Command} xml={Xml}",
                        Player?.Data.PSNUserId, _command, xml.Content);
            }
            else
                _logger.LogDebug("[{User}] Quick Match response: command={Command} http={Http} result={Result}",
                    Player?.Data.PSNUserId, _command, status, (response as ObjectResult)?.Value is GrimResult result ? result.Result : null);
            return response;
        }

        private async Task<ActionResult> HandleRequest()
        {
            var player = Player;
            if (player?.Token == null || player.Token.ExpiryDate <= DateTime.Now)
            {
                if (_users != null && await new SessionRecover(_users, Players).CheckIfPlayerExist(Request.Cookies["X-gt-token"]))
                    player = Player;
                if (player?.Token == null || player.Token.ExpiryDate <= DateTime.Now)
                {
                    _logger.LogWarning("Quick Match session rejected: playerFound={Found}, tokenFound={Token}, expiry={Expiry}",
                        player != null, player?.Token != null, player?.Token?.ExpiryDate);
                    return Unauthorized();
                }
            }

            if (player.Data.LastGame != "GT6") return BadRequest(GrimResult.FromInt(-1));

            bool logXml = _logger.IsEnabled(LogLevel.Debug);
            if (logXml) Request.EnableBuffering();
            long bodyStart = logXml ? Request.Body.Position : 0;
            GrimRequest request;
            try { request = await GrimRequest.Deserialize(Request.Body); }
            catch (InvalidOperationException) { return BadRequest(); }
            if (request == null) return BadRequest();
            if (request.Params?.ParamList == null)
            {
                if (request.Command != "matching.leaveGame") return BadRequest();
                request.Params = new GrimRequestParams { ParamList = new System.Collections.Generic.List<GrimRequestParam>() };
            }
            _command = request.Command;
            if (logXml && _command == "matching.enterGame")
            {
                long bodyEnd = Request.Body.Position;
                Request.Body.Position = bodyStart;
                using var reader = new StreamReader(Request.Body, Encoding.UTF8, true, 1024, leaveOpen: true);
                string requestXml = await reader.ReadToEndAsync();
                Request.Body.Position = bodyEnd;
                _logger.LogDebug("[{User}] Quick Match XML request: command={Command} xml={Xml}",
                    player.Data.PSNUserId, _command, requestXml);
            }
            string Get(string key) => request.TryGetParameterByKey(key, out var value) ? value.Text : null;
            bool Number(string key, out ulong value) => ulong.TryParse(Get(key), NumberStyles.None, CultureInfo.InvariantCulture, out value);
            bool Integer(string key, int min, int max, out int value) =>
                int.TryParse(Get(key), NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= min && value <= max;
            _logger.LogDebug("[{User}] Quick Match: {Command}", player.Data.PSNUserId, request.Command);
            if (request.Command == "matching.getLobbyStatus")
            {
                if (!Integer("level", 0, 3, out var level)) return BadRequest();
                return Content(_matching.LobbyStatus(), "application/xml");
            }
            if (request.Command == "matching.leaveGame")
            {
                ulong? leaveEvent = null, leaveRoom = null;
                if (Get("event_id") != null)
                {
                    if (!Number("event_id", out var suppliedEvent)) return BadRequest(GrimResult.FromInt(-1));
                    leaveEvent = suppliedEvent;
                }
                if (Get("room_id") != null)
                {
                    if (!Number("room_id", out var suppliedRoom)) return BadRequest(GrimResult.FromInt(-1));
                    leaveRoom = suppliedRoom;
                }
                _logger.LogDebug("[{User}] Quick Match leave: event={Event} room={Room}", player.Data.PSNUserId, leaveEvent, leaveRoom);
                return Ok(GrimResult.FromInt(_matching.Cancel(player, leaveEvent, leaveRoom) ? 1 : -1));
            }
            if (!Number("event_id", out var eventId) || eventId == 0) return BadRequest();
            if (request.Command == "matching.enterGame")
            {
                string Numeric(string key) => long.TryParse(Get(key), out var n) ? n.ToString(CultureInfo.InvariantCulture) : "missing/invalid";
                _logger.LogDebug("[{User}] Quick Match entry: event={Event} max={Max} nat={Nat} send={Send} receive={Receive} wait={Wait} timeout={Timeout}",
                    player.Data.PSNUserId, eventId, Numeric("max_entry"), Numeric("nat_type"), Numeric("send_band"), Numeric("receive_band"), Numeric("matching_time"), Numeric("matching_time_out"));
                if (!Integer("max_entry", 1, 16, out var maxEntry) || !Integer("nat_type", 0, 3, out var nat) ||
                    !Integer("send_band", 0, int.MaxValue, out var send) || !Integer("receive_band", 0, int.MaxValue, out var receive)) return BadRequest();
                var pdi = Get("pdi_user_id");
                if (string.IsNullOrWhiteSpace(pdi) || pdi.Length > 2048) return BadRequest();
                try { XmlConvert.VerifyXmlChars(pdi); } catch (XmlException) { return BadRequest(); }
                int wait = 120, timeout;
                if (Get("matching_time") != null && !Integer("matching_time", 0, 600, out wait)) return BadRequest();

                timeout = wait + 90;
                if (Get("matching_time_out") != null && !Integer("matching_time_out", 1, 3600, out timeout)) return BadRequest();
                if (timeout < wait) return BadRequest();
                int settleSeconds = Get("matching_time") == null ? 60 : 0;
                _logger.LogDebug("[{User}] Quick Match effective timing: collect={Collect} settle={Settle} timeout={Timeout}",
                    player.Data.PSNUserId, wait, settleSeconds, timeout);
                return Content(_matching.Enter(player, eventId, maxEntry, nat, send, receive, pdi, wait, timeout, settleSeconds), "application/xml");
            }
            if (request.Command == "matching.joinGame")
                return Content(_matching.Join(player, eventId), "application/xml");
            if (request.Command == "matching.startGame")
            {
                if (!_matching.Start(player, eventId)) return Ok(GrimResult.FromInt(-1));
                var round = _matching.StartedRound(player, eventId);
                if (_stats != null && round.HasValue) await _stats.RecordAsync(round.Value.Round, round.Value.Event, round.Value.Entries);
                return Ok(GrimResult.FromInt(1));
            }
            if (!Number("room_id", out var roomId)) return BadRequest();
            switch (request.Command)
            {
                case "matching.createGame":
                    if (roomId == 0) return BadRequest();
                    return Content(_matching.Create(player, eventId, roomId), "application/xml");
                case "matching.setJoinable":
                    if (!Integer("joinable", 0, 1, out var joinable)) return BadRequest();
                    _logger.LogDebug("[{User}] Quick Match admission: room={Room} joinable={Joinable}", player.Data.PSNUserId, roomId, joinable);
                    return Content(_matching.SetJoinable(player, eventId, roomId, joinable == 1), "application/xml");
                case "matching.heartbeat":
                    var csv = Get("user_id_csv");
                    if (csv == null || csv.Length > 1024) return BadRequest();
                    var users = csv.Split(',', StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal).ToArray();
                    if (users.Length > 16 || users.Any(u => u.Length > 32)) return BadRequest();
                    _logger.LogDebug("[{User}] Quick Match heartbeat: room={Room} reportedMembers={Count} users={Users}",
                        player.Data.PSNUserId, roomId, users.Length, System.Text.Json.JsonSerializer.Serialize(users));
                    return Ok(GrimResult.FromInt(_matching.Heartbeat(player, eventId, roomId, users) ? 1 : -1));
                default: return BadRequest(GrimResult.FromInt(-1));
            }
        }
    }
}
