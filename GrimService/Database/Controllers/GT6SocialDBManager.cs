using System;
using System.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using GrimService.Database.Tables;

namespace GrimService.Database.Controllers
{
    public class GT6SocialDBManager
    {
        private readonly IDbConnection _con;
        public GT6SocialDBManager(IDbConnection con) => _con = con;
        public void CreateTable() => _con.Execute(@"
            ALTER TABLE gt6_forum_threads ADD COLUMN IF NOT EXISTS thread_type INTEGER NOT NULL DEFAULT 0;
            CREATE TABLE IF NOT EXISTS gt6_activity (
                id BIGSERIAL PRIMARY KEY, user_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                club_id BIGINT REFERENCES clubs(id) ON DELETE CASCADE, values TEXT[] NOT NULL,
                create_time TIMESTAMPTZ NOT NULL DEFAULT NOW());
            CREATE INDEX IF NOT EXISTS gt6_activity_user_idx ON gt6_activity(user_id,id);
            CREATE INDEX IF NOT EXISTS gt6_activity_club_idx ON gt6_activity(club_id,id);
            CREATE TABLE IF NOT EXISTS gt6_club_invites (
                club_id BIGINT NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
                user_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                sender_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(), PRIMARY KEY(club_id,user_id));
            CREATE TABLE IF NOT EXISTS gt6_club_events (
                id BIGSERIAL PRIMARY KEY, club_id BIGINT NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
                thread_id BIGINT NOT NULL UNIQUE REFERENCES gt6_forum_threads(id) ON DELETE CASCADE,
                title TEXT NOT NULL, description TEXT NOT NULL, regulation TEXT NOT NULL,
                open INTEGER NOT NULL DEFAULT 1, mode INTEGER NOT NULL DEFAULT 1, ranking_board BIGINT NOT NULL DEFAULT 0,
                photo_id TEXT NOT NULL DEFAULT '', start_time TIMESTAMPTZ NOT NULL, end_time TIMESTAMPTZ NOT NULL,
                create_time TIMESTAMPTZ NOT NULL DEFAULT NOW(), update_time TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                CHECK(end_time>start_time));
            CREATE INDEX IF NOT EXISTS gt6_events_club_idx ON gt6_club_events(club_id);
            CREATE TABLE IF NOT EXISTS gt6_event_members (
                event_id BIGINT REFERENCES gt6_club_events(id) ON DELETE CASCADE,
                user_id BIGINT REFERENCES users(id) ON DELETE CASCADE, PRIMARY KEY(event_id,user_id));
            CREATE TABLE IF NOT EXISTS gt6_event_invites (
                event_id BIGINT REFERENCES gt6_club_events(id) ON DELETE CASCADE,
                user_id BIGINT REFERENCES users(id) ON DELETE CASCADE,
                sender_id BIGINT NOT NULL REFERENCES users(id), PRIMARY KEY(event_id,user_id));
            CREATE TABLE IF NOT EXISTS gt6_event_templates (
                id BIGSERIAL PRIMARY KEY, club_id BIGINT NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
                description TEXT NOT NULL, regulation TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS gt6_templates_club_idx ON gt6_event_templates(club_id);
            CREATE TABLE IF NOT EXISTS gt6_event_results (
                id BIGSERIAL PRIMARY KEY, event_id BIGINT NOT NULL REFERENCES gt6_club_events(id) ON DELETE CASCADE,
                user_id BIGINT NOT NULL REFERENCES users(id), result TEXT NOT NULL, create_time TIMESTAMPTZ NOT NULL DEFAULT NOW());
            CREATE INDEX IF NOT EXISTS gt6_results_event_idx ON gt6_event_results(event_id,id);
            ALTER TABLE gt6_club_images ADD COLUMN IF NOT EXISTS event_id BIGINT REFERENCES gt6_club_events(id) ON DELETE CASCADE;");

        public Task AddActivityAsync(long userId, long? clubId, string[] values) => _con.ExecuteAsync(
            "INSERT INTO gt6_activity(user_id,club_id,values) VALUES(@UserId,@ClubId,@Values)", new { UserId=userId, ClubId=clubId, Values=values });
        public Task<IEnumerable<GT6ActivityDTO>> ActivityAsync(long userId) => _con.QueryAsync<GT6ActivityDTO>(@"
            SELECT a.*,u.psn_user_id AS username FROM gt6_activity a JOIN users u ON u.id=a.user_id
            LEFT JOIN clubs c ON c.id=a.club_id WHERE (
              (a.club_id IS NULL AND (a.user_id=@UserId OR EXISTS(SELECT 1 FROM friends f WHERE f.userid=@UserId AND f.friendid=a.user_id))) OR
              (c.id IS NOT NULL AND (c.user_id=@UserId OR EXISTS(SELECT 1 FROM club_members m WHERE m.club_id=c.id AND m.user_id=@UserId)
               OR (c.is_open='1' AND (a.user_id=@UserId OR EXISTS(SELECT 1 FROM friends f WHERE f.userid=@UserId AND f.friendid=a.user_id))))))
            AND (a.values[1] NOT IN ('EC','EU','CEE') OR EXISTS(SELECT 1 FROM gt6_club_events e WHERE
                (e.id+1000000)::text=CASE WHEN a.values[1]='CEE' THEN a.values[4] ELSE a.values[3] END
                AND (e.open=1 OR c.user_id=@UserId OR EXISTS(SELECT 1 FROM club_members m WHERE m.club_id=e.club_id AND m.user_id=@UserId))))
            AND (a.values[1]<>'BTC' OR EXISTS(SELECT 1 FROM gt6_forum_threads t WHERE t.id::text=a.values[3] AND t.club_id=a.club_id))
            ORDER BY a.id DESC LIMIT 100", new { UserId=userId });
        public Task<bool> OwnerAsync(long clubId,long userId) => _con.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM clubs WHERE id=@ClubId AND user_id=@UserId)",new {ClubId=clubId,UserId=userId});
        public async Task<int> DeleteClubAsync(long clubId,long userId)
        {
            if (_con.State!=ConnectionState.Open) _con.Open();
            using var tx=_con.BeginTransaction();
            var owner=await _con.QuerySingleOrDefaultAsync<long?>("SELECT user_id FROM clubs WHERE id=@ClubId FOR UPDATE",new {ClubId=clubId},tx);
            if(owner!=userId) return 0;
            await _con.ExecuteAsync("DELETE FROM photos WHERE id IN (SELECT id FROM gt6_club_images WHERE club_id=@ClubId)",new {ClubId=clubId},tx);
            var count=await _con.ExecuteAsync("DELETE FROM clubs WHERE id=@ClubId",new {ClubId=clubId},tx);
            tx.Commit(); return count;
        }
        public Task<int> LeaveClubAsync(long clubId,long userId) => _con.ExecuteAsync(@"
            WITH left_club AS (DELETE FROM club_members m USING clubs c WHERE m.club_id=c.id AND c.id=@ClubId AND m.user_id=@UserId AND c.user_id<>@UserId RETURNING m.club_id)
            DELETE FROM gt6_event_members m USING gt6_club_events e WHERE m.event_id=e.id AND m.user_id=@UserId AND e.club_id IN (SELECT club_id FROM left_club)",
            new {ClubId=clubId,UserId=userId});
        public Task<IEnumerable<string>> FriendsAsync(long clubId,long userId,long eventId=0) => _con.QueryAsync<string>(@"
            SELECT u.psn_user_id FROM users u JOIN club_members m ON m.user_id=u.id WHERE m.club_id=@ClubId AND u.id<>@UserId
            AND EXISTS(SELECT 1 FROM friends f WHERE f.userid=@UserId AND f.friendid=u.id)
            AND (@EventId=0 OR EXISTS(SELECT 1 FROM gt6_event_members em WHERE em.event_id=@EventId AND em.user_id=u.id)) ORDER BY u.psn_user_id",
            new {ClubId=clubId,UserId=userId,EventId=eventId});

        public Task<bool> IsClubInvitedAsync(long clubId,long userId) => _con.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM gt6_club_invites WHERE club_id=@clubId AND user_id=@userId)",new {clubId,userId});

        public Task<int> InviteClubAsync(long clubId,long sender,string target) => _con.ExecuteAsync(@"
            INSERT INTO gt6_club_invites(club_id,user_id,sender_id)
            SELECT c.id,u.id,@sender FROM clubs c JOIN users u ON u.psn_user_id=@target
            WHERE c.id=@clubId AND u.id<>@sender
              AND (c.user_id=@sender OR (c.is_open='1' AND EXISTS(
                  SELECT 1 FROM club_members m WHERE m.club_id=c.id AND m.user_id=@sender)))
              AND EXISTS(SELECT 1 FROM friends f WHERE f.userid=@sender AND f.friendid=u.id)
            ON CONFLICT(club_id,user_id) DO UPDATE SET sender_id=EXCLUDED.sender_id,created_at=NOW()",
            new {clubId,sender,target});

        public Task<IEnumerable<ClubEventDTO>> EventsAsync(long clubId,long viewer,long eventId=0,string username=null) => _con.QueryAsync<ClubEventDTO>(@"
            SELECT e.*, (SELECT COUNT(*) FROM gt6_event_members m WHERE m.event_id=e.id) AS user_count,
              (SELECT COUNT(*) FROM gt6_event_members m WHERE m.event_id=e.id AND m.user_id<>@Viewer AND EXISTS(
                SELECT 1 FROM friends f WHERE f.userid=@Viewer AND f.friendid=m.user_id)) AS friend_count
            FROM gt6_club_events e JOIN clubs c ON c.id=e.club_id
            WHERE (@ClubId=0 OR e.club_id=@ClubId) AND (@EventId=0 OR e.id=@EventId)
            AND (@Username IS NULL OR EXISTS(SELECT 1 FROM gt6_event_members m JOIN users u ON u.id=m.user_id WHERE m.event_id=e.id AND u.psn_user_id=@Username))
            AND (c.user_id=@Viewer OR EXISTS(SELECT 1 FROM club_members m WHERE m.club_id=c.id AND m.user_id=@Viewer)
                OR (e.open=1 AND (c.is_open='1' OR EXISTS(SELECT 1 FROM club_members m JOIN friends f ON f.friendid=m.user_id WHERE m.club_id=c.id AND f.userid=@Viewer))))
            ORDER BY e.start_time DESC,e.id DESC LIMIT 100",new {ClubId=clubId,Viewer=viewer,EventId=eventId,Username=username});
        public async Task<long> SaveEventAsync(ClubEventDTO e,long userId,bool create,bool activity,string bbsComment="")
        {
            if(_con.State!=ConnectionState.Open) _con.Open();
            using var tx=_con.BeginTransaction();
            var owner=await _con.QuerySingleOrDefaultAsync<long?>("SELECT user_id FROM clubs WHERE id=@ClubId FOR UPDATE",new {e.ClubId},tx);
            if(owner!=userId) return -1;
            if(create)
            {
                if(await _con.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM gt6_club_events WHERE club_id=@ClubId",new {e.ClubId},tx)>=10) return -1;
                e.ThreadId=await _con.ExecuteScalarAsync<long>(@"INSERT INTO gt6_forum_threads(club_id,user_id,title,comment,thread_type)
                    VALUES(@ClubId,@UserId,@Title,@Comment,1) RETURNING id",new {e.ClubId,UserId=userId,e.Title,Comment=bbsComment},tx);
                e.Id=await _con.ExecuteScalarAsync<long>(@"INSERT INTO gt6_club_events(club_id,thread_id,title,description,regulation,open,mode,ranking_board,start_time,end_time)
                    VALUES(@ClubId,@ThreadId,@Title,@Description,@Regulation,@Open,@Mode,@RankingBoard,@StartTime,@EndTime) RETURNING id",e,tx);
            }
            else
            {
                if(await _con.ExecuteAsync(@"UPDATE gt6_club_events SET title=@Title,description=@Description,regulation=@Regulation,open=@Open,
                    start_time=@StartTime,end_time=@EndTime,update_time=NOW() WHERE id=@Id AND club_id=@ClubId",e,tx)==0) return -1;
                await _con.ExecuteAsync("UPDATE gt6_forum_threads SET title=@Title,update_time=NOW() WHERE id=@ThreadId",e,tx);
            }
            if(create||activity)
                await _con.ExecuteAsync(@"INSERT INTO gt6_activity(user_id,club_id,values) SELECT @UserId,@ClubId,
                    ARRAY[@Kind,psn_user_id,@PublicId,@Title,@Description] FROM users WHERE id=@UserId",
                    new {UserId=userId,e.ClubId,Kind=create?"EC":"EU",PublicId=(e.Id+1000000).ToString(),e.Title,e.Description},tx);
            tx.Commit();return e.Id;
        }
        public async Task<int> DeleteEventAsync(long clubId,long eventId,long userId)
        {
            if(_con.State!=ConnectionState.Open) _con.Open();
            using var tx=_con.BeginTransaction();
            var thread=await _con.QuerySingleOrDefaultAsync<long?>(@"SELECT e.thread_id FROM gt6_club_events e JOIN clubs c ON c.id=e.club_id
                WHERE e.id=@EventId AND c.id=@ClubId AND c.user_id=@UserId FOR UPDATE OF e",new {ClubId=clubId,EventId=eventId,UserId=userId},tx);
            if(thread==null)return 0;
            await _con.ExecuteAsync("DELETE FROM photos WHERE id IN (SELECT id FROM gt6_club_images WHERE event_id=@EventId OR thread_id=@Thread)",new {EventId=eventId,Thread=thread},tx);
            await _con.ExecuteAsync("DELETE FROM gt6_forum_threads WHERE id=@Thread",new {Thread=thread},tx);
            tx.Commit();return 1;
        }
        public async Task<bool> EventMembershipAsync(long clubId,long eventId,long userId,bool join)
        {
            if(!join)
            {
                await _con.ExecuteAsync(@"DELETE FROM gt6_event_members m USING gt6_club_events e WHERE m.event_id=e.id AND e.club_id=@ClubId AND e.id=@EventId AND m.user_id=@UserId",
                    new {ClubId=clubId,EventId=eventId,UserId=userId});return true;
            }
            return await _con.ExecuteScalarAsync<bool>(@"WITH target AS (SELECT e.id FROM gt6_club_events e JOIN club_members m ON m.club_id=e.club_id
                    WHERE e.club_id=@ClubId AND e.id=@EventId AND m.user_id=@UserId AND e.end_time>NOW()), ins AS (
                    INSERT INTO gt6_event_members(event_id,user_id) SELECT id,@UserId FROM target ON CONFLICT DO NOTHING RETURNING event_id), activity AS (
                    INSERT INTO gt6_activity(user_id,club_id,values) SELECT @UserId,c.id,ARRAY['CEE',(c.id+1000000)::text,c.title,(e.id+1000000)::text,e.title,e.description,e.photo_id]
                    FROM ins JOIN gt6_club_events e ON e.id=ins.event_id JOIN clubs c ON c.id=e.club_id)
                    SELECT EXISTS(SELECT 1 FROM target)",new {ClubId=clubId,EventId=eventId,UserId=userId});
        }
        public Task<IEnumerable<ClubTemplateDTO>> TemplatesAsync(long clubId,long id=0) => _con.QueryAsync<ClubTemplateDTO>(
            "SELECT * FROM gt6_event_templates WHERE club_id=@ClubId AND (@Id=0 OR id=@Id) ORDER BY id LIMIT 10",new {ClubId=clubId,Id=id});
        public async Task<long> SaveTemplateAsync(long clubId,long userId,long id,string description,string regulation,bool delete=false)
        {
            if(_con.State!=ConnectionState.Open)_con.Open();
            using var tx=_con.BeginTransaction();
            if(await _con.QuerySingleOrDefaultAsync<long?>("SELECT user_id FROM clubs WHERE id=@ClubId FOR UPDATE",new {ClubId=clubId},tx)!=userId)return -1;
            var p=new {ClubId=clubId,Id=id,Description=description,Regulation=regulation};
            if(delete) {var n=await _con.ExecuteAsync("DELETE FROM gt6_event_templates WHERE club_id=@ClubId AND id=@Id",p,tx);tx.Commit();return n>0?1:-1;}
            if(id==0)
            {
                if(await _con.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM gt6_event_templates WHERE club_id=@ClubId",p,tx)>=10)return -1;
                id=await _con.ExecuteScalarAsync<long>("INSERT INTO gt6_event_templates(club_id,description,regulation) VALUES(@ClubId,@Description,@Regulation) RETURNING id",p,tx);
            }
            else if(await _con.ExecuteAsync("UPDATE gt6_event_templates SET description=@Description,regulation=@Regulation WHERE club_id=@ClubId AND id=@Id",p,tx)==0)return -1;
            tx.Commit();return id;
        }
        public Task<IEnumerable<ClubResultDTO>> ResultsAsync(long clubId,long eventId) => _con.QueryAsync<ClubResultDTO>(@"
            SELECT r.*,e.club_id,u.psn_user_id AS username FROM gt6_event_results r JOIN gt6_club_events e ON e.id=r.event_id
            JOIN users u ON u.id=r.user_id WHERE e.club_id=@ClubId AND e.id=@EventId ORDER BY r.id DESC LIMIT 100",new {ClubId=clubId,EventId=eventId});
        public Task<long> AddResultAsync(long clubId,long eventId,long userId,string result) => _con.ExecuteScalarAsync<long>(@"
            INSERT INTO gt6_event_results(event_id,user_id,result) SELECT e.id,@UserId,@Result FROM gt6_club_events e
            WHERE e.club_id=@ClubId AND e.id=@EventId AND EXISTS(SELECT 1 FROM gt6_event_members m WHERE m.event_id=e.id AND m.user_id=@UserId)
            RETURNING id",new {ClubId=clubId,EventId=eventId,UserId=userId,Result=result});
        public Task<int> DeleteResultAsync(long clubId,long eventId,long resultId,long userId) => _con.ExecuteAsync(@"
            DELETE FROM gt6_event_results r USING gt6_club_events e,clubs c WHERE r.event_id=e.id AND e.club_id=c.id
            AND c.id=@ClubId AND e.id=@EventId AND r.id=@ResultId AND c.user_id=@UserId",new {ClubId=clubId,EventId=eventId,ResultId=resultId,UserId=userId});
        public Task<int> InviteAsync(long eventId,long sender,string target) => _con.ExecuteAsync(@"
            INSERT INTO gt6_event_invites(event_id,user_id,sender_id)
            SELECT e.id,u.id,@Sender FROM gt6_club_events e JOIN users u ON u.psn_user_id=@Target
            WHERE e.id=@EventId AND EXISTS(SELECT 1 FROM club_members m WHERE m.club_id=e.club_id AND m.user_id=@Sender)
            AND EXISTS(SELECT 1 FROM friends f WHERE f.userid=@Sender AND f.friendid=u.id)
            ON CONFLICT(event_id,user_id) DO UPDATE SET sender_id=EXCLUDED.sender_id",
            new {EventId=eventId,Sender=sender,Target=target});
    }
}
