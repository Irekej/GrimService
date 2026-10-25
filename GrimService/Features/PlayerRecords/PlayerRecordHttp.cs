using System;
using System.Threading.Tasks;
using System.Xml;
using GrimService.Controllers;
using GrimService.Controllers.Services;
using GrimService.Database.Controllers;
using GrimService.Models;
using GrimService.Models.Xml;
using GrimService.Services;

namespace GrimService.Features.PlayerRecords;

internal static class PlayerRecordHttp
{
    public static async Task<Player> Recover(GrimControllerBase controller, UserDBManager users, PlayerManager players)
    {
        if (controller.Player != null) return controller.Player;
        var token = controller.Request.Cookies["X-gt-token"];
        if (string.IsNullOrWhiteSpace(token)) return null;
        if (!await new SessionRecover(users, players).CheckIfPlayerExist(token)) return null;
        return controller.Player;
    }

    public static async Task<GrimRequest> Read(GrimControllerBase controller)
    {
        try { return await GrimRequest.Deserialize(controller.Request.Body, controller.HttpContext.RequestAborted); }
        catch (InvalidOperationException) { return null; }
        catch (XmlException) { return null; }
    }
}
