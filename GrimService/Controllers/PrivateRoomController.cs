using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using GrimService.Database.Controllers;
using GrimService.Database.Tables;
using GrimService.Filters;
using GrimService.Models.Xml;
using GrimService.Services;

namespace GrimService.Controllers
{
    [ApiController, PDIClient, Produces("application/xml"), Authorize]
    public class PrivateRoomController : GrimControllerBase
    {
        private readonly ILogger<PrivateRoomController> _logger;
        private readonly PrivateRoomsDBManager _rooms;

        public PrivateRoomController(PlayerManager players, PrivateRoomsDBManager rooms,
            ILogger<PrivateRoomController> logger) : base(players)
        {
            _rooms = rooms;
            _logger = logger;
        }

        [HttpPost("/ap/private_room/")]
        [RequestSizeLimit(1048576)]
        public async Task<ActionResult> Post()
        {
            var player = Player;
            if (player == null) return Unauthorized();
            GrimRequest request;
            try { request = await GrimRequest.Deserialize(Request.Body); }
            catch (InvalidOperationException) { return BadRequest(GrimResult.FromInt(-1)); }
            if (request?.Params?.ParamList == null)
                return BadRequest(GrimResult.FromInt(-1));

            _logger.LogDebug("[{User}] Log: <- {Command}", player.Data.PSNUserId, request.Command);
            if (request.Command != "room.createPrivateRoom" && request.Command != "room.updatePrivateRoom")
                return BadRequest(GrimResult.FromInt(-1));
            if (!request.TryGetParameterByKey("stats", out var stats) || string.IsNullOrWhiteSpace(stats.Text))
            {
                _logger.LogWarning("Private room rejected: user={User}, reason=missing stats", player.Data.PSNUserId);
                return BadRequest(GrimResult.FromInt(-1));
            }
            var hasOwner = request.TryGetParameterByKey("user_id", out var owner);
            if (request.Command == "room.updatePrivateRoom" && !hasOwner)
                return BadRequest(GrimResult.FromInt(-1));

            if (hasOwner && !string.Equals(owner.Text, player.Data.PSNUserId, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Private room rejected: user={User}, requestedOwner={Owner}",
                    player.Data.PSNUserId, owner.Text);
                return StatusCode(403, GrimResult.FromInt(-1));
            }
            var config = new PrivateRoomsDTO(player.Data.Id, player.Data.PSNUserId,
                stats.Text, DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified));
            var id = await _rooms.SaveAsync(config);
            _logger.LogInformation("Private room saved: user={User}, config={Id}, statsLength={Length}, result=1",
                player.Data.PSNUserId, id, stats.Text.Length);
            return Ok(GrimResult.FromBool(true));
        }

        [HttpGet("/private_room/{username}.xml")]
        public async Task<ActionResult> Get(string username)
        {
            if (Player == null) return Unauthorized();
            var room = (await _rooms.GetPrivateRoom(username)).FirstOrDefault();
            Response.Headers["Cache-Control"] = "no-store";
            if (room == null)
            {
                _logger.LogDebug("Private room configuration absent: owner={Owner}", username);
                return NotFound();
            }
            _logger.LogDebug("Private room configuration read: owner={Owner}, config={Id}, statsLength={Length}",
                username, room.Id, room.Stats?.Length ?? 0);
            return Ok(PrivateRoom.FromDatabaseObject(room));
        }
    }
}
