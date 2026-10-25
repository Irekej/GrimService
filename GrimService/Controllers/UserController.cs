using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.Xml.Serialization;
using System.IO;

using GrimService.Database.Controllers;
using GrimService.Database.Tables;
using GrimService.Filters;
using GrimService.Controllers;
using GrimService.Services;
using GrimService.Models;
using GrimService.Models.Xml;
using GrimService.Utils;

namespace GrimService.Helpers
{



    [ApiController]
    [Authorize]
    [PDIClient]
    [Route("[controller]")]
    [Produces("application/xml")]
    public class UserController : GrimControllerBase
    {
        private readonly ILogger<UserController> _logger;
        private readonly UserDBManager _userDb;
        private readonly FriendDBManager _friendsDb;

        public UserController(PlayerManager players, 
            ILogger<UserController> logger, 
            UserDBManager userDb,
            FriendDBManager friendsDb)
            : base(players)
        {
            _logger = logger;
            _userDb = userDb;
            _friendsDb = friendsDb;
        }

        [HttpGet("no/{number:long}.xml")]
        public async Task<ActionResult> GetByNumber(long number)
        {
            if (Player == null) return Unauthorized();
            if (number <= 0) return NotFound();
            var user = await _userDb.GetByIDAsync(number);
            return user == null ? NotFound() : await Get(user.PSNUserId);
        }






        [HttpGet]
        [Route("{userId}.xml")]
        public async Task<ActionResult> Get(string UserId)
        {
            var currentPlayer = Player;
            if (currentPlayer is null)
            {
                _logger.LogWarning("Could not get current player. IP: {RemoteIpAddress}", Request.HttpContext.Connection.RemoteIpAddress);
                return Unauthorized();
            }

            /* TESTING STUFF
            if (UserId == "IrekejO")
            {
                string eventFilePath;
                eventFilePath = $"Resources/user_test.xml";

                if (System.IO.File.Exists(eventFilePath))
                {
                    // Pobranie zawartości pliku
                    string xmlContent = await System.IO.File.ReadAllTextAsync(eventFilePath);

                    // Określenie odpowiedniego MIME Type dla XML
                    var contentType = "application/xml";

                    // Zwrócenie zawartości pliku XML jako ContentResult
                    var contentResult = new ContentResult
                    {
                        Content = xmlContent,
                        ContentType = contentType,
                        StatusCode = 200
                    };

                    return contentResult;
                }
                else
                {
                    return NotFound();
                }
            } */

            var userDto = await _userDb.GetByPSNUserIdAsync(UserId);
            if (userDto == null)
                return NotFound();
            string[] tagValues = Array.Empty<string>();
            if (!string.IsNullOrEmpty(userDto?.TagIdCSV))
            {
                tagValues = userDto.TagIdCSV.Split(",").Distinct().ToArray();
            }
            string tag1 = tagValues.Length > 0 ? tagValues[0] : string.Empty;
            string tag2 = tagValues.Length > 1 ? tagValues[1] : string.Empty;
            string tag3 = tagValues.Length > 2 ? tagValues[2] : string.Empty;
            string tag4 = tagValues.Length > 3 ? tagValues[3] : string.Empty;
            string tag5 = tagValues.Length > 4 ? tagValues[4] : string.Empty;
            string tag6 = tagValues.Length > 5 ? tagValues[5] : string.Empty;

            var profile = new UserProfile();
            profile.UserId = userDto.PSNUserId;
            profile.Number = userDto.Id;
            profile.Country = userDto.Country;
            profile.NicknameChanges = userDto.NicknameChanges;
            profile.LicenseLevel = userDto.LicenseLevel;
            profile.Achievement = userDto.Achievement;
            profile.Trophy = userDto.Trophy;
            profile.CarCount = userDto.CarCount;
            profile.LicenseGold = userDto.LicenseGold;
            profile.Odometer = userDto.Odometer;
            profile.ASpecLevel = userDto.ASpecLevel;
            profile.ASpecExp = userDto.ASpecExp;
            profile.BSpecLevel = userDto.BSpecLevel;
            profile.BSpecExp = userDto.BSpecExp;
            profile.WinCount = userDto.WinCount;
            profile.LicenseSilver = userDto.LicenseSilver;
            profile.LicenseBronze = userDto.LicenseBronze;
            profile.Credit = userDto.Credit;
            profile.ProfileLevel = userDto.ProfileLevel;
            profile.Comment = userDto.Comment;

            if (!string.IsNullOrEmpty(tag1))
                profile.Tags.Add(tag1);
            if (!string.IsNullOrEmpty(tag2))
                profile.Tags.Add(tag2);
            if (!string.IsNullOrEmpty(tag3))
                profile.Tags.Add(tag3);
            if (!string.IsNullOrEmpty(tag4))
                profile.Tags.Add(tag4);
            if (!string.IsNullOrEmpty(tag5))
                profile.Tags.Add(tag5);
            if (!string.IsNullOrEmpty(tag6))
                profile.Tags.Add(tag6);

            profile.Recent = userDto.Recent;
            profile.RecentUpdateTime = userDto.RecentUpdateTime;
            if (currentPlayer.Data.LastGame == "GT6")
                profile.RecentUpdateTimeNative = userDto.RecentUpdateTime.ToRfc3339String();
            profile.WelcomeMessage = userDto.WelcomeMessage;
            profile.Nickname = userDto.Nickname;

            if (Player.Data.LastGame == "GT5")
            {
                profile.HelmetId = userDto.MenuHelmet;
                profile.HelmetColorId = userDto.MenuHelmetColor;
                profile.WearId = userDto.MenuWear;
                profile.WearColorId = userDto.MenuWearColor;
                profile.MenuColor = userDto.MenuColor;
                profile.MenuMatiere = userDto.MenuMatiere;
                profile.MenuHelmet = 0;
                profile.MenuHelmetColor = 0;
                profile.MenuWear = 0;
                profile.MenuWearColor = 0;
            }

            if (Player.Data.LastGame == "GT6")
            {
                profile.HelmetId = userDto.Helmet;
                profile.HelmetColorId = userDto.HelmetColor;
                profile.WearId = userDto.Wear;
                profile.WearColorId = userDto.WearColor;
                profile.MenuColor = userDto.MenuColor;
                profile.MenuMatiere = userDto.MenuMatiere;
                profile.MenuHelmet = 0;
                profile.MenuHelmetColor = 0;
                profile.MenuWear = 0;
                profile.MenuWearColor = 0;




            }

            profile.PhotoIdAvatar = userDto.PhotoIdAvatar;
            profile.PhotoBackground = userDto.PhotoBackground;
            profile.BandDown = userDto.BandDown;
            profile.BandUp = userDto.BandUp;
            profile.BandTest = userDto.BandTest;
            profile.BandUpdateTime = userDto.BandUpdateTime.ToRfc3339String();
            return Ok(profile);
        }
        
    }
}
