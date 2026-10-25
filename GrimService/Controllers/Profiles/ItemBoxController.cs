using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using GrimService.Filters;
using GrimService.Models.Xml;
using GrimService.Services;
using GrimService.Features.Gifts;

namespace GrimService.Controllers
{
    [ApiController]
    [PDIClient]
    [Authorize]
    [Route("/ap/[controller]")]
    [Produces("application/xml")]
    public class ItemBoxController : GrimControllerBase
    {
        private readonly ILogger<ItemBoxController> _logger;
        private readonly Gt5GiftService _gifts;

        public ItemBoxController(PlayerManager players, ILogger<ItemBoxController> logger, Gt5GiftService gifts) : base(players)
        { _logger = logger; _gifts = gifts; }

        [HttpPost]
        public async Task<ActionResult> Post()
        {
            var player = Player;
            if (player?.Token == null || player.Token.ExpiryDate <= DateTime.Now) return Unauthorized();
            if (player.Data.LastGame != "GT5") return BadRequest();

            GrimRequest request;
            try { request = await GrimRequest.Deserialize(Request.Body); }
            catch (System.Xml.XmlException) { return BadRequest(); }
            catch (InvalidOperationException) { return BadRequest(); }
            if (request == null || (request.Command != "itembox.getidlist" && request.Params?.ParamList == null)) return BadRequest();

            string username = player.Data.PSNUserId;
            switch (request.Command)
            {
                case "itembox.getlist":
                    if (!request.TryGetParameterLongByKey("itembox_id", out long cursor) || cursor < 0) return BadRequest();
                    return Ok(await _gifts.ListAsync(username, cursor));
                case "itembox.getidlist":
                    return Ok(await _gifts.ListIdsAsync(username));
                case "itembox.send":
                    if (!request.TryGetParameterByKey("receiver", out var receiver) ||
                        !request.TryGetParameterByKey("type", out var type) ||
                        !request.TryGetParameterByKey("stats", out var stats) ||
                        !request.TryGetParameterByKey("mac", out var mac)) return BadRequest();
                    request.TryGetParameterByKey("comment", out var comment);
                    if (!await _gifts.SendAsync(player.Data.Id, username, receiver.Text, type.Text, stats.Text, comment?.Text, mac.Text))
                        return NotFound();
                    _logger.LogDebug("[{name}] Log: ItemBox item send request", username);
                    return Ok(GrimResult.FromLong(1));
                case "itembox.receive":
                    if (!request.TryGetParameterLongByKey("itembox_id", out long id) || id <= 0) return BadRequest();
                    if (!await _gifts.ReceiveAsync(id, username)) return NotFound();
                    _logger.LogDebug("[{name}] Log: ItemBox item remove request ID: {id}", username, id);
                    return Ok(GrimResult.FromLong(1));
                default:
                    _logger.LogDebug("Received unimplemented itembox command: {command}", request.Command);
                    return BadRequest();
            }
        }
    }
}
