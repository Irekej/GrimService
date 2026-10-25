using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using System;
using System.Threading.Tasks;

using GrimService.Config;
using GrimService.Services;
using GrimService.Models;
using GrimService.Models.Xml;
using GrimService.Filters;
using GrimService.Database.Tables;
using GrimService.Features.Replays;

namespace GrimService.Controllers
{



    [ApiController]
    [PDIClient]
    [Authorize]
    [Route("/ap/[controller]/")]
    [Produces("application/xml")]
    public class ReplayController : GrimControllerBase
    {
        private readonly ILogger<ReplayController> _logger;
        private readonly GameServerOptions _gsOptions;
        private readonly ReplayTransferService _transfers;

        public ReplayController(PlayerManager players,
            IOptions<GameServerOptions> options,
            ILogger<ReplayController> logger,
            ReplayTransferService transfers)
            : base(players)
        {
            _logger = logger;
            _gsOptions = options.Value;
            _transfers = transfers;
        }

        [HttpPost]
        [Route("/ap/replay")]
        public async Task<ActionResult> Post(string server)
        {
            var player = Player;
            if (player is null)
            {
                _logger.LogWarning("Could not get current player. IP: {RemoteIpAddress}", Request.HttpContext.Connection.RemoteIpAddress);
                return Unauthorized();
            }

            GrimRequest gRequest = await GrimRequest.Deserialize(Request.Body);
            if (gRequest is null)
                return BadRequest();

            _logger.LogDebug("[{name}] Log: <- {command}", Player.Data.PSNUserId, gRequest.Command);

            switch (gRequest.Command)
            {
                case "photo.getlist":
                    return OnRequestGetList(player, gRequest);
            }

            _logger.LogDebug("Got unimplemented photo call: {command}", gRequest.Command);


            return BadRequest();
        }

        private ActionResult OnRequestGetList(Player player, GrimRequest gRequest)
        {
            if (_gsOptions.GameType != GameType.GT6)
            {
                _logger.LogWarning("Got photo.getlist request on a non GT6 server");
                return BadRequest();
            }

            if (!gRequest.TryGetParameterByKey("user_id", out var userIdParam) || userIdParam.Text.Length > 32)
            {
                _logger.LogWarning("Got photo.getlist request with missing parameter: user_id");
                return BadRequest();
            }

            if (!gRequest.TryGetParameterByKey("type", out var typeParam))
            {
                _logger.LogWarning("Got photo.getlist request with missing parameter: type");
                return BadRequest();
            }


            return NotFound();

        }

        [HttpGet]
        [Route("/replay/{replay_id}.dat")]
        [Produces("application/octet-stream")]
        public async Task<ActionResult> GetReplayFile(string replay_id)
        {
            if (Player == null) return Unauthorized();
            if (!long.TryParse(replay_id, out var id) || id <= 0) return BadRequest();
            var stream = await _transfers.OpenAsync(id);
            if (stream == null) return NotFound();

            Response.ContentLength = stream.Length;
            _logger.LogDebug("Replay download: id={ReplayId}, bytes={Length}", id, stream.Length);
            return File(stream, "application/octet-stream", enableRangeProcessing: true);
        }

        [HttpGet]
        [Route("/replays/{replay_id2}.dat")]
        [Produces("application/octet-stream")]
        public Task<ActionResult> GetReplayFile1(string replay_id2) => GetReplayFile(replay_id2);

        [HttpPost]
        [Route("/ap/replay/upload")]
        [RequestSizeLimit(ReplayTransferService.MaxUploadBytes)]
        public async Task<ActionResult> OnRequestUploadReplay()
        {
            Player player = Player;
            if (player is null)
            { 
                _logger.LogWarning("Could not get current player. IP: {RemoteIpAddress}", Request.HttpContext.Connection.RemoteIpAddress);
                return Unauthorized();
            }

            var value = Request.Headers["X-gt-xml"];
            if (value.Count != 1)
                return BadRequest();

            string xml = value[0];
            if (string.IsNullOrEmpty(xml))
                return BadRequest();

            GrimRequest gRequest;
            try { gRequest = GrimRequest.Deserialize(xml); }
            catch (InvalidOperationException) { return BadRequest(); }
            if (gRequest?.Params?.ParamList is null)
                return BadRequest();

            if (gRequest.Command != "rankingReplay.put")
                return BadRequest();

            if (!gRequest.TryGetParameterByKey("board_id", out var boardidParam))
            {
                _logger.LogWarning($"Got rankingReplay.put without board_id.");
                return BadRequest();
            }
            if (!gRequest.TryGetParameterByKey("is_short", out var isshortParam))
            {

                if (player.Data.LastGame == "GT5") isshortParam = new GrimRequestParam { Key="is_short", Text="0" };
                else
                {
                _logger.LogWarning($"Got rankingReplay.put without is_short.");
                return BadRequest();
                }
            }
            if (!long.TryParse(boardidParam.Text, out var board) || board <= 0 ||
                (isshortParam.Text != "0" && isshortParam.Text != "1")) return BadRequest();

            if (Request.HasFormContentType) return StatusCode(StatusCodes.Status415UnsupportedMediaType);
            var replay = new ReplayDTO(player.Data.Id, player.Data.PSNUserId, boardidParam.Text, isshortParam.Text, DateTime.UtcNow);
            var result = await _transfers.UploadAsync(replay, Request.Body, Request.ContentLength, HttpContext.RequestAborted);
            switch (result.Status)
            {
                case ReplayUploadStatus.MissingRanking: return NotFound();
                case ReplayUploadStatus.InvalidPayload: return BadRequest();
                case ReplayUploadStatus.TooLarge: return StatusCode(413);
                case ReplayUploadStatus.StaleRanking: return Conflict(GrimResult.FromInt(-1));
                case ReplayUploadStatus.Saved:
                    _logger.LogInformation("Replay uploaded: user={User}, id={Id}, short={Short}, bytes={Length}",
                        player.Data.PSNUserId, result.Id, isshortParam.Text, result.Length);
                    return Ok(GrimResult.FromLong(result.Id));
                default: throw new InvalidOperationException("Unknown replay upload result.");
            }
        }
    }
}
