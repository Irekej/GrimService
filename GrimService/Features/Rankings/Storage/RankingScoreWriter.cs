using System;
using System.Globalization;
using System.Threading.Tasks;
using Dapper;
using GrimService.Database.Tables;
using GrimService.Features.Rankings;
using Npgsql;

namespace GrimService.Features.Rankings.Storage;

public sealed class RankingScoreWriter(string connectionString)
{
    public async Task<RankingSaveStatus> SaveBestAsync(RankingsDTO submission)
    {
        if (submission == null || string.IsNullOrWhiteSpace(submission.BoardId) || submission.Stats == null ||
            !RankingService.IsValidScore(submission.Score)) return RankingSaveStatus.Invalid;


        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
        string key = "grim:ranking:" + submission.UserId.ToString(CultureInfo.InvariantCulture) + ":" + submission.BoardId;
        await connection.ExecuteAsync("SELECT pg_advisory_xact_lock(hashtextextended(@Key, 0))", new { Key = key }, transaction);
        var existing = await connection.QueryFirstOrDefaultAsync<RankingsDTO>(@"SELECT * FROM rankings
            WHERE user_id=@UserId AND board_id=@BoardId ORDER BY score::numeric, id LIMIT 1 FOR UPDATE", submission, transaction);
        if (existing != null && decimal.Parse(submission.Score, CultureInfo.InvariantCulture) > decimal.Parse(existing.Score, CultureInfo.InvariantCulture))
        {
            await transaction.CommitAsync();
            return RankingSaveStatus.NotImproved;
        }

        if (existing == null)
            await connection.ExecuteAsync(@"INSERT INTO rankings(user_id,username,country,board_id,score,stats,create_time)
                VALUES (@UserId,@Username,@Country,@BoardId,@Score,@Stats,@CreateTime)", submission, transaction);
        else
            await connection.ExecuteAsync(@"UPDATE rankings SET score=@Score,stats=@Stats,
                replay_id=CASE WHEN score IS DISTINCT FROM @Score OR stats IS DISTINCT FROM @Stats THEN NULL ELSE replay_id END,
                replay_mark_id=CASE WHEN score IS DISTINCT FROM @Score OR stats IS DISTINCT FROM @Stats THEN NULL ELSE replay_mark_id END
                WHERE id=@Id AND user_id=@UserId AND board_id=@BoardId",
                new { existing.Id, submission.UserId, submission.BoardId, submission.Score, submission.Stats }, transaction);
        await transaction.CommitAsync();
        return existing == null ? RankingSaveStatus.Added : RankingSaveStatus.Updated;
    }
}
