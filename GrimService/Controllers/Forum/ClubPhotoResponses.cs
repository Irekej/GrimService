using System;
using GrimService.Features.ClubPhotos;
using GrimService.Models.Xml;
using Microsoft.AspNetCore.Mvc;

namespace GrimService.Controllers.Forum
{
    public static class ClubPhotoResponses
    {
        public static ActionResult ToResponse(ClubPhotoResult result) => result.Status switch
        {
            ClubPhotoStatus.Uploaded => new OkObjectResult(GrimResult.FromLong(result.Id)),
            ClubPhotoStatus.Invalid => new BadRequestResult(),
            ClubPhotoStatus.Forbidden => new ForbidResult(),
            ClubPhotoStatus.TooLarge => new StatusCodeResult(413),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }
}
