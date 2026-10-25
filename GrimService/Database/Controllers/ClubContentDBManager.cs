using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using GrimService.Database.Tables;

namespace GrimService.Database.Controllers
{

    public class ClubContentDBManager
    {
        private readonly IDbConnection _con;
        public ClubContentDBManager(IDbConnection con) => _con = con;

        public void CreateTable() => _con.Execute(@"
            CREATE TABLE IF NOT EXISTS gt6_forum_threads (
                id BIGSERIAL PRIMARY KEY,
                club_id BIGINT NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
                user_id BIGINT NOT NULL REFERENCES users(id),
                title TEXT NOT NULL, comment TEXT NOT NULL,
                activity BOOLEAN NOT NULL DEFAULT FALSE,
                create_time TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                update_time TIMESTAMPTZ NOT NULL DEFAULT NOW());
            CREATE INDEX IF NOT EXISTS gt6_forum_threads_club_idx ON gt6_forum_threads(club_id);
            CREATE TABLE IF NOT EXISTS gt6_forum_comments (
                id BIGSERIAL PRIMARY KEY,
                thread_id BIGINT NOT NULL REFERENCES gt6_forum_threads(id) ON DELETE CASCADE,
                user_id BIGINT NOT NULL REFERENCES users(id),
                comment TEXT NOT NULL,
                photo_id TEXT NOT NULL DEFAULT '',
                delete_user_id TEXT NOT NULL DEFAULT '',
                create_time TIMESTAMPTZ NOT NULL DEFAULT NOW());
            CREATE INDEX IF NOT EXISTS gt6_forum_comments_thread_idx ON gt6_forum_comments(thread_id, id);
            CREATE TABLE IF NOT EXISTS gt6_club_images (
                id BIGINT PRIMARY KEY REFERENCES photos(id) ON DELETE CASCADE,
                club_id BIGINT NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
                thread_id BIGINT REFERENCES gt6_forum_threads(id) ON DELETE CASCADE,
                data BYTEA NOT NULL);");

        public Task<bool> CanReadAsync(long clubId, long userId) => _con.ExecuteScalarAsync<bool>(@"
            SELECT EXISTS(SELECT 1 FROM clubs c WHERE c.id=@ClubId AND
                (c.is_open='1' OR c.user_id=@UserId OR EXISTS(
                    SELECT 1 FROM club_members m WHERE m.club_id=c.id AND
                    (m.user_id=@UserId OR EXISTS(SELECT 1 FROM friends f
                        WHERE f.userid=@UserId AND f.friendid=m.user_id)))))", new { ClubId = clubId, UserId = userId });

        public Task<bool> IsMemberAsync(long clubId, long userId) => _con.ExecuteScalarAsync<bool>(@"
            SELECT EXISTS(SELECT 1 FROM clubs c WHERE c.id=@ClubId AND (c.user_id=@UserId OR
                EXISTS(SELECT 1 FROM club_members m WHERE m.club_id=c.id AND m.user_id=@UserId)))",
            new { ClubId = clubId, UserId = userId });

        public Task<IEnumerable<ClubThreadDTO>> GetThreadsAsync(long clubId, int threadType = 0, long viewer = 0) => _con.QueryAsync<ClubThreadDTO>(@"
            SELECT t.*, u.psn_user_id AS username,
                (SELECT COUNT(*) FROM gt6_forum_comments m WHERE m.thread_id=t.id) AS comment_count
            FROM gt6_forum_threads t JOIN users u ON u.id=t.user_id
            WHERE t.club_id=@ClubId AND t.thread_type=@ThreadType
                AND (t.thread_type=0 OR EXISTS(SELECT 1 FROM gt6_club_events e JOIN clubs c ON c.id=e.club_id
                    WHERE e.thread_id=t.id AND (e.open=1 OR c.user_id=@Viewer OR EXISTS(SELECT 1 FROM club_members m WHERE m.club_id=c.id AND m.user_id=@Viewer))))
            ORDER BY t.update_time DESC, t.id DESC LIMIT 10", new { ClubId = clubId, ThreadType = threadType, Viewer=viewer });

        public Task<bool> ThreadExistsAsync(long clubId, long threadId, long viewer = 0) => _con.ExecuteScalarAsync<bool>(@"
            SELECT EXISTS(SELECT 1 FROM gt6_forum_threads t WHERE id=@ThreadId AND club_id=@ClubId AND
                (t.thread_type=0 OR EXISTS(SELECT 1 FROM gt6_club_events e JOIN clubs c ON c.id=e.club_id WHERE e.thread_id=t.id
                    AND(e.open=1 OR c.user_id=@Viewer OR EXISTS(SELECT 1 FROM club_members m WHERE m.club_id=c.id AND m.user_id=@Viewer)))))",
            new { ClubId = clubId, ThreadId = threadId,Viewer=viewer });

        public Task<IEnumerable<ClubCommentDTO>> GetCommentsAsync(long clubId, long threadId, long cursor = 0, int direction = 0, int count = 100)
        {

            string order = direction > 0 ? "ASC" : "DESC";
            return _con.QueryAsync<ClubCommentDTO>($@"
                SELECT * FROM (SELECT m.*, u.psn_user_id AS username, u.photo_id_avatar AS photo_avatar
                    FROM gt6_forum_comments m JOIN gt6_forum_threads t ON t.id=m.thread_id
                    JOIN users u ON u.id=m.user_id WHERE t.club_id=@ClubId AND t.id=@ThreadId
                    AND (@Direction=0 OR (@Direction>0 AND m.id>@Cursor) OR (@Direction<0 AND m.id<@Cursor))
                    ORDER BY m.id {order} LIMIT @Count) page ORDER BY id ASC",
                new { ClubId = clubId, ThreadId = threadId, Cursor = cursor, Direction = direction, Count = System.Math.Clamp(count, 1, 100) });
        }

        public async Task<long> CreateThreadAsync(long clubId, long userId, string title, string comment, bool activity)
        {
            if (_con.State != ConnectionState.Open) _con.Open();
            using var tx = _con.BeginTransaction();
            var owner = await _con.QuerySingleOrDefaultAsync<long?>("SELECT user_id FROM clubs WHERE id=@ClubId FOR UPDATE", new { ClubId = clubId }, tx);
            if (owner != userId) return -1;
            if (await _con.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM gt6_forum_threads WHERE club_id=@ClubId AND thread_type=0", new { ClubId = clubId }, tx) >= 10) return -1;
            var id = await _con.ExecuteScalarAsync<long>(@"INSERT INTO gt6_forum_threads(club_id,user_id,title,comment,activity)
                VALUES(@ClubId,@UserId,@Title,@Comment,@Activity) RETURNING id", new { ClubId = clubId, UserId = userId, Title = title, Comment = comment, Activity = activity }, tx);
            if(activity)
                await _con.ExecuteAsync(@"INSERT INTO gt6_activity(user_id,club_id,values) SELECT @UserId,@ClubId,
                    ARRAY['BTC',psn_user_id,@ThreadId,@Title,@Comment] FROM users WHERE id=@UserId",
                    new {UserId=userId,ClubId=clubId,ThreadId=id.ToString(),Title=title,Comment=comment},tx);
            tx.Commit();
            return id;
        }

        public async Task<long> CreateCommentAsync(long clubId, long threadId, long userId, string comment, byte[] image = null)
        {
            if (_con.State != ConnectionState.Open) _con.Open();
            using var tx = _con.BeginTransaction();
            var exists = await _con.QuerySingleOrDefaultAsync<long?>(@"SELECT t.id FROM gt6_forum_threads t JOIN clubs c ON c.id=t.club_id
                WHERE t.id=@ThreadId AND t.club_id=@ClubId AND (c.user_id=@UserId OR EXISTS(
                    SELECT 1 FROM club_members m WHERE m.club_id=c.id AND m.user_id=@UserId)) FOR UPDATE OF t",
                new { ClubId = clubId, ThreadId = threadId, UserId = userId }, tx);
            if (exists == null) return -1;
            string photoId = image == null ? "" : (await InsertImageAsync(clubId, threadId, userId, image, tx)).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var id = await _con.ExecuteScalarAsync<long>(@"INSERT INTO gt6_forum_comments(thread_id,user_id,comment,photo_id)
                VALUES(@ThreadId,@UserId,@Comment,@PhotoId) RETURNING id", new { ThreadId = threadId, UserId = userId, Comment = comment, PhotoId = photoId }, tx);
            await _con.ExecuteAsync("UPDATE gt6_forum_threads SET update_time=NOW() WHERE id=@ThreadId", new { ThreadId = threadId }, tx);
            tx.Commit();
            return id;
        }

        public Task<int> DeleteThreadAsync(long clubId, long threadId, long userId) => _con.ExecuteAsync(@"
            DELETE FROM gt6_forum_threads t USING clubs c WHERE t.club_id=c.id
                AND c.id=@ClubId AND t.id=@ThreadId AND c.user_id=@UserId AND t.thread_type=0",
            new { ClubId = clubId, ThreadId = threadId, UserId = userId });

        public Task<int> DeleteCommentAsync(long clubId, long threadId, long commentId, long userId) => _con.ExecuteAsync(@"
            UPDATE gt6_forum_comments m SET comment='', photo_id='', delete_user_id=u.psn_user_id
            FROM gt6_forum_threads t, clubs c, users u
            WHERE m.thread_id=t.id AND t.club_id=c.id AND u.id=@UserId AND c.id=@ClubId
                AND t.id=@ThreadId AND m.id=@CommentId AND (m.user_id=@UserId OR c.user_id=@UserId)",
            new { ClubId = clubId, ThreadId = threadId, CommentId = commentId, UserId = userId });

        private async Task<long> InsertImageAsync(long clubId, long? threadId, long userId, byte[] data, IDbTransaction tx)
        {

            var id = await _con.ExecuteScalarAsync<long>(@"INSERT INTO photos(user_id,username,type,create_time)
                SELECT id,psn_user_id,@Type,NOW() FROM users WHERE id=@UserId RETURNING id",
                new { UserId = userId, Type = threadId == null ? "gt6_club" : "gt6_forum" }, tx);
            await _con.ExecuteAsync(@"INSERT INTO gt6_club_images(id,club_id,thread_id,data) VALUES(@Id,@ClubId,@ThreadId,@Data)",
                new { Id = id, ClubId = clubId, ThreadId = threadId, Data = data }, tx);
            return id;
        }

        public async Task<long> SetClubImageAsync(long clubId, long userId, byte[] data)
        {
            if (_con.State != ConnectionState.Open) _con.Open();
            using var tx = _con.BeginTransaction();
            var owner = await _con.QuerySingleOrDefaultAsync<long?>("SELECT user_id FROM clubs WHERE id=@ClubId FOR UPDATE", new { ClubId = clubId }, tx);
            if (owner != userId) return -1;
            var id = await InsertImageAsync(clubId, null, userId, data, tx);
            await _con.ExecuteAsync("UPDATE clubs SET photo_id=@PhotoId,recent_update_time=NOW() WHERE id=@ClubId",
                new { ClubId = clubId, PhotoId = id.ToString(System.Globalization.CultureInfo.InvariantCulture) }, tx);

            await _con.ExecuteAsync("DELETE FROM photos WHERE id IN (SELECT id FROM gt6_club_images WHERE club_id=@ClubId AND thread_id IS NULL AND id<>@Id)",
                new { ClubId = clubId, Id = id }, tx);
            tx.Commit();
            return id;
        }

        public Task<ClubImageDTO> GetImageAsync(long imageId, long userId) => _con.QuerySingleOrDefaultAsync<ClubImageDTO>(@"
            SELECT i.* FROM gt6_club_images i JOIN clubs c ON c.id=i.club_id WHERE i.id=@ImageId
                AND (NOT EXISTS(SELECT 1 FROM gt6_club_events e WHERE e.thread_id=i.thread_id AND e.open=0)
                    OR c.user_id=@UserId OR EXISTS(SELECT 1 FROM club_members m WHERE m.club_id=c.id AND m.user_id=@UserId))
                AND (c.is_open='1' OR c.user_id=@UserId OR EXISTS(SELECT 1 FROM club_members m WHERE m.club_id=c.id
                    AND (m.user_id=@UserId OR EXISTS(SELECT 1 FROM friends f WHERE f.userid=@UserId AND f.friendid=m.user_id))))
                AND ((i.thread_id IS NULL AND i.event_id IS NULL AND c.photo_id=i.id::text) OR
                    EXISTS(SELECT 1 FROM gt6_club_events e WHERE e.id=i.event_id AND e.photo_id=i.id::text AND
                        (e.open=1 OR c.user_id=@UserId OR EXISTS(SELECT 1 FROM club_members m WHERE m.club_id=c.id AND m.user_id=@UserId))) OR EXISTS(
                    SELECT 1 FROM gt6_forum_comments m WHERE m.thread_id=i.thread_id AND m.photo_id=i.id::text AND m.delete_user_id=''))",
            new { ImageId = imageId, UserId = userId });

        public async Task<long> SetEventImageAsync(long clubId,long eventId,long userId,byte[] data)
        {
            if(_con.State!=ConnectionState.Open)_con.Open();
            using var tx=_con.BeginTransaction();
            var thread=await _con.QuerySingleOrDefaultAsync<long?>(@"SELECT e.thread_id FROM gt6_club_events e JOIN clubs c ON c.id=e.club_id
                WHERE e.id=@EventId AND e.club_id=@ClubId AND c.user_id=@UserId FOR UPDATE OF e",new {EventId=eventId,ClubId=clubId,UserId=userId},tx);
            if(thread==null)return -1;
            var id=await InsertImageAsync(clubId,thread,userId,data,tx);
            await _con.ExecuteAsync("UPDATE gt6_club_images SET event_id=@EventId WHERE id=@Id",new {EventId=eventId,Id=id},tx);
            await _con.ExecuteAsync("UPDATE gt6_club_events SET photo_id=@PhotoId,update_time=NOW() WHERE id=@EventId",new {EventId=eventId,PhotoId=id.ToString()},tx);
            await _con.ExecuteAsync("DELETE FROM photos WHERE id IN(SELECT id FROM gt6_club_images WHERE event_id=@EventId AND id<>@Id)",new {EventId=eventId,Id=id},tx);
            tx.Commit();return id;
        }
    }
}
