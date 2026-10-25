using System;
using System.Collections.Generic;
using GrimService.Features.Diagnostics;

namespace GrimService.Helpers;

public class LogHelper
{

    public static Dictionary<string, string> GT6Codes = ClientLogCatalog.CreateTelemetryCodes();
    public static Dictionary<string, string> RoomConfigTextCodes = ClientLogCatalog.CreateRoomCodes();

    public static string[] Humanify(string[] args) => ClientLogDecoder.ExpandInPlace(args, GT6Codes);
}
