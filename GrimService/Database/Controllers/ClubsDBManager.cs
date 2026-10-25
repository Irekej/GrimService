using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.Data;
using System.Net;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;
using Dapper;
using Npgsql;

using Microsoft.Extensions.DependencyInjection;
using GrimService.Database.Tables;

namespace GrimService.Database.Controllers
{
    public class ClubsDBManager : IDBManager<ClubsDTO>
    {
        private ILogger<ClubsDBManager> _logger;
        protected IDbConnection _con;

        public ClubsDBManager(ILogger<ClubsDBManager> logger, IDbConnection con)
        {
            _logger = logger;
            _con = con;
        }

        public async Task<ClubsDTO> GetByIDAsync(long id)
            => await _con.QueryFirstOrDefaultAsync<ClubsDTO>(@"SELECT * FROM clubs WHERE id = @Id", new { Id = id });






        public async Task<IEnumerable<ClubsDTO>> GetAllClubsOfUser(string userId)
            => await _con.QueryAsync<ClubsDTO>(@"SELECT c.* FROM clubs c
                INNER JOIN club_members cm ON cm.club_id = c.id
                INNER JOIN users u ON u.id = cm.user_id
                WHERE u.psn_user_id = @Username
                ORDER BY cm.is_active DESC, c.id ASC", new { Username = userId });

        public async Task<IEnumerable<ClubsDTO>> GetClubById(long id)
            => await _con.QueryAsync<ClubsDTO>(@"SELECT * FROM clubs WHERE id=@Id", new { Id = id });


        public async Task<IEnumerable<ClubsDTO>> GetAllClubs()
            => await _con.QueryAsync<ClubsDTO>(@"SELECT * FROM clubs ORDER BY id ASC");

        public async Task<IEnumerable<ClubsDTO>> GetPublicClubsAsync()
            => await _con.QueryAsync<ClubsDTO>(@"SELECT * FROM clubs
                WHERE is_open = '1' ORDER BY create_time DESC NULLS LAST, id DESC");

        public async Task<IEnumerable<ClubsDTO>> GetFavoriteClubsAsync(long userId)
            => await _con.QueryAsync<ClubsDTO>(@"SELECT c.* FROM clubs c
                INNER JOIN club_favorites f ON f.club_id = c.id
                WHERE f.user_id = @UserId
                ORDER BY f.created_at DESC, c.id DESC", new { UserId = userId });

        public async Task<bool> AddFavoriteAsync(long clubId, long userId)
            => await _con.ExecuteScalarAsync<bool>(@"WITH target AS (
                    SELECT id FROM clubs WHERE id = @ClubId
                ), inserted AS (
                    INSERT INTO club_favorites (club_id, user_id)
                    SELECT id, @UserId FROM target
                    ON CONFLICT (user_id, club_id) DO NOTHING RETURNING club_id
                ) SELECT EXISTS(SELECT 1 FROM target)", new { ClubId = clubId, UserId = userId });



        public async Task RemoveFavoriteAsync(long clubId, long userId)
            => await _con.ExecuteAsync(@"DELETE FROM club_favorites
                WHERE club_id = @ClubId AND user_id = @UserId", new { ClubId = clubId, UserId = userId });

        public async Task<IReadOnlyList<string>> GetMemberNamesAsync(long clubId)
            => (await _con.QueryAsync<string>(@"SELECT u.psn_user_id FROM club_members cm
                INNER JOIN users u ON u.id = cm.user_id
                WHERE cm.club_id = @ClubId ORDER BY cm.joined_at ASC", new { ClubId = clubId })).ToList();

        public async Task<bool> IsActiveForUserAsync(long clubId, long userId)
            => await _con.ExecuteScalarAsync<bool>(@"SELECT EXISTS(SELECT 1 FROM club_members WHERE club_id = @ClubId AND user_id = @UserId AND is_active)", new { ClubId = clubId, UserId = userId });

        public Task<int> GetFriendCountAsync(long clubId, long userId) => _con.ExecuteScalarAsync<int>(@"
            SELECT COUNT(*) FROM club_members m WHERE m.club_id=@ClubId AND m.user_id<>@UserId
                AND EXISTS(SELECT 1 FROM friends f WHERE f.userid=@UserId AND f.friendid=m.user_id)",
            new { ClubId = clubId, UserId = userId });

        public Task<string> GetClubPhotoIdAsync(long clubId) => _con.QuerySingleOrDefaultAsync<string>(@"
            SELECT c.photo_id FROM clubs c JOIN gt6_club_images i ON c.photo_id=i.id::text
                AND i.club_id=c.id AND i.thread_id IS NULL WHERE c.id=@ClubId", new { ClubId = clubId });

        public Task<int> UpdateClubDetailsAsync(long clubId, long ownerId, string title, string description, string open, string[] tags, string recent = null)
            => _con.ExecuteAsync(@"UPDATE clubs SET title=COALESCE(@Title,title), description=COALESCE(@Description,description), is_open=COALESCE(@Open,is_open),
                recent=COALESCE(@Recent,recent),
                tag_1=COALESCE(@Tag1,tag_1),tag_2=COALESCE(@Tag2,tag_2),tag_3=COALESCE(@Tag3,tag_3),
                tag_4=COALESCE(@Tag4,tag_4),tag_5=COALESCE(@Tag5,tag_5),tag_6=COALESCE(@Tag6,tag_6),recent_update_time=NOW()
                WHERE id=@ClubId AND user_id=@OwnerId", new { ClubId = clubId, OwnerId = ownerId, Title = title, Description = description, Open = open,
                    Recent = recent,
                    Tag1 = tags == null ? null : tags.ElementAtOrDefault(0) ?? "", Tag2 = tags == null ? null : tags.ElementAtOrDefault(1) ?? "", Tag3 = tags == null ? null : tags.ElementAtOrDefault(2) ?? "",
                    Tag4 = tags == null ? null : tags.ElementAtOrDefault(3) ?? "", Tag5 = tags == null ? null : tags.ElementAtOrDefault(4) ?? "", Tag6 = tags == null ? null : tags.ElementAtOrDefault(5) ?? "" });

        public async Task<bool> JoinAndActivateAsync(long clubId, long userId)
        {
            if (await GetByIDAsync(clubId) == null)
                return false;

            if (_con.State != ConnectionState.Open)
                _con.Open();
            using var transaction = _con.BeginTransaction();
            try
            {

                await _con.ExecuteAsync("SELECT id FROM users WHERE id = @UserId FOR UPDATE", new { UserId = userId }, transaction);
                await _con.ExecuteAsync(@"INSERT INTO club_members (club_id, user_id, is_active)
                    VALUES (@ClubId, @UserId, FALSE) ON CONFLICT (club_id, user_id) DO NOTHING", new { ClubId = clubId, UserId = userId }, transaction);
                await _con.ExecuteAsync("UPDATE club_members SET is_active = FALSE WHERE user_id = @UserId", new { UserId = userId }, transaction);
                await _con.ExecuteAsync("UPDATE club_members SET is_active = TRUE WHERE club_id = @ClubId AND user_id = @UserId", new { ClubId = clubId, UserId = userId }, transaction);
                transaction.Commit();
                return true;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task UpdateAsync(ClubsDTO pData)
            => await _con.ExecuteAsync(@"UPDATE clubs SET psnuserid=@PSNUserId, country=@Country, title=@Title, description=@Description, recent=@Recent, tag_1=@Tag_1, tag_2=@Tag_2, tag_3=@Tag_3, tag_4=@Tag_4, tag_5=@Tag_5, tag_6=@Tag_6, is_open=@Is_Open, photo_id=@PhotoId, recent_update_time=@RecentUpdateTime WHERE id=@Id AND user_id=@UserId", pData);

        public async Task<long> AddAsync(ClubsDTO pData)
        {
            var query =
@"INSERT INTO clubs (user_id, psnuserid, country, title, description, recent, tag_1, tag_2, tag_3, tag_4, tag_5, tag_6, is_open, photo_id, recent_update_time, create_time)
  VALUES
(@UserId, @PSNUserId, @Country, @Title, @Description, @Recent, @Tag_1, @Tag_2, @Tag_3, @Tag_4, @Tag_5, @Tag_6, @Is_Open, @PhotoId, @RecentUpdateTime, @CreateTime)
  returning id";

            try
            {
                var clubId = await _con.ExecuteScalarAsync<long>(query, new { pData.UserId, pData.PSNUserId, pData.Country, pData.Title, pData.Description, pData.Recent, pData.Tag_1, pData.Tag_2, pData.Tag_3, pData.Tag_4, pData.Tag_5, pData.Tag_6, pData.Is_Open, pData.PhotoId, pData.RecentUpdateTime, pData.CreateTime });
                await JoinAndActivateAsync(clubId, pData.UserId);
                return clubId;
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Failed to addasync club. EX: {ex}");
                return 0;
            }
        }

        public async Task RemoveAsync(long id)
            => await _con.ExecuteAsync(@"DELETE FROM clubs WHERE id=@Id", new { Id = id });

        public void CreateTable()
        {
            string query =
            @"CREATE TABLE IF NOT EXISTS clubs (
                id BIGSERIAL PRIMARY KEY,
				user_id BIGINT REFERENCES users(id),
                psnuserid TEXT,
                country TEXT,
                title TEXT,
                description TEXT,
                recent TEXT,
                tag_1 TEXT,
                tag_2 TEXT,
                tag_3 TEXT,
                tag_4 TEXT,
                tag_5 TEXT,
                tag_6 TEXT,
                is_open TEXT,
                photo_id TEXT,
                recent_update_time TIMESTAMP WITH TIME ZONE,
                create_time TIMESTAMP WITH TIME ZONE
			);";

            _con.Execute(query);

            string query2 = @"CREATE INDEX IF NOT EXISTS clubs_user_id_idx ON clubs (user_id)";
            _con.Execute(query2);

            _con.Execute(@"CREATE TABLE IF NOT EXISTS club_members (
                club_id BIGINT NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
                user_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                is_active BOOLEAN NOT NULL DEFAULT FALSE,
                joined_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                PRIMARY KEY (club_id, user_id)
            )");
            _con.Execute(@"INSERT INTO club_members (club_id, user_id, is_active)
                SELECT c.id, c.user_id, FALSE
                FROM clubs c WHERE c.user_id IS NOT NULL
                ON CONFLICT (club_id, user_id) DO NOTHING");
            _con.Execute(@"WITH ranked_members AS (
                    SELECT club_id, user_id, ROW_NUMBER() OVER (PARTITION BY user_id ORDER BY joined_at DESC, club_id DESC) AS number
                    FROM club_members WHERE is_active
                )
                UPDATE club_members cm SET is_active = (ranked_members.number = 1)
                FROM ranked_members WHERE cm.club_id = ranked_members.club_id AND cm.user_id = ranked_members.user_id");
            _con.Execute(@"CREATE UNIQUE INDEX IF NOT EXISTS club_members_one_active_per_user_idx ON club_members (user_id) WHERE is_active");
            _con.Execute(@"CREATE TABLE IF NOT EXISTS club_favorites (
                user_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                club_id BIGINT NOT NULL REFERENCES clubs(id) ON DELETE CASCADE,
                created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                PRIMARY KEY (user_id, club_id)
            )");
            _con.Execute(@"CREATE INDEX IF NOT EXISTS club_favorites_club_id_idx ON club_favorites (club_id)");
        }
    }
}
