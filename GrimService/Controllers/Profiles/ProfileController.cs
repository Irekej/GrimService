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
using GrimService.Results;
using static Npgsql.PostgresTypes.PostgresCompositeType;
using System.Xml;
using GrimService.Controllers.Services;
using GrimService.Infrastructure.Sessions;
using GrimService.Infrastructure.Protocol;

namespace GrimService.Controllers.Profiles
{



    [ApiController]
    [PDIClient]
    [Route("/ap/[controller]/")]
    [Produces("application/xml")]
    [Authorize]
    public class ProfileController : GrimControllerBase
    {
        private readonly ILogger<ProfileController> _logger;
        private readonly GameServerOptions _gsOptions;

        private readonly FriendDBManager _friendDB;
        private readonly UserDBManager _userDB;
        private readonly UserSpecialDBManager _userSpecialDB;
        private readonly PlayerManager _players;
        private readonly PresenceDBManager _presenceDB;
        private readonly GT6SocialDBManager _social;
        private readonly GameRequestSession _sessions;
        private readonly GrimService.Features.Friends.FriendProfileService _friendProfiles;

        public ProfileController(PlayerManager players,
            UserDBManager userDB,
            FriendDBManager friendDB,
            PlayerManager players1,
            PresenceDBManager presenceDB,

            UserSpecialDBManager userSpecialDB,
            IOptions<GameServerOptions> gsOptions, 
            ILogger<ProfileController> logger, GT6SocialDBManager social = null, GameRequestSession sessions = null,
            GrimService.Features.Friends.FriendProfileService friendProfiles = null)
            : base(players)
        {
            _logger = logger;
            _gsOptions = gsOptions.Value;

            _userDB = userDB;
            _friendDB = friendDB;
            _presenceDB = presenceDB;
            _userSpecialDB = userSpecialDB;
            _players = players1;
            _social = social;
            _friendProfiles = friendProfiles ?? new(new GrimService.Features.Friends.DatabaseFriendProfiles(friendDB, userDB));
            _sessions = sessions ?? new GameRequestSession(players1, new DatabaseGameRequestRecovery(userDB, players1));
        }

        [HttpPost]
        public async Task<ActionResult> Post()
        {
            var session = await _sessions.Resolve(Request.Cookies["X-gt-token"]);
            if (session.Status == GrimService.Infrastructure.Sessions.GameSessionStatus.MissingToken) return NotFound();
            if (session.Status != GrimService.Infrastructure.Sessions.GameSessionStatus.Authenticated) return Unauthorized();
            var player = session.Player;

            _players.CheckIfNotConnected(player);
            _players.UpdatePlayerActivity(player);

            GrimRequest gRequest = await GameRequestPayload.ReadAsync(Request);
            if (gRequest is null)
            {

                var badReq = GrimResult.FromInt(-1);
                return BadRequest(badReq);
            }



            switch (gRequest.Command)
            {
                case "profile.update":
                    return await OnProfileUpdate(player, gRequest);
                case "profile.getspecialstatus":
                    return OnGetSpecialStatus();
                case "profile.updatefriendlist":
                    return await OnUpdateFriendList(gRequest);
                case "profile.getsimplefriendlist":
                    return await OnGetSimpleFriendList(player);
                case "profile.getListFriend":
                    return await OnGetListFriend(player, gRequest);
                case "profile.updateNickname":
                    return await OnUpdateNickname(player, gRequest);
                case "profile.setpresence":
                    return await SetPresence(gRequest);
                case "profile.getpresence":
                    return await GetPresence(gRequest);
                case "profile.getSpecialList":
                    return await OnGetUserSpecialPresentList(player, gRequest);


                case "profile.getuserstatistics":
                    return Test12345(player, gRequest);
            }

            _logger.LogDebug("[{name}] Log: Received unimplemented profile call: {command}", Player.Data.PSNUserId, gRequest.Command);
            var res = GrimResult.FromInt(-1);
            return BadRequest(res);
        }

        #region FriendsLists

        private async Task<ActionResult> OnGetSimpleFriendList(Player player)
        {
            return Ok(await _friendProfiles.ForGt5(player.Data.Id));
        }


        private async Task<ActionResult> OnGetListFriend(Player player, GrimRequest requestReq)
        {
            if (!requestReq.TryGetParameterByKey("user_id", out var userId) || userId.Text.Length > 32) return BadRequest();
            return Content(await _friendProfiles.ForGt6(player.Data.Id), "application/xml");
        }

        #endregion

        #region UpdaterMAIN

        private async Task<ActionResult> OnProfileUpdate(Player player, GrimRequest requestReq)
        {

            if (requestReq.TryGetParameterByKey("license_level", out var licenselevelParam))
            {
                if (requestReq.TryGetParameterByKey("aspec_level", out var aspeclevelParam) && requestReq.TryGetParameterByKey("bspec_level", out var bspeclevelParam))
                {
                    return await GT5ProfileUpdate(player, requestReq);
                }
                return await ProfileUpdate(player, requestReq);
            }
            if (requestReq.TryGetParameterByKey("recent", out var recentParam))
            {
                return await OnProfileRecentUpdate(player, requestReq);
            }
            if (requestReq.TryGetParameterByKey("profile_level", out var profilelevelParam))
            {
                return await OnProfileSettingsUpdate(player, requestReq);
            }



            if (requestReq.TryGetParameterByKey("menu_color", out var menu_color) && requestReq.TryGetParameterByKey("menu_matiere", out var menu_matiere))
            {
                return await OnUpdateMyHomeDesign(player, menu_color.Text, menu_matiere.Text);
            }
            if (requestReq.TryGetParameterByKey("helmet", out var helmet) && requestReq.TryGetParameterByKey("helmet_color", out var helmetColor))
            {
                return await OnUpdateHelmet(player, helmet.Text, helmetColor.Text);
            }
            if (requestReq.TryGetParameterByKey("wear", out var wear) && requestReq.TryGetParameterByKey("wear_color", out var wear_color))
            {
                return await OnUpdateWear(player, wear.Text, wear_color.Text);
            }
            if (requestReq.TryGetParameterByKey("welcomemessage", out var welcomeMessage) && requestReq.Params.ParamList.Count == 1)
            {
                return await OnUpdateAutoMessage(player, welcomeMessage.Text);
            }


            return Ok();
        }

        private async Task<ActionResult> OnProfileSettingsUpdate(Player player, GrimRequest requestReq)
        {

            requestReq.TryGetParameterByKey("profile_level", out var profilelevelParam);
            requestReq.TryGetParameterByKey("comment", out var commentParam);
            requestReq.TryGetParameterByKey("tag_id_csv", out var tagidcsvParam);

            /*
            if (!requestReq.TryGetParameterByKey("comment", out var comment2Param))
            {
                if (!requestReq.TryGetParameterByKey("tag_id_csv", out var tagidcsv2Param))
                {
                    return await OnRequestUpdateProfileLevelSettings(player, profilelevelParam.Text);
                }
                return await OnRequestUpdateCommentSettings(player, profilelevelParam.Text, tagidcsv2Param.Text);
            }*/

            if (commentParam == null &&  tagidcsvParam == null) 
            {
                _logger.LogDebug("[{name}] Log: Updated his ProfileLevel", Player.Data.PSNUserId);
                return await OnRequestUpdateProfileLevelSettings(player, profilelevelParam.Text);
            }
            if (commentParam == null)
            {
                _logger.LogDebug("[{name}] Log: Updated his Tags", Player.Data.PSNUserId);
                return await OnRequestUpdateTagIdSettings(player, tagidcsvParam.Text);
            }
            if (tagidcsvParam == null)
            {
                _logger.LogDebug("[{name}] Log: Updated his Comment", Player.Data.PSNUserId);
                return await OnRequestUpdateCommentSettings(player, profilelevelParam.Text, commentParam.Text);
            }




            _logger.LogDebug("[{name}] Log: Received Settings profile.update with all data.", Player.Data.PSNUserId);
            return await OnRequestUpdateGameSettings(player, profilelevelParam.Text, commentParam.Text, tagidcsvParam.Text);
        }

        private async Task<ActionResult> OnProfileRecentUpdate(Player player, GrimRequest requestReq)
        {

            if (!requestReq.TryGetParameterByKey("recent", out var recentParam))
            {
                _logger.LogDebug("[{name}] Log: Received wrong profile data: recent", Player.Data.PSNUserId);
                return BadRequest();
            }

            string recent = recentParam.Text;




            _logger.LogDebug("[{name}] Log: Received RECENT profile.update with all data.", Player.Data.PSNUserId);
            return await OnGetRecentUpdate(player, recent, DateTime.Now);
        }

        private async Task<ActionResult> GT5ProfileUpdate(Player player, GrimRequest requestReq)
        {
            if (!requestReq.TryGetParameterByKey("aspec_level", out var aspecLevelParam))
                return BadRequest();

            if (!requestReq.TryGetParameterByKey("aspec_exp", out var aspecExpParam))
                return BadRequest();

            if (!requestReq.TryGetParameterByKey("bspec_level", out var bspecLevelParam))
                return BadRequest();

            if (!requestReq.TryGetParameterByKey("bspec_exp", out var bspecExpParam))
                return BadRequest();

            if (!requestReq.TryGetParameterByKey("achievement", out var achievementParam))
            {
                return BadRequest();
            }

            if (!requestReq.TryGetParameterByKey("credit", out var creditParam))
                return BadRequest();

            if (!requestReq.TryGetParameterByKey("win_count", out var winCountParam))
                return BadRequest();

            if (!requestReq.TryGetParameterByKey("car_count", out var carCountParam))
            {
                _logger.LogDebug("Received wrong profile data: car_count");
                return BadRequest();
            }

            if (!requestReq.TryGetParameterByKey("trophy", out var trophyParam))
            {
                _logger.LogDebug("Received wrong profile data: trophy");
                return BadRequest();
            }

            if (!requestReq.TryGetParameterByKey("odometer", out var odometerParam))
            {
                _logger.LogDebug("Received wrong profile data: odometer");
                return BadRequest();
            }

            if (!requestReq.TryGetParameterByKey("license_level", out var licenselevelParam))
            {
                _logger.LogDebug("Received wrong profile data: license_level");
                return BadRequest();
            }

            if (!requestReq.TryGetParameterByKey("license_gold", out var licenseGoldParam))
            {
                _logger.LogDebug("Received wrong profile data: license_gold");
                return BadRequest();
            }

            if (!requestReq.TryGetParameterByKey("license_silver", out var licenseSilverParam))
                return BadRequest();

            if (!requestReq.TryGetParameterByKey("license_bronze", out var licenseBronzeParam))
                return BadRequest();



            string aspec_level = aspecLevelParam.Text;
            string aspec_exp = aspecExpParam.Text;
            string bspec_level = bspecLevelParam.Text;
            string bspec_exp = bspecExpParam.Text;
            string achievement = achievementParam.Text;
            string trophy = trophyParam.Text;
            string car_count = carCountParam.Text;
            string odometer = odometerParam.Text;
            string win_count = winCountParam.Text;
            string credit = creditParam.Text;
            string license_level = licenselevelParam.Text;
            string license_gold = licenseGoldParam.Text;
            string license_silver = licenseSilverParam.Text;
            string license_bronze = licenseBronzeParam.Text;




            _players.UpdatePlayerActivity(Player);

            _logger.LogDebug("[{name}] Log: Received full Gran Turismo 5 profile data.", Player.Data.PSNUserId);
            return await OnRequestUpdateGameInfoGT5(player, license_level, achievement, trophy, car_count, license_gold, odometer, win_count, aspec_level, aspec_exp, bspec_level, bspec_exp, credit, license_silver, license_bronze);
        }

        private async Task<ActionResult> ProfileUpdate(Player player, GrimRequest requestReq)
        {
            if (!requestReq.TryGetParameterByKey("license_level", out var licenselevelParam))
            {
                _logger.LogDebug("Received wrong profile data: license_level");
                return BadRequest();
            }

            if (!requestReq.TryGetParameterByKey("achievement", out var achievementParam))
            {
                _logger.LogDebug("Received wrong profile data: achievement");
                return BadRequest();
            }

            if (!requestReq.TryGetParameterByKey("trophy", out var trophyParam))
            {
                _logger.LogDebug("Received wrong profile data: trophy");
                return BadRequest();
            }

            if (!requestReq.TryGetParameterByKey("car_count", out var carCountParam))
            {
                _logger.LogDebug("Received wrong profile data: car_count");
                return BadRequest();
            }

            if (!requestReq.TryGetParameterByKey("license_gold", out var licenseGoldParam))
            {
                _logger.LogDebug("Received wrong profile data: license_gold");
                return BadRequest();
            }


            if (!requestReq.TryGetParameterByKey("odometer", out var odometerParam))
            {
                _logger.LogDebug("Received wrong profile data: odometer");
                return BadRequest();
            }


            string license_level = licenselevelParam.Text;
            string achievement = achievementParam.Text;
            string trophy = trophyParam.Text;
            string car_count = carCountParam.Text;
            string license_gold = licenseGoldParam.Text;
            string odometer = odometerParam.Text;




            _players.UpdatePlayerActivity(Player);

            _logger.LogDebug("[{name}] Log: Received full Gran Turismo 6 profile data.", Player.Data.PSNUserId);
            return await OnRequestUpdateGameStats(player, license_level, achievement, trophy, car_count, license_gold, odometer);
        }

        #endregion

        #region Updaters








        private async Task<ActionResult> OnUpdateWear(Player player, string wearParam, string wearColorParam)
        {
            if (!int.TryParse(wearParam, out int wearId))
                return BadRequest();
            if (!int.TryParse(wearColorParam, out int wearColorId))
                return BadRequest();

            player.Data.Wear = wearId;
            player.Data.WearColor = wearColorId;

            if (Player.Data.LastGame == "GT5")
            {
                _logger.LogDebug("[{name}] Log: Received wear update for Gran Turismo 5", Player.Data.PSNUserId);
                await _userDB.UpdateWearGT5(player.Data.Id, wearId, wearColorId);
            }
            if (Player.Data.LastGame == "GT6")
            {
                _logger.LogDebug("[{name}] Log: Received wear update for Gran Turismo 6", Player.Data.PSNUserId);
                await _userDB.UpdateWear(player.Data.Id, wearId, wearColorId);
            }

            return Ok(GrimResult.FromBool(true));
        }








        private async Task<ActionResult> OnUpdateHelmet(Player player, string helmetParam, string helmetColorParam)
        {
            if (!int.TryParse(helmetParam, out int helmetId))
                return BadRequest();
            if (!int.TryParse(helmetColorParam, out int helmetColorId))
                return BadRequest();

            player.Data.Helmet = helmetId;
            player.Data.HelmetColor = helmetColorId;

            if (Player.Data.LastGame == "GT5")
            {
                _logger.LogDebug("[{name}] Log: Received helmet update for Gran Turismo 5", Player.Data.PSNUserId);
                await _userDB.UpdateHelmetGT5(player.Data.Id, helmetId, helmetColorId);
            }
            if (Player.Data.LastGame == "GT6")
            {
                _logger.LogDebug("[{name}] Log: Received helmet update for Gran Turismo 6", Player.Data.PSNUserId);
                await _userDB.UpdateHelmet(player.Data.Id, helmetId, helmetColorId);
            }
            return Ok(GrimResult.FromBool(true));
        }







        private async Task<ActionResult> OnUpdateAutoMessage(Player player, string message)
        {
            if (message.Length > 30)
                return BadRequest();

            var data = player.Data;
            data.WelcomeMessage = message;

            await _userDB.UpdateWelcomeMessage(data);
            return Ok(GrimResult.FromBool(true));
        }








        private async Task<ActionResult> OnUpdateMyHomeDesign(Player player, string menuColorParam, string menuMatiereParam)
        {
            if (!int.TryParse(menuColorParam, out int menuColorIndex))
                return BadRequest();
            if (!int.TryParse(menuMatiereParam, out int menuMatiereIndex))
                return BadRequest();

            player.Data.MenuColor = menuColorIndex;
            player.Data.MenuMatiere = menuMatiereIndex;

            _logger.LogDebug("[{name}] Log: Received home design update for Gran Turismo 5", Player.Data.PSNUserId);

            await _userDB.UpdateHomeDesign(player.Data);
            return Ok(GrimResult.FromBool(true));
        }







        private async Task<ActionResult> OnGetRecentUpdate(Player player, string recent, DateTime UpdateTime)
        {

            var data = player.Data;
            data.Recent = recent;
            data.RecentUpdateTime = UpdateTime;

            _logger.LogDebug("[{name}] Log: Received recent update: {recent}", Player.Data.PSNUserId, recent);

            await _userDB.UpdateGameRecent(data);
            if(player.Data.LastGame=="GT6" && _social!=null)
                await _social.AddActivityAsync(player.Data.Id,null,new[]{"RU",player.Data.PSNUserId,recent});
            return Ok(GrimResult.FromBool(true));
        }









        private async Task<ActionResult> OnRequestUpdateGameSettings(Player player, string profile_level, string comment, string tag_id_csv)
        {

            var data = player.Data;
            data.ProfileLevel = profile_level;
            data.Comment = comment;
            data.TagIdCSV = tag_id_csv;

            await _userDB.UpdateGameSettings(data);
            return Ok(GrimResult.FromBool(true));
        }

        private async Task<ActionResult> OnRequestUpdateTagIdSettings(Player player, string tag_id_csv)
        {

            var data = player.Data;
            data.TagIdCSV = tag_id_csv;

            await _userDB.UpdateTagSettings(data);
            return Ok(GrimResult.FromBool(true));
        }

        private async Task<ActionResult> OnRequestUpdateCommentSettings(Player player, string profile_level, string comment)
        {

            var data = player.Data;
            data.ProfileLevel = profile_level;
            data.Comment = comment;

            await _userDB.UpdateCommentSettings(data);
            return Ok(GrimResult.FromBool(true));
        }

        private async Task<ActionResult> OnRequestUpdateProfileLevelSettings(Player player, string profile_level)
        {

            var data = player.Data;
            data.ProfileLevel = profile_level;

            await _userDB.UpdateProfileLevelSettings(data);
            return Ok(GrimResult.FromBool(true));
        }












        private async Task<ActionResult> OnRequestUpdateGameStats(Player player, string license_level, string achievement, string trophy,
            string car_count, string license_gold, string odometer)
        {

            var data = player.Data;
            data.LicenseLevel = license_level;
            data.Achievement = achievement;
            data.Trophy = trophy;
            data.CarCount = car_count;
            data.LicenseGold = license_gold;
            data.Odometer = odometer;

            await _userDB.UpdateGameStats(data);
            return Ok(GrimResult.FromBool(true));
        }

        private async Task<ActionResult> OnRequestUpdateGameInfoGT5(Player player, string license_level, string achievement, string trophy,
    string car_count, string license_gold, string odometer, string win_count, string aspec_level, string aspec_exp, string bspec_level, string bspec_exp, string credit, string license_silver, string license_bronze)
        {

            var data = player.Data;

            data.LicenseLevel = license_level;
            data.Achievement = achievement;
            data.Trophy = trophy;
            data.CarCount = car_count;
            data.LicenseGold = license_gold;
            data.Odometer = odometer;

            data.WinCount = win_count;
            data.Credit = credit;
            data.LicenseSilver = license_silver;
            data.LicenseBronze = license_bronze;
            data.ASpecLevel = aspec_level;
            data.ASpecExp = aspec_exp;
            data.BSpecLevel = bspec_level;
            data.BSpecExp = bspec_exp;

            await _userDB.UpdateMyHomeProfile(data);
            return Ok(GrimResult.FromBool(true));
        }





        private async Task<ActionResult> OnUpdateFriendList(GrimRequest gRequest)
        {
            var player = Player;

            if (player is null)
            {
                _logger.LogWarning("Could not get current player for friend list request");
                return Unauthorized();
            }

            if (!gRequest.TryGetParameterByKey("friend_list", out var param))
            {
                _logger.LogWarning("Got OnUpdateFriendList with missing or invalid friend_list key param");
                return BadRequest();
            }

            string[] userIds = param.Text.Split(",").Distinct().ToArray();

            await _friendDB.UpdateFriendList(player.Data.Id, userIds);



            var res = GrimResult.FromInt(0);
            return Ok(res);
        }






        private async Task<ActionResult> OnUpdateNickname(Player player, GrimRequest gRequest)
        {

            if (!gRequest.TryGetParameterByKey("nickname", out var param))
            {
                _logger.LogWarning("Got SetPresence with missing nickname parameter");
                return BadRequest();
            }

            if (player.Data.NicknameChanges <= 0)
                return BadRequest(GrimResult.FromBool(false));

            if (!IsValidNickname(param.Text))
                return BadRequest(GrimResult.FromBool(false));

            _logger.LogDebug("[{username}] Log: Updated nickname: {nickname}", player.Data.PSNUserId, param.Text);

            player.Data.Nickname = param.Text;
            player.Data.NicknameChanges--;
            await _userDB.UpdateNewNickname(player.Data);

            var result = GrimResult.FromBool(true);
            return Ok(result);

        }

        #endregion

        #region Presence






        private async Task<ActionResult> GetPresence(GrimRequest gRequest)
        {
            var player = Player;
            if (player?.Token == null || player.Token.ExpiryDate <= DateTime.Now) return Unauthorized();
            gRequest.TryGetParameterByKey("user_id", out var userIdParam);
            var name = userIdParam?.Text ?? "";
            if (name.Length > 32 || name.Any(char.IsControl)) return BadRequest(GrimResult.FromInt(-1));
            HashSet<long> friends = null;
            if (string.IsNullOrWhiteSpace(name))
                friends = (await _friendDB.GetAllFriendsOfUser(player.Data.Id)).Select(f => f.FriendId).ToHashSet();
            var result = PresenceProtocol.Read(_players.Players.Values, player.Data.LastGame,
                p => friends != null ? friends.Contains(p.Data.Id) : p.Data.PSNUserId == name, DateTime.UtcNow);
            _logger.LogDebug("[{User}] GetPresence: game={Game}, scope={Scope}, count={Count}",
                player.Data.PSNUserId, player.Data.LastGame, friends != null ? "friends" : name, result.Presences.Count);
            return Ok(result);
        }






        private Task<ActionResult> SetPresence(GrimRequest gRequest)
        {
            var player = Player;
            if (player?.Token == null || player.Token.ExpiryDate <= DateTime.Now)
                return Task.FromResult<ActionResult>(Unauthorized());
            if (!gRequest.TryGetParameterByKey("stats", out var stats) ||
                !PresenceProtocol.Set(player, stats.Text, DateTime.UtcNow))
                return Task.FromResult<ActionResult>(BadRequest(GrimResult.FromInt(-1)));
            _logger.LogDebug("[{User}] SetPresence: game={Game}, stats={Stats}",
                player.Data.PSNUserId, player.Data.LastGame, stats.Text);
            return Task.FromResult<ActionResult>(Ok(GrimResult.FromInt(1)));
        }

        #endregion

        #region UserSpecials






        private async Task<ActionResult> OnGetUserSpecialPresentList(Player player, GrimRequest gRequest)
        {
            if (!gRequest.TryGetParameterByKey("type", out var param))
            {
                _logger.LogWarning("Got profile.getSpecialList request with missing parameter");
                return BadRequest();
            }

            if (param.Text != "3")
            {
                _logger.LogWarning("Got profile.getSpecialList request with type parameter different than 3: {type}", param.Text);
                return BadRequest();
            }

            var user = await _userSpecialDB.GetAllPresentsOfUserAsync(Player.Data.Id, 3);
            var user1 = await _userDB.GetByPSNUserIdAsync(player.Data.PSNUserId);

            var specialList = new SpecialList();
            specialList.Items = new List<UserSpecial>();

            var speciallist1 = new UserSpecial()
            {

            };
            /*
            {
                UserId = player.Data.PSNUserId,
                Key = "DevelopMenu",
                Value = "1",
                Type = 3,
            };
            */

            specialList.Items.Add(speciallist1);
            return Ok(specialList);

            /*
            var result = new SpecialList();
            foreach (var specialData in specialDataList)
            {
                var special = new UserSpecial(player.Data.PSNUserId, specialData.Type, specialData.Key, specialData.Value);
                result.Items.Add(special);

                if (specialData.Type == 3 && specialData.Key.StartsWith("CAR"))
                {
                    // Its a one-time car present
                    await _userSpecialDB.RemoveAsync(specialData.Id);
                }
            }
            

            return Ok(result);

            */
        }





        private ActionResult OnGetSpecialStatus()
        {








            var res = GrimResult.FromInt(0);
            return Ok(res);
        }

        #endregion

        #region Other

        private async Task<bool> CheckIfUserExists()
        {
            var existingRecord = await _presenceDB.GetByPSNUserIdAsync(Player.Data.PSNUserId);
            return existingRecord != null;
        }

        private ActionResult Test12345(Player player, GrimRequest gRequest)
        {

            _logger.LogDebug("[{name}] Log: <- profile.getuserstatistics (GT ACADEMY)", Player.Data.PSNUserId);

            var result = GrimResult.FromInt(1);
            return Ok(result);
        }






        private static bool IsValidNickname(string name)
        {
            if (name.Length > 12 || name.Length == 0)
                return false;

            int spaceCount = 0;
            int dotCount = 0;
            for (int i = 0; i < name.Length; i++)
            {
                if (char.IsLetterOrDigit(name[i]))
                    continue;

                if (i == '.')
                    dotCount++;
                else if (i == ' ')
                    spaceCount++;
            }

            if (dotCount > 1 || spaceCount > 1)
                return false;

            return true;
        }

        #endregion
    }
}
