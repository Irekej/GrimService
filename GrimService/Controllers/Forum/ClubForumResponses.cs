using System;
using GrimService.Features.Forum;
using GrimService.Models.Xml;
using Microsoft.AspNetCore.Mvc;

namespace GrimService.Controllers.Forum
{
    public static class ClubForumResponses
    {
        public static ActionResult ToResponse(ForumResult result) => result.Status switch
        {
            ForumStatus.Completed => new OkObjectResult(GrimResult.FromLong(result.Value)),
            ForumStatus.Xml => new ContentResult { Content = result.Xml, ContentType = "application/xml" },
            ForumStatus.Invalid => new BadRequestResult(),
            ForumStatus.Forbidden => new ForbidResult(),
            ForumStatus.MissingThread => new NotFoundObjectResult(GrimResult.FromInt(-1)),
            ForumStatus.Unsupported => new BadRequestObjectResult(GrimResult.FromInt(-1)),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }
}
