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
    public class ReplayDBManager : IDBManager<ReplayDTO>
    {
        private ILogger<ReplayDBManager> _logger;
        protected IDbConnection _con;

        public ReplayDBManager(ILogger<ReplayDBManager> logger, IDbConnection con)
        {
            _logger = logger;
            _con = con;
        }

        public async Task<ReplayDTO> GetByIDAsync(long id)
            => await _con.QueryFirstOrDefaultAsync<ReplayDTO>(@"SELECT * FROM replays WHERE id = @Id", new { Id = id });

        public async Task<IEnumerable<ReplayDTO>> GetAllForAdminAsync(long userId)
            => await _con.QueryAsync<ReplayDTO>(@"SELECT * FROM replays WHERE user_id = @UserId ORDER BY create_time DESC NULLS LAST", new { UserId = userId });


        public async Task<IEnumerable<ReplayDTO>> GetAllRankings(string boardId)
            => await _con.QueryAsync<ReplayDTO>(@"SELECT * FROM rankings WHERE board_id=@BoardId ORDER BY score ASC", new { BoardId = boardId });

        public async Task<IEnumerable<ReplayDTO>> GetAllRankingsOfUser(string userId, string boardId)
            => await _con.QueryAsync<ReplayDTO>(@"SELECT * FROM rankings WHERE board_id=@BoardId ORDER BY score ASC", new { UserId = userId, BoardId = boardId });


        public async Task<IEnumerable<ReplayDTO>> GetAllRankingsFriends(string boardId)
            => await _con.QueryAsync<ReplayDTO>(@"SELECT * FROM rankings WHERE board_id=@BoardId ORDER BY score ASC", new { BoardId = boardId });

        public async Task<IEnumerable<ReplayDTO>> GetScoreRankingOfPlayer(string userId, string boardId)
            => await _con.QueryAsync<ReplayDTO>(@"SELECT score FROM rankings WHERE username=@UserId AND board_id=@BoardId ORDER BY score ASC", new { UserId = userId, BoardId = boardId });

        public async Task<long> GetPlayerRankingPosition(string userId, string boardId)
        {
            var query = @"
        SELECT CAST(ROW_NUMBER() OVER (ORDER BY score ASC) AS BIGINT) AS position
        FROM rankings
        WHERE board_id = @BoardId AND username = @UserId;";

            return await _con.ExecuteScalarAsync<long>(query, new { UserId = userId, BoardId = boardId });
        }

        public async Task<ReplayDTO> GetRankingByPlayerIdAndBoardId(long playerId, string boardId)
        {
            var query = @"SELECT * FROM rankings WHERE user_id=@PlayerId AND board_id=@BoardId";
            var result = await _con.QuerySingleOrDefaultAsync<ReplayDTO>(query, new { PlayerId = playerId, BoardId = boardId });
            return result;
        }

        public async Task<long> CountAllRankingsBoard(string boardId)
        {
            var countQuery = @"SELECT COUNT(*) FROM rankings WHERE board_id=@BoardId";
            var count = await _con.ExecuteScalarAsync<long>(countQuery, new { BoardId = boardId });
            return count;
        }

        public async Task<ReplayDTO> GetRankingByUserIdAndBoardId(long userId, string boardId)
        {
            var query = @"SELECT * FROM rankings WHERE user_id=@UserId AND board_id=@BoardId";
            var result = await _con.QuerySingleOrDefaultAsync<ReplayDTO>(query, new { UserId = userId, BoardId = boardId });
            return result;
        }

        public async Task UpdateAsync(ReplayDTO pData)
            => await _con.ExecuteAsync(@"UPDATE replays SET username=@Username, board_id=@BoardId, is_short=@IsShort, create_time=@CreateTime WHERE id=@Id AND user_id=@UserId", pData);

        public async Task<long> AddAsync(ReplayDTO pData)
        {
            var query =
@"INSERT INTO replays (user_id, username, board_id, is_short, create_time)
  VALUES
(@UserId, @Username, @BoardId, @IsShort, @CreateTime)
  returning id";

            return await _con.ExecuteScalarAsync<long>(query, new { pData.UserId, pData.Username, pData.BoardId, pData.IsShort, pData.CreateTime });
        }

        public async Task RemoveAsync(long id)
            => await _con.ExecuteAsync(@"DELETE FROM replays WHERE id=@id", new { Id = id });

        public async Task RemoveForAdminAsync(long userId, long replayId)
            => await _con.ExecuteAsync(@"DELETE FROM replays WHERE user_id = @UserId AND id = @Id", new { UserId = userId, Id = replayId });







        public async Task RemoveCourseAsync(long userId, long courseId)
           => await _con.ExecuteAsync(@"DELETE FROM rankings WHERE userid=@UserId AND id=@Id", new { UserId = userId, Id = courseId });

        public void CreateTable()
        {
            string query =
            @"CREATE TABLE IF NOT EXISTS replays (
                id SERIAL PRIMARY KEY,
				user_id BIGINT REFERENCES users(id),
                username TEXT,
                board_id TEXT,
                is_short TEXT,
                create_time TIMESTAMP WITHOUT TIME ZONE
			);";

            _con.Execute(query);

            string query2 = @"CREATE INDEX IF NOT EXISTS replays_user_id_idx ON replays (user_id)";
            _con.Execute(query2);



            _con.Execute(@"UPDATE rankings k SET replay_mark_id=COALESCE(k.replay_mark_id,k.replay_id), replay_id=NULL
                FROM replays r WHERE k.replay_id=r.id::text AND r.is_short='1'");
        }
    }
}
