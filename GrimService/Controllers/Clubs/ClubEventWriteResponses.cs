using System;
using GrimService.Features.Clubs;
using GrimService.Models.Xml;
using Microsoft.AspNetCore.Mvc;

namespace GrimService.Controllers.Clubs
{
    public static class ClubEventWriteResponses
    {
        public static ActionResult ToResponse(ClubEventWriteResult result) => result.Status switch
        {
            ClubEventWriteStatus.Invalid => new BadRequestResult(),
            ClubEventWriteStatus.Forbidden => new ForbidResult(),
            ClubEventWriteStatus.Saved => new OkObjectResult(GrimResult.FromLong(result.Value)),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }
}
