using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

using System;
using System.Linq;
using System.Threading.Tasks;

using GrimService.Filters;
using GrimService.Models;
using GrimService.Models.Xml;
using GrimService.Database.Controllers;
using GrimService.Services;
using GrimService.Features.Rankings;
using GrimService.Controllers.Services;

namespace GrimService.Controllers
{



    [ApiController]
    [PDIClient]
    [Authorize]
    [Route("/ap/[controller]")]
    [Produces("application/xml")]
    public class RankingController : GrimControllerBase
    {
        private readonly ILogger<RankingController> _logger;
        private readonly UserDBManager _userDB;
        private readonly FriendDBManager _friendDB;
        private readonly RankingService _rankings;
        private readonly PlayerManager _players;

        public RankingController(PlayerManager players, UserDBManager UserDb, ILogger<RankingController> logger, FriendDBManager friendDB, RankingService rankings) : base(players)
        {
            _players = players;
            _userDB = UserDb;
            _logger = logger;
            _friendDB = friendDB;
            _rankings = rankings;
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
                player = Player;
                if (player == null) return Unauthorized();
            }

            GrimRequest requestReq;
            try { requestReq = await GrimRequest.Deserialize(Request.Body); }
            catch (InvalidOperationException) { return BadRequest(); }
            if (requestReq?.Params?.ParamList is null)
            {

                var badReq = GrimResult.FromInt(-1);
                return BadRequest(badReq);
            }

            _players.CheckIfNotConnected(Player);
            _players.UpdatePlayerActivity(Player);

            _logger.LogDebug("[{name}] Log: <- Got ranking request: {command}", Player.Data.PSNUserId, requestReq.Command);

            switch (requestReq.Command)
            {
                case "ranking.calc2":
                    return await OnGetGT5(player, requestReq);
                case "ranking.calc":
                    return await OnGetCalc2Ranking(requestReq);
                case "ranking.calcTemporary":
                    return await OnGetCalcTemporary(requestReq);
                case "ranking.getCount":
                    return await OnGetCount(requestReq);
                case "ranking.get":
                    return await OnGetGT5(player, requestReq);
                case "ranking.getList":
                    return await OnGetList(player, requestReq);
                case "ranking.getList2":
                    return await OnGetList2(player, requestReq);
                case "ranking.getListFriend":
                    return await OnGetListFriend(player, requestReq);
                case "ranking.update":
                    return await OnRankingUpdate(player, requestReq);
                case "ranking.getListFriends":
                    return requestReq.TryGetParameterByKey("user_id_csv", out _) ?
                        await OnGetList2(player, requestReq) : await OnGetListFriend(player, requestReq);
            }

            _logger.LogDebug("<- Got unknown ranking command: {command}", requestReq.Command);
            var badReqs = GrimResult.FromInt(-1);
            return BadRequest(badReqs);
        }

        private async Task<ActionResult> OnGetCount(GrimRequest request)
        {
            if (!request.TryGetParameterByKey("board_id", out var boardIdParam))
            {
                _logger.LogWarning("Got ranking getCount request without 'board_id'");
                return BadRequest();
            }
            var count = await _rankings.CountAsync(boardIdParam.Text);
            return Ok(GrimResult.FromLong(count));
        }



        private async Task<ActionResult> OnGetGT5(Player player, GrimRequest request)
        {
            if (!request.TryGetParameterByKey("board_id", out var board) ||
                !request.TryGetParameterByKey("user_id", out var user)) return BadRequest();
            var rankings = await _rankings.ReadBoardAsync(board.Text);
            return Ok(RankingResponses.Player(rankings.FindUser(user.Text)));
        }

        private async Task<ActionResult> OnGetList(Player player, GrimRequest request)
        {
            if (!request.TryGetParameterByKey("board_id", out var board) ||
                !request.TryGetParameterIntByKey("begin", out var begin) ||
                !request.TryGetParameterIntByKey("end", out var end) ||
                begin < 1 || end < begin) return BadRequest();
            var rankings = await _rankings.ReadBoardAsync(board.Text);
            return Ok(RankingResponses.List(rankings.Page(begin, end)));
        }

        private async Task<ActionResult> OnGetList2(Player player, GrimRequest request)
        {
            if (!request.TryGetParameterByKey("board_id", out var board) ||
                !request.TryGetParameterByKey("user_id_csv", out var users)) return BadRequest();
            var rankings = await _rankings.ReadBoardAsync(board.Text);
            return Ok(RankingResponses.List(rankings.ForUsers(users.Text.Split(','))));
        }

        private async Task<ActionResult> OnGetListFriend(Player player, GrimRequest request)
        {
            if (!request.TryGetParameterByKey("board_id", out var board)) return BadRequest();
            var friends = await _friendDB.GetAllFriendsOfUser(player.Data.Id);
            var owners = friends.Select(friend => (long)friend.FriendId).Append((long)player.Data.Id);
            var rankings = await _rankings.ReadBoardAsync(board.Text);
            return Ok(RankingResponses.List(rankings.ForOwners(owners)));
        }

        private async Task<ActionResult> OnGetCalc2Ranking(GrimRequest request)
        {
            if (!request.TryGetParameterByKey("board_id", out var boardIdParam))
            {
                _logger.LogWarning("Got ranking calc2 request without 'board_id'");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("user_id", out var userIdParam))
            {
                _logger.LogWarning("Got ranking calc2 request without 'user_id'");
                return BadRequest();
            }

            long position = await _rankings.FindPositionAsync(userIdParam.Text, boardIdParam.Text);
            return Ok(GrimResult.FromLong(position));
        }

        private async Task<ActionResult> OnGetCalcTemporary(GrimRequest request)
        {
            if (!request.TryGetParameterByKey("board_id", out var boardIdParam))
            {
                _logger.LogWarning("Got ranking calcTemporary request without 'board_id'");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("score", out var scoreParam))
            {
                _logger.LogWarning("Got ranking calcTemporary request without 'score'");
                return BadRequest();
            }

            if (!RankingService.IsValidScore(scoreParam.Text)) return BadRequest();
            long position = await _rankings.PredictPositionAsync(boardIdParam.Text, scoreParam.Text);
            return Ok(GrimResult.FromLong(position));
        }

        private async Task<ActionResult> OnRankingUpdate(Player player, GrimRequest request)
        {
            request.TryGetParameterByKey("board_id", out var board);
            request.TryGetParameterByKey("score", out var score);
            request.TryGetParameterByKey("stats", out var stats);
            var result = await _rankings.SubmitAsync(new RankingSubmission(
                player.Data.Id, player.Data.PSNUserId, player.Data.Country,
                board?.Text, score?.Text, stats?.Text));
            if (result.Status == RankingSaveStatus.Invalid) return BadRequest();
            if (result.Status == RankingSaveStatus.NotImproved)
                _logger.LogDebug("[{name}] Log: Ranking upload request with a higher score. Not updating.", player.Data.PSNUserId);
            else if (result.Status == RankingSaveStatus.Added)
                _logger.LogDebug("[{name}] Log: Ranking upload request", player.Data.PSNUserId);
            else
                _logger.LogDebug("[{name}] Log: Ranking update request", player.Data.PSNUserId);
            return Ok(GrimResult.FromLong(result.Position));
        }
    }
}
