using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GrimService.Database.Controllers;
using GrimService.Database.Tables;
using GrimService.Models;
using GrimService.Models.Xml;
using GrimService.Utils;
using Microsoft.Extensions.Logging;

namespace GrimService.Features.PlayerRecords;

public interface IActionHistoryRepository
{
    Task<IEnumerable<ActionLogDTO>> Read(int userId);
    Task Append(ActionLogDTO entry);
}

public sealed class ActionHistoryRepository(ActionLogDBManager database) : IActionHistoryRepository
{
    public Task<IEnumerable<ActionLogDTO>> Read(int userId) => database.GetAllActionsOfUser(userId);
    public async Task Append(ActionLogDTO entry) => await database.AddAsync(entry);
}

public enum ActionHistoryStatus { Saved, Ignored, InvalidRequest }

public sealed class ActionHistoryService(IActionHistoryRepository repository, ILogger logger, TimeProvider clock)
{
    private readonly ILogger _logger = logger;

    public async Task<ActionHistoryStatus> Record(Player player, GrimRequest request, Action onAccepted)
    {
        if (!request.TryGetParameterIntByKey("action_type", out _))
            return ActionHistoryStatus.InvalidRequest;
        var values = new string[5];
        for (int i = 0; i < values.Length; i++)
        {
            if (!request.TryGetParameterByKey("value" + (i + 1), out var param))
                return ActionHistoryStatus.InvalidRequest;
            values[i] = param.Text;
        }
        if (!ActionLog.Tags.TryGetValue(values[0], out var action))
            return ActionHistoryStatus.Ignored;

        ActionHistoryTelemetry.Write(_logger, player, action, values[1], values[2], values[3], values[4]);
        var entry = new ActionLogDTO(player.Data.Id, DateTime.SpecifyKind(clock.GetLocalNow().DateTime, DateTimeKind.Local),
            values[0], values[1], values[2], values[3], values[4]);
        onAccepted();
        await repository.Append(entry);
        return ActionHistoryStatus.Saved;
    }

    public async Task<bool> WriteList(int userId, Stream destination, CancellationToken cancellationToken)
    {
        var entries = await repository.Read(userId);
        if (entries == null) return false;


        await using var writer = new StreamWriter(destination, new UTF8Encoding(false), 1024, leaveOpen: true);
        foreach (var row in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string line = $"<actionlog create_time=\"{row.CreateTime.ToRfc3339String()}\" value1=\"{row.Value1}\" value2=\"{row.Value2}\" value3=\"{row.Value3}\" value4=\"{row.Value4}\" value5=\"{row.Value5}\"/>\n";
            await writer.WriteAsync(line.AsMemory(), cancellationToken);
        }
        await writer.FlushAsync(cancellationToken);
        return true;
    }

}
