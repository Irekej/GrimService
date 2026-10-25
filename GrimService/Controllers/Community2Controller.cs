using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;


using GrimService.Database.Controllers;
using GrimService.Filters;
using GrimService.Models;
using GrimService.Models.Xml;
using GrimService.Config;
using GrimService.Services;

namespace GrimService.Controllers
{
    [ApiController]
    [PDIClient]
    [Route("/ap/[controller]/")]
    [Produces("application/xml")]
    [Authorize]
    public partial class Community2Controller : GrimControllerBase
    {
        private readonly ILogger<Community2Controller> _logger;
        private readonly GT6SocialDBManager _social;
        private readonly GrimService.Features.Clubs.ClubAccessService _clubAccess;
        private readonly GrimService.Features.Clubs.ClubOperationsService _clubOperations;
        private readonly GrimService.Features.Clubs.ClubMembershipService _memberships;
        private readonly GrimService.Features.Clubs.ClubFavoritesService _favorites;
        private readonly GrimService.Features.Clubs.ClubReadService _clubReads;
        private readonly GrimService.Features.Clubs.ClubWriteService _clubWrites;
        private readonly GrimService.Features.Clubs.ClubEventWriteService _eventWrites;
        private readonly GrimService.Features.Clubs.ClubEventActionsService _eventActions;
        private readonly GrimService.Features.Clubs.ClubEventReadService _eventReads;
        private readonly GrimService.Features.Clubs.ClubTemplateService _eventTemplates;

        public Community2Controller(PlayerManager players,
            UserDBManager userDB,
            ClubsDBManager clubsDB,
            FriendDBManager friendDB,
            UserSpecialDBManager userSpecialDB,
            IOptions<GameServerOptions> gsOptions,
            ILogger<Community2Controller> logger, GT6SocialDBManager social = null,
            GrimService.Features.Clubs.ClubAccessService clubAccess = null,
            GrimService.Features.Clubs.ClubOperationsService clubOperations = null,
            GrimService.Features.Clubs.ClubMembershipService memberships = null,
            GrimService.Features.Clubs.ClubFavoritesService favorites = null,
            GrimService.Features.Clubs.ClubReadService clubReads = null,
            GrimService.Features.Clubs.ClubWriteService clubWrites = null,
            GrimService.Features.Clubs.ClubEventWriteService eventWrites = null,
            GrimService.Features.Clubs.ClubEventActionsService eventActions = null,
            GrimService.Features.Clubs.ClubEventReadService eventReads = null,
            GrimService.Features.Clubs.ClubTemplateService eventTemplates = null)
            : base(players)
        {
            _logger = logger;
            _social = social;
            _clubAccess = clubAccess ?? new GrimService.Features.Clubs.ClubAccessService(
                new GrimService.Features.Clubs.ClubAccessRepository(clubsDB, social));
            _clubOperations = clubOperations ?? new GrimService.Features.Clubs.ClubOperationsService(
                _clubAccess, new GrimService.Features.Clubs.ClubOperationsRepository(social));
            _memberships = memberships ?? new GrimService.Features.Clubs.ClubMembershipService(
                new GrimService.Features.Clubs.ClubMembershipRepository(clubsDB, social));
            _favorites = favorites ?? new GrimService.Features.Clubs.ClubFavoritesService(
                new GrimService.Features.Clubs.ClubFavoritesRepository(clubsDB));
            _clubReads = clubReads ?? new GrimService.Features.Clubs.ClubReadService(
                new GrimService.Features.Clubs.ClubReadRepository(clubsDB));
            _clubWrites = clubWrites ?? new GrimService.Features.Clubs.ClubWriteService(
                new GrimService.Features.Clubs.ClubWriteRepository(clubsDB, social));
            _eventWrites = eventWrites ?? new GrimService.Features.Clubs.ClubEventWriteService(
                new GrimService.Features.Clubs.ClubEventWriteRepository(social));
            _eventActions = eventActions ?? new GrimService.Features.Clubs.ClubEventActionsService(
                new GrimService.Features.Clubs.ClubEventActionsRepository(social));
            _eventReads = eventReads ?? new GrimService.Features.Clubs.ClubEventReadService(
                new GrimService.Features.Clubs.ClubEventReadRepository(social));
            _eventTemplates = eventTemplates ?? new GrimService.Features.Clubs.ClubTemplateService(
                new GrimService.Features.Clubs.ClubTemplateRepository(social));
        }

        [HttpPost, RequestSizeLimit(2000000)]
        public async Task<ActionResult> Post()
        {
            var player = Player;
            if (player is null)
            {
                _logger.LogWarning("Could not get current player. IP: {RemoteIpAddress}", Request.HttpContext.Connection.RemoteIpAddress.ToString());
                return Unauthorized();
            }

            GrimRequest gRequest;
            try { gRequest = await GrimRequest.Deserialize(Request.Body); }
            catch (InvalidOperationException) { return BadRequest(GrimResult.FromInt(-1)); }
            if (gRequest?.Params?.ParamList is null)
            {
                var badReq = GrimResult.FromInt(-1);
                return BadRequest(badReq);
            }

            _logger.LogDebug("<- {command}", gRequest.Command);

            if (gRequest.Command != null && (gRequest.Command.StartsWith("event.", StringComparison.Ordinal) ||
                gRequest.Command.StartsWith("event_template.", StringComparison.Ordinal) || gRequest.Command.StartsWith("event_result.", StringComparison.Ordinal) ||
                gRequest.Command == "community.delete" || gRequest.Command == "community.leave" || gRequest.Command == "community.getFriendList" || gRequest.Command == "community.invite"))
                return await OnSocialRequest(player, gRequest);

            switch (gRequest.Command)
            {
                case "community.getListUser":
                    return await OnListUserGet(player, gRequest);
                case "community.getListRecommend":
                    return await OnListRecommend(player, gRequest);
                case "community.getListNew":
                    return await OnListNew(player, gRequest);
                case "community.getIdListUser":
                    return await OnIdListUserGet(player, gRequest);
                case "community.create":
                    return await OnCreateClub(player, gRequest);
                case "community.join":
                    return await OnJoinClub(player, gRequest);
                case "community.setActive":
                case "community.activate":
                    return await OnJoinClub(player, gRequest);
                case "community.get":
                    return await OnGetClub(player, gRequest);
                case "community.update":
                    return await OnUpdateClub(player, gRequest);
                case "favorite.getList":
                    return await OnFavoriteGetList(player, gRequest);
                case "favorite.add":
                    return await OnFavoriteChange(player, gRequest, true);
                case "favorite.delete":
                    return await OnFavoriteChange(player, gRequest, false);
                case "event.getListUser":
                    return await OnListEventGet(player, gRequest);
                case "event.getList":
                    return await OnListEventGet(player, gRequest);
            }

            _logger.LogDebug("Received unimplemented profile call: {command}", gRequest.Command);
            var res = GrimResult.FromInt(-1);
            return BadRequest(res);
        }

        private async Task<ActionResult> OnIdListUserGet(Player player, GrimRequest request)
        {
            if (!request.TryGetParameterByKey("user_id", out var username) || username.Text.Length > 32)
                return BadRequest();
            return GrimService.Controllers.Clubs.ClubListResponses.Memberships(
                await _clubReads.MembershipsAsync(username.Text, player.Data.Id), username.Text);
        }

        private async Task<ActionResult> OnListUserGet(Player player, GrimRequest request)
        {
            if (!request.TryGetParameterByKey("user_id", out var username) || username.Text.Length > 32)
                return BadRequest();
            return GrimService.Controllers.Clubs.ClubListResponses.List(
                await _clubReads.MembershipsAsync(username.Text, player.Data.Id));
        }

        private Task<ActionResult> OnListEventGet(Player player, GrimRequest request)
            => OnSocialRequest(player, request);

        private Task<ActionResult> OnListRecommend(Player player, GrimRequest request)
            => OnListNew(player, request);

        private async Task<ActionResult> OnListNew(Player player, GrimRequest request)
            => GrimService.Controllers.Clubs.ClubListResponses.List(await _clubReads.PublicAsync(player.Data.Id));

        private async Task<ActionResult> OnCreateClub(Player player, GrimRequest request)
        {
            if (player == null) return Unauthorized();
            request.TryGetParameterByKey("title", out var title);
            request.TryGetParameterByKey("description", out var description);
            request.TryGetParameterByKey("tag_csv", out var tags);
            request.TryGetParameterByKey("open", out var open);
            request.TryGetParameterByKey("recent", out var recent);
            request.TryGetParameterByKey("image_id", out var image);
            try
            {
                var result = await _clubWrites.CreateAsync(
                    new GrimService.Features.Clubs.ClubEditor(player.Data.Id, player.Data.PSNUserId, player.Data.Country, player.Data.LastGame),
                    new GrimService.Features.Clubs.ClubCreateDraft(title?.Text, description?.Text, tags?.Text, open?.Text, recent?.Text, image?.Text));
                return result.Status == GrimService.Features.Clubs.ClubWriteStatus.Saved
                    ? Ok(GrimResult.FromLong(result.PublicId)) : BadRequest();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not create club for {Player}", player.Data.PSNUserId);
                return BadRequest();
            }
        }

        private async Task<ActionResult> OnJoinClub(Player player, GrimRequest request)
        {
            if (Player == null)
            {
                return Unauthorized();
            }

            if (!request.TryGetParameterLongByKey("community_id", out var communityIdParam))
            {
                _logger.LogWarning("Got community.join request without 'community_id'");
                return BadRequest();
            }

            var clubId = ToDatabaseClubId(communityIdParam);
            var result = await _memberships.JoinAsync(clubId, player.Data.Id, player.Data.PSNUserId);
            if (result == GrimService.Features.Clubs.ClubJoinStatus.MissingClub)
            {
                _logger.LogWarning("Player {Player} attempted to join unknown club {CommunityId}", player.Data.PSNUserId, communityIdParam);
                return NotFound(GrimResult.FromInt(-1));
            }
            if (result == GrimService.Features.Clubs.ClubJoinStatus.MembershipNotVisible)
                return StatusCode(500, GrimResult.FromInt(-1));
            _logger.LogInformation("Player {Player} joined and activated club {ClubId}", player.Data.PSNUserId, clubId);
            return Ok(GrimResult.FromInt(1));
        }

        private async Task<ActionResult> OnGetClub(Player player, GrimRequest request)
        {
            if (Player == null) return Unauthorized();
            if (!request.TryGetParameterLongByKey("community_id", out var publicId)) return BadRequest();
            return GrimService.Controllers.Clubs.ClubListResponses.List(
                await _clubReads.ByIdAsync(ToDatabaseClubId(publicId), player.Data.Id));
        }

        private async Task<ActionResult> OnUpdateClub(Player player, GrimRequest request)
        {
            if (Player == null) return Unauthorized();
            if (!request.TryGetParameterLongByKey("community_id", out var publicId)) return BadRequest();
            request.TryGetParameterByKey("title", out var title);
            request.TryGetParameterByKey("description", out var description);
            request.TryGetParameterByKey("open", out var open);
            request.TryGetParameterByKey("tag_csv", out var tags);
            request.TryGetParameterByKey("recent", out var recent);
            var result = await _clubWrites.UpdateAsync(ToDatabaseClubId(publicId),
                new GrimService.Features.Clubs.ClubEditor(player.Data.Id, player.Data.PSNUserId, player.Data.Country, player.Data.LastGame),
                new GrimService.Features.Clubs.ClubUpdateDraft(title?.Text, description?.Text, open?.Text, tags?.Text, recent?.Text));
            if (result.Status == GrimService.Features.Clubs.ClubWriteStatus.Invalid) return BadRequest();
            return result.Status == GrimService.Features.Clubs.ClubWriteStatus.Saved ? Ok(GrimResult.FromInt(1)) : Forbid();
        }

        private const long CommunityIdOffset = GrimService.Features.Clubs.ClubIdentity.Offset;

        private static long ToDatabaseClubId(long communityId)
            => GrimService.Features.Clubs.ClubIdentity.ToDatabase(communityId);

        private async Task<ActionResult> OnFavoriteGetList(Player player, GrimRequest request)
        {
            if (!request.TryGetParameterByKey("user_id", out var username) || username.Text.Length > 32)
                return BadRequest();
            var favorites = await _favorites.ListAsync(player.Data.Id, player.Data.PSNUserId, username.Text);
            if (!favorites.Allowed) return Forbid();
            return GrimService.Controllers.Clubs.ClubListResponses.List(
                await _clubReads.MapAsync(favorites.Clubs, player.Data.Id));
        }

        private async Task<ActionResult> OnFavoriteChange(Player player, GrimRequest request, bool add)
        {
            if (!request.TryGetParameterLongByKey("community_id", out var publicId))
                return BadRequest(GrimResult.FromInt(-1));

            var result = await _favorites.ChangeAsync(ToDatabaseClubId(publicId), player.Data.Id, add);
            if (result == GrimService.Features.Clubs.ClubFavoriteStatus.InvalidId)
                return BadRequest(GrimResult.FromInt(-1));
            if (result == GrimService.Features.Clubs.ClubFavoriteStatus.MissingClub)
                return Ok(GrimResult.FromInt(-1));
            return Ok(GrimResult.FromInt(1));
        }


    }

}
