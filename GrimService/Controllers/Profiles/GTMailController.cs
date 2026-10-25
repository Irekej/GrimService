using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using GrimService.Filters;
using GrimService.Models;
using GrimService.Models.Xml;
using GrimService.Services;
using GrimService.Database.Controllers;
using GrimService.Controllers.Services;
using GrimService.Features.Messaging;

namespace GrimService.Controllers
{
    [ApiController]
    [PDIClient]
    [Authorize]
    [Route("/ap/[controller]")]
    [Produces("application/xml")]
    public class GTMailController : GrimControllerBase
    {
        private readonly ILogger<GTMailController> _logger;
        private readonly Gt5MailService _gt5;
        private readonly Gt6MailService _gt6;
        private readonly UserDBManager _users;
        private readonly PlayerManager _players;

        public GTMailController(PlayerManager players, ILogger<GTMailController> logger,
            Gt5MailService gt5, UserDBManager users, Gt6MailService gt6 = null) : base(players)
        {
            _logger = logger;
            _gt5 = gt5;
            _gt6 = gt6;
            _users = users;
            _players = players;
        }

        [HttpPost]
        [RequestSizeLimit(65536)]
        public async Task<ActionResult> Post()
        {
            if (Player == null)
            {
                string ticket = Request.Cookies["X-gt-token"];
                if (ticket == null) return NotFound();
                if (!await new SessionRecover(_users, _players).CheckIfPlayerExist(ticket)) return Unauthorized();
            }
            var player = Player;
            if (player == null) return Unauthorized();

            GrimRequest request;
            try { request = await GrimRequest.Deserialize(Request.Body); }
            catch (System.Xml.XmlException) { return BadRequest(); }
            catch (InvalidOperationException) { return BadRequest(); }
            if (request?.Params?.ParamList == null) return BadRequest();
            if (player.Token == null || player.Token.ExpiryDate <= DateTime.Now) return Unauthorized();

            if (player.Data.LastGame == "GT6") return await HandleGt6(player, request);
            if (player.Data.LastGame != "GT5") return BadRequest();
            return await HandleGt5(player, request);
        }

        private async Task<ActionResult> HandleGt5(Player player, GrimRequest request)
        {
            switch (request.Command)
            {
                case "mail.getlist":
                    if (!request.TryGetParameterLongByKey("mail_id", out long cursor) || cursor < 0 ||
                        !request.TryGetParameterByKey("by", out var mailbox) || !MailRules.IsMailbox(mailbox.Text))
                        return BadRequest();
                    return Ok(await _gt5.ListAsync(player.Data.PSNUserId, cursor, mailbox.Text));
                case "mail.delete.to":
                case "mail.delete.from":
                    if (!request.TryGetParameterLongByKey("mail_id", out long id) || id < 0) return BadRequest();
                    if (!await _gt5.DeleteAsync(id, player.Data.PSNUserId, request.Command == "mail.delete.from")) return Forbid();
                    return Ok(GrimResult.FromLong(1));
                case "mail.send":
                    if (!ReadMessage(request, out var to, out var subject, out var body)) return BadRequest();
                    var sent = await _gt5.SendAsync(player.Data.Id, player.Data.PSNUserId, to, subject, body);
                    if (sent.Status == MailSendStatus.Invalid) return BadRequest();
                    if (sent.Status == MailSendStatus.RecipientMissing) return NotFound();
                    _logger.LogDebug("[{name}] Log: GTMail send request (to -> [{ToUsername}])", player.Data.PSNUserId, to);
                    return Ok(GrimResult.FromLong(sent.Id));
                default:
                    _logger.LogDebug("Received unimplemented mail command: {Command}", request.Command);
                    return BadRequest();
            }
        }

        private async Task<ActionResult> HandleGt6(Player player, GrimRequest request)
        {
            if (_gt6 == null) return StatusCode(503);
            if (!request.TryGetParameterLongByKey("mail_id", out long id) || id < 0) return BadRequest();
            switch (request.Command)
            {
                case "mail.getlist":
                    if (!request.TryGetParameterByKey("by", out var mailbox) || !MailRules.IsMailbox(mailbox.Text)) return BadRequest();
                    return Content(await _gt6.ListAsync(player.Data.Id, id, mailbox.Text), "application/xml");
                case "mail.send":
                    if (!ReadMessage(request, out var to, out var subject, out var body)) return BadRequest();
                    var sent = await _gt6.SendAsync(player.Data.Id, to, subject, body);
                    if (sent.Status == MailSendStatus.Invalid) return BadRequest();
                    if (sent.Status == MailSendStatus.RecipientMissing) return NotFound();
                    return Content(await _gt6.ListAsync(player.Data.Id, id, "all"), "application/xml");
                case "mail.delete.to":
                case "mail.delete.from":
                case "mail.read.to":
                    if (id == 0) return BadRequest();
                    if (!await _gt6.ChangeAsync(player.Data.Id, id, request.Command)) return Forbid();
                    return Ok(GrimResult.FromLong(1));
                default: return BadRequest();
            }
        }

        private static bool ReadMessage(GrimRequest request, out string to, out string subject, out string body)
        {
            request.TryGetParameterByKey("to", out var recipient);
            request.TryGetParameterByKey("subject", out var title);
            request.TryGetParameterByKey("body", out var text);
            to = recipient?.Text;
            subject = title?.Text;
            body = text?.Text;
            return recipient != null && title != null && text != null;
        }
    }
}
