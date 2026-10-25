using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using GrimService.Controllers.Services;
using GrimService.Database.Controllers;
using GrimService.Filters;
using GrimService.Models;
using GrimService.Models.Xml;
using GrimService.Services;
using GrimService.Features.Forum;
using GrimService.Controllers.Forum;
using GrimService.Features.ClubPhotos;

namespace GrimService.Controllers
{
    [ApiController, PDIClient, Authorize]
    [Route("/ap/bbs2/")]
    [Produces("application/xml")]
    public class Bbs2Controller : GrimControllerBase
    {
        private readonly UserDBManager _users;
        private readonly ILogger<Bbs2Controller> _logger;
        private readonly ClubForumService _forum;
        private readonly ClubPhotoService _photos;
        public Bbs2Controller(PlayerManager players, ClubContentDBManager db, UserDBManager users, ILogger<Bbs2Controller> logger, ClubForumService forum = null, ClubPhotoService photos = null) : base(players)
        {
            _users = users; _logger = logger;
            _forum = forum ?? new ClubForumService(new ClubForumRepository(db));
            _photos = photos ?? new ClubPhotoService(new ClubPhotoRepository(db));
        }

        private async Task<Player> GetGT6PlayerAsync()
        {
            if (Player == null)
            {
                var token = Request.Cookies["X-gt-token"];
                if (string.IsNullOrEmpty(token) || !await new SessionRecover(_users, Players).CheckIfPlayerExist(token)) return null;
            }
            var player = Player;
            return player?.Data.LastGame == "GT6" && player.Token != null && player.Token.ExpiryDate > DateTime.Now ? player : null;
        }

        private static long ClubId(long id) => id >= 1000000 ? id - 1000000 : id;
        [HttpPost, RequestSizeLimit(65536)]
        public async Task<ActionResult> Post()
        {
            var player = await GetGT6PlayerAsync();
            if (player == null) return Unauthorized();
            GrimRequest request;
            try { request = await GrimRequest.Deserialize(Request.Body); }
            catch (InvalidOperationException) { return BadRequest(); }
            if (request?.Params?.ParamList == null || !request.TryGetParameterLongByKey("board_id", out var board)) return BadRequest();
            var clubId = ClubId(board);
            int? type = request.TryGetParameterIntByKey("thread_type",out var threadType) ? threadType : null;
            request.TryGetParameterIntByKey("latest_comment",out var latest);
            request.TryGetParameterIntByKey("activity",out var activity);
            long? thread = request.TryGetParameterLongByKey("thread_id",out var threadId) ? threadId : null;
            long? commentId = request.TryGetParameterLongByKey("comment_id",out var cursor) ? cursor : null;
            request.TryGetParameterByKey("title",out var title);
            request.TryGetParameterByKey("comment",out var comment);
            var result = await _forum.ExecuteAsync(new ForumRequest(request.Command,clubId,player.Data.Id,type,latest,
                thread,commentId,title?.Text,comment?.Text,activity));
            if(result.Status!=ForumStatus.Forbidden)
                _logger.LogDebug("GT6 <- {Command}, club={ClubId}, user={User}",request.Command,board,player.Data.PSNUserId);
            if(result.Status==ForumStatus.Unsupported)
                _logger.LogWarning("Unsupported GT6 forum command {Command}",request.Command);
            return ClubForumResponses.ToResponse(result);
        }

        [HttpPost("/ap/community2/photo_upload"), RequestSizeLimit(GTConstants.MaxPhotoSize)]
        public Task<ActionResult> UploadClubPhoto() => UploadPhotoAsync(false);

        [HttpPost("/ap/bbs2/photo_upload"), RequestSizeLimit(GTConstants.MaxPhotoSize)]
        public Task<ActionResult> UploadCommentPhoto() => UploadPhotoAsync(true);

        [HttpPost("/ap/community2/event_photo_upload"), RequestSizeLimit(GTConstants.MaxPhotoSize)]
        public Task<ActionResult> UploadEventPhoto() => UploadPhotoAsync(false, true);

        private async Task<ActionResult> UploadPhotoAsync(bool forum, bool eventPhoto = false)
        {
            var player = await GetGT6PlayerAsync();
            if (player == null) return Unauthorized();
            var header = Request.Headers["X-gt-xml"];
            if (header.Count != 1 || string.IsNullOrEmpty(header[0])) return BadRequest();
            GrimRequest request;
            try { request = GrimRequest.Deserialize(header[0]); }
            catch (InvalidOperationException) { return BadRequest(); }
            if (request?.Params?.ParamList == null || request.Command != "photo.upload" ||
                !request.TryGetParameterLongByKey(forum ? "board_id" : "community_id", out var board)) return BadRequest();
            var clubId = ClubId(board);
            long? threadId = request.TryGetParameterLongByKey("thread_id",out var thread) ? thread : null;
            long? eventId = request.TryGetParameterLongByKey("community_event_id",out var eventValue) ? eventValue : null;
            request.TryGetParameterByKey("comment",out var comment);
            var kind = forum ? ClubPhotoKind.Comment : eventPhoto ? ClubPhotoKind.Event : ClubPhotoKind.Club;
            var result = await _photos.UploadAsync(new ClubPhotoRequest(clubId,player.Data.Id,kind,threadId,eventId,comment?.Text),
                Request.Body,HttpContext.RequestAborted);
            if(result.Status==ClubPhotoStatus.Uploaded)
                _logger.LogDebug("GT6 {Kind} photo upload: club={ClubId}, result={Id}",forum ? "forum" : "club",board,result.Id);
            return ClubPhotoResponses.ToResponse(result);
        }
    }
}
