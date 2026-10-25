using GrimService.Database.Controllers;
using GrimService.Database.Tables;
using GrimService.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace GrimService.Controllers
{

    [ApiController]
    [Route("api/discord")]
    public class DiscordApiController : ControllerBase
    {
        private readonly ILogger<CourseController> _logger;
        private readonly UserDBManager _users;
        private readonly CourseDBManager _courses;

        public DiscordApiController(ILogger<CourseController> logger, UserDBManager users, CourseDBManager courses)
        {
            _logger = logger;
            _users = users;
            _courses = courses;
        }

        [HttpPost]
        [Route("link/AaaapIirekswirek2198592gssstggg/elo")]
        public async Task<IActionResult> LinkAccount([FromBody] LinkAccountRequest request)
        {


            if (ModelState.IsValid)
            {
                var userData = await _users.GetByPSNUserIdAsync(request.PsnName);
                if (userData == null)
                {
                    return Ok($"System can not find this name: {request.PsnName}. Check if you put it properly.");
                }
                if (userData.DiscordId == null || userData.DiscordId == "")
                {
                    var existingPlayer = await _users.GetUserByDiscordId(request.DiscordId);
                    if (existingPlayer == null && userData != null)
                    {
                        await _users.UpdateDiscordId(request.DiscordId, userData.Id);
                        _logger.LogInformation("[{name}] Log: Player linked discord account: {discordid}", userData.PSNUserId, request.DiscordId);
                        return Ok($"Successfully added discord account to your GT Account! Your Player Id: {userData.Id}");
                    }
                }
                if (userData.DiscordId != null || userData.DiscordId != "")
                {
                    _logger.LogDebug("[{name}] Log: Someone trying to overwrite DiscordId with: {discordid}", userData.PSNUserId, request.DiscordId);
                    return Ok("This PSN user have already linked discord account. Contact administrator");
                }

                _logger.LogDebug("Log: discordlink account failed for [{discordid}]", request.DiscordId);
                return Ok($"Failed to link your discord with PSN Account. Check if your PSN Name is correct.");
            }

            return BadRequest(ModelState);
        }

        [HttpPost]
        [Route("unlink/AaaapIirekswirek2198592gssstggg/elo")]
        public async Task<IActionResult> UNLinkAccount([FromBody] LinkAccountRequest request)
        {


            if (ModelState.IsValid)
            {
                var userData = await _users.GetByPSNUserIdAsync(request.PsnName);
                if (userData == null)
                {
                    return Ok($"System can not find this name: {request.PsnName}. Check if you put it properly.");
                }
                if (userData.DiscordId == null || userData.DiscordId == "")
                {
                    _logger.LogDebug("Log: fail to unlink unlinked account [{discordid}]", request.DiscordId);
                    return Ok($"Player dont have any linked discord to his PSN GT account.");
                }
                await _users.UpdateDiscordId("", userData.Id);

                _logger.LogDebug("Log: discord unlink account for [{discordid}]", request.DiscordId);
                return Ok($"Unlinked players discord id. Player can link discord again.");
            }

            return BadRequest(ModelState);
        }

        [XmlRoot("LinkAccountRequest")]
        public class LinkAccountRequest
        {
            [XmlElement("PsnName")]
            public string PsnName { get; set; }

            [XmlElement("DiscordId")]
            public string DiscordId { get; set; }
        }


    }
}
