using System.Collections.Generic;
using GrimService.Models;
using Microsoft.Extensions.Logging;

namespace GrimService.Features.PlayerRecords;

internal static class ActionHistoryTelemetry
{
    private sealed record Rule(string Template, int ValueCount, int NumericMask = 0, bool LevelChange = false);


    private static readonly IReadOnlyDictionary<string, Rule> Rules = new Dictionary<string, Rule>
    {
        ["NEW_GAME"] = new("[{player}] started new game", 0),
        ["FRIENDCAR_RIDE"] = new("[{player}] borrowing friend car: {car} from {userid}", 2),
        ["CAR_BUY"] = new("[{player}] bought car: {car}", 1),
        ["CAR_RIDE"] = new("[{player}] now using car: {car}", 1),
        ["ASPEC_EVENT"] = new("[{player}] finished aspec event: game_id:{game_id} course:{v3}", 2, 1),
        ["BSPEC_EVENT"] = new("[{player}] finished bspec event: game_id:{game_id} course:{v3}", 2, 1),
        ["ASPEC_LEVEL"] = new("[{player}] a-spec level: {old}->{new}", 1, 1, true),
        ["BSPEC_LEVEL"] = new("[{player}] b-spec level: {old}->{new}", 1, 1, true),
        ["LICENSE_LEVEL"] = new("[{player}] new license: {level}", 1, 1),
        ["LICENSE_EVENT"] = new("[{player}] finished license event: id:{v2} result:{v3}", 2, 3),
        ["SPECIAL_EVENT"] = new("[{player}] finished special event: event_id:{game_id} course:{v3} result:{result}", 3, 1),
        ["DRIVER_CLASS"] = new("[{player}] driver class: {name} class: {class}", 2),
        ["GIFT_MUSEUMCARD"] = new("[{player}] gifted museum card: id:{museum_id} dealer:{dealer} dealer_id:{dealer_id}", 3),
        ["GIFT_SPECIAL"] = new("[{player}] gifted car ticket: gameitem_id:{gameitem_id} label:{label}", 2),
        ["GIFT_TUNEPARTS"] = new("[{player}] gifted tune parts: gameitem_id:{gameitem_id} horn_id:{horn_id}", 2),
        ["GIFT_OTHERPARTS"] = new("[{player}] gifted paint: gameitem_id:{gameitem_id} color:{color}", 2),
        ["GIFT_DRIVER_ITEM"] = new("[{player}] gifted driver item: gameitem_id:{gameitem_id} code:{code}, color:{v4}", 3),
        ["TROPHY_UNLOCK"] = new("[{player}] unlocked trophy: {type}", 1)
    };

    public static void Write(ILogger logger, Player player, string action, params string[] values)
    {
        if (!Rules.TryGetValue(action, out var rule)) return;
        var arguments = new object[rule.ValueCount + 1];
        arguments[0] = player;
        for (int i = 0; i < rule.ValueCount; i++)
        {
            if ((rule.NumericMask & (1 << i)) == 0) arguments[i + 1] = values[i];
            else
            {
                if (!int.TryParse(values[i], out int number)) return;
                arguments[i + 1] = number;
            }
        }
        if (rule.LevelChange)
        {
            int level = (int)arguments[1];
            arguments = new object[] { player, unchecked(level - 1), level };
        }
        logger.LogInformation(rule.Template, arguments);
    }
}
