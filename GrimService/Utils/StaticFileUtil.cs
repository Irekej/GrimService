using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using GrimService.Features.Resources;

namespace GrimService.Utils
{
    public static class StaticFileUtil
    {
        public static Task SendFile(this ControllerBase controller, string root, string relativePath) =>
            new GameResourceStore(root).CopyAsync(controller.HttpContext, relativePath);
    }
}
