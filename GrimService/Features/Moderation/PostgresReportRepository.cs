using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using GrimService.Services;

namespace GrimService.Features.Moderation
{
    public sealed class PostgresReportRepository : IReportRepository
    {
        private readonly string connectionString;
        public PostgresReportRepository(IDbConnection db) => connectionString = db.ConnectionString;
        private NpgsqlConnection Connect() => new NpgsqlConnection(connectionString);
        public void CreateTable()
        {
            using var db = Connect();
            db.Execute(@"CREATE TABLE IF NOT EXISTS moderation_reports (
                id BIGSERIAL PRIMARY KEY, game TEXT NOT NULL CHECK(game IN ('GT5','GT6')),
                reporter_id BIGINT NOT NULL, reporter TEXT NOT NULL, target TEXT NOT NULL,
                payload TEXT NOT NULL, fingerprint TEXT NOT NULL,
                status TEXT NOT NULL DEFAULT 'open' CHECK(status IN ('open','reviewing','resolved','dismissed')),
                note TEXT NOT NULL DEFAULT '', version INTEGER NOT NULL DEFAULT 1,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW());
                CREATE INDEX IF NOT EXISTS moderation_reports_queue ON moderation_reports(game,status,id DESC);
                CREATE INDEX IF NOT EXISTS moderation_reports_sender ON moderation_reports(reporter_id,created_at);
                CREATE TABLE IF NOT EXISTS moderation_report_audit (
                    id BIGSERIAL PRIMARY KEY, report_id BIGINT NOT NULL REFERENCES moderation_reports(id),
                    actor TEXT NOT NULL, old_status TEXT NOT NULL, new_status TEXT NOT NULL,
                    old_note TEXT NOT NULL, new_note TEXT NOT NULL, created_at TIMESTAMPTZ NOT NULL DEFAULT NOW());");
        }

        public async Task<long> SubmitAsync(ReportSubmission report)
        {
            var (game, reporterId, reporter, target, payload, fingerprint) = report;
            using var db = Connect(); await db.OpenAsync(); using var tx = db.BeginTransaction();
            await db.ExecuteAsync("SELECT id FROM users WHERE id=@reporterId FOR UPDATE", new { reporterId }, tx);
            var duplicate = await db.QueryFirstOrDefaultAsync<long?>(@"SELECT id FROM moderation_reports
                WHERE reporter_id=@reporterId AND game=@game AND fingerprint=@fingerprint
                AND created_at > NOW()-INTERVAL '24 hours' ORDER BY id DESC LIMIT 1", new { reporterId, game, fingerprint }, tx);
            if (duplicate.HasValue) { tx.Commit(); return duplicate.Value; }
            var count = await db.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM moderation_reports
                WHERE reporter_id=@reporterId AND created_at > NOW()-INTERVAL '24 hours'", new { reporterId }, tx);
            if (count >= 50) { tx.Commit(); return -1; }
            var id = await db.ExecuteScalarAsync<long>(@"INSERT INTO moderation_reports(game,reporter_id,reporter,target,payload,fingerprint)
                VALUES(@game,@reporterId,@reporter,@target,@payload,@fingerprint) RETURNING id",
                new { game, reporterId, reporter, target, payload, fingerprint }, tx);
            tx.Commit(); return id;
        }

        public async Task<IEnumerable<ModerationReport>> ListAsync(string game, int page)
        {
            using var db = Connect();
            return await db.QueryAsync<ModerationReport>(@"SELECT id,game,reporter,target,payload,status,note,version,created_at AS CreatedAt
                FROM moderation_reports WHERE game=@game ORDER BY id DESC LIMIT 50 OFFSET @offset",
                new { game, offset = (Math.Clamp(page, 1, 100000) - 1) * 50 });
        }

        public async Task<bool> ReviewAsync(long id, int version, string status, string note, string actor)
        {
            using var db = Connect(); await db.OpenAsync(); using var tx = db.BeginTransaction();
            var previous = await db.QuerySingleOrDefaultAsync<ModerationReport>(
                "SELECT id,status,note,version FROM moderation_reports WHERE id=@id FOR UPDATE", new { id }, tx);
            if (previous == null || previous.Version != version) return false;
            await db.ExecuteAsync("UPDATE moderation_reports SET status=@status,note=@note,version=version+1 WHERE id=@id", new { id, status, note }, tx);
            await db.ExecuteAsync(@"INSERT INTO moderation_report_audit(report_id,actor,old_status,new_status,old_note,new_note)
                VALUES(@id,@actor,@oldStatus,@status,@oldNote,@note)",
                new { id, actor, oldStatus = previous.Status, status, oldNote = previous.Note, note }, tx);
            tx.Commit(); return true;
        }

        public async Task<IEnumerable<ReportDecision>> HistoryAsync(long id)
        {
            using var db = Connect();
            return await db.QueryAsync<ReportDecision>(@"SELECT actor,old_status AS OldStatus,new_status AS NewStatus,
                old_note AS OldNote,new_note AS NewNote,created_at AS CreatedAt
                FROM moderation_report_audit WHERE report_id=@id ORDER BY id DESC LIMIT 100", new { id });
        }
    }
}
