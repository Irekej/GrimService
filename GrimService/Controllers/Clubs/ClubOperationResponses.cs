using System;
using System.Linq;
using System.Xml.Linq;
using GrimService.Features.Clubs;
using GrimService.Models.Xml;
using Microsoft.AspNetCore.Mvc;

namespace GrimService.Controllers.Clubs
{
    public static class ClubOperationResponses
    {
        public static bool TryGetOperation(string command, out ClubOperation operation)
        {
            switch (command)
            {
                case "community.invite": operation = ClubOperation.Invite; return true;
                case "community.delete": operation = ClubOperation.Delete; return true;
                case "community.leave": operation = ClubOperation.Leave; return true;
                case "community.getFriendList": operation = ClubOperation.ListFriends; return true;
                default: operation = default; return false;
            }
        }

        public static ActionResult ToResponse(ClubOperationResult result, long communityIdOffset)
        {
            switch (result.Status)
            {
                case ClubOperationStatus.Invalid: return new BadRequestResult();
                case ClubOperationStatus.Forbidden: return new ForbidResult();
                case ClubOperationStatus.MissingClub: return new NotFoundObjectResult(GrimResult.FromInt(-1));
                case ClubOperationStatus.Rejected: return new BadRequestObjectResult(GrimResult.FromInt(-1));
                case ClubOperationStatus.Completed:
                    if (result.Friends == null) return new OkObjectResult(GrimResult.FromInt(result.Value));
                    return new ContentResult
                    {
                        ContentType = "application/xml",
                        Content = new XElement("club_user_list", result.Friends.Select(user => new XElement("community_user",
                            new XAttribute("community_id", result.ClubId + communityIdOffset),
                            new XAttribute("user_id", user), new XAttribute("level", 1)))).ToString(SaveOptions.DisableFormatting)
                    };
                default: throw new ArgumentOutOfRangeException(nameof(result));
            }
        }
    }
}
