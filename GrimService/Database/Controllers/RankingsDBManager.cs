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
    public class RankingsDBManager : IDBManager<RankingsDTO>
    {
        private ILogger<RankingsDBManager> _logger;
        protected IDbConnection _con;
        private readonly GrimService.Features.Rankings.Storage.RankingScoreWriter _scoreWriter;

        public RankingsDBManager(ILogger<RankingsDBManager> logger, IDbConnection con)
        {
            _logger = logger;
            _con = con;
            _scoreWriter = new GrimService.Features.Rankings.Storage.RankingScoreWriter(con.ConnectionString);
        }

        public Task<GrimService.Features.Rankings.RankingSaveStatus> SaveBestAsync(RankingsDTO ranking) => _scoreWriter.SaveBestAsync(ranking);

        public async Task<RankingsDTO> GetByIDAsync(long id)
            => await _con.QueryFirstOrDefaultAsync<RankingsDTO>(@"SELECT * FROM rankings WHERE id = @Id", new { Id = id });



        public async Task<List<RankingsDTO>> GetAllRankingsForecast(string boardId)
        {
            var rankingsList = await _con.QueryAsync<RankingsDTO>(
                @"SELECT * FROM rankings WHERE board_id=@BoardId ORDER BY score ASC",
                new { BoardId = boardId }
            );

            return rankingsList.ToList();
        }

        public async Task<List<RankingsDTO>> GetAllPlayerRankingForecast(string userId)
        {
            var rankingsList = await _con.QueryAsync<RankingsDTO>(
                @"SELECT * FROM rankings WHERE username=@UserId ORDER BY score ASC",
                new { UserId = userId }
            );

            return rankingsList.ToList();
        }

        public async Task<IEnumerable<RankingsDTO>> GetAllRankings(string boardId)
            => await _con.QueryAsync<RankingsDTO>(@"SELECT * FROM rankings WHERE board_id=@BoardId ORDER BY score ASC", new { BoardId = boardId });

        public async Task<IEnumerable<RankingsDTO>> GetAllRankingsOfUser(string userId, string boardId)
            => await _con.QueryAsync<RankingsDTO>(@"SELECT * FROM rankings WHERE username=@UserId AND board_id=@BoardId ORDER BY score ASC", new { UserId = userId, BoardId = boardId });


        public async Task<IEnumerable<RankingsDTO>> GetAllRankingsFriends(string boardId)
            => await _con.QueryAsync<RankingsDTO>(@"SELECT * FROM rankings WHERE board_id=@BoardId ORDER BY score ASC", new { BoardId = boardId });
 
        public async Task<IEnumerable<RankingsDTO>> GetScoreRankingOfPlayer(string userId, string boardId)
            => await _con.QueryAsync<RankingsDTO>(@"SELECT score FROM rankings WHERE username=@UserId AND board_id=@BoardId ORDER BY score ASC", new { UserId = userId, BoardId = boardId });

        public async Task<long> GetPlayerRankingPosition(string userId, string boardId)
        {
            var query = @"
        SELECT position FROM (
            SELECT username, ROW_NUMBER() OVER (ORDER BY score::numeric ASC, id ASC) AS position
            FROM rankings WHERE board_id = @BoardId
        ) ranked WHERE username = @UserId ORDER BY position LIMIT 1;";

            return await _con.ExecuteScalarAsync<long>(query, new { UserId = userId, BoardId = boardId });
        }

        public async Task<long> GetScoreRankingPosition(string boardId, string score)
        {
            var query = @"
        SELECT COUNT(*) + 1 FROM rankings
        WHERE board_id = @BoardId AND score::numeric < CAST(@Score AS numeric);";

            return await _con.ExecuteScalarAsync<long>(query, new { BoardId = boardId, Score = score });
        }

        public async Task<RankingsDTO> GetRankingByPlayerIdAndBoardId(long playerId, string boardId)
        {
            var query = @"SELECT * FROM rankings WHERE user_id=@PlayerId AND board_id=@BoardId";
            var result = await _con.QuerySingleOrDefaultAsync<RankingsDTO>(query, new { PlayerId = playerId, BoardId = boardId });
            return result;
        }

        public async Task<long> CountAllRankingsBoard(string boardId)
        {
            var countQuery = @"SELECT COUNT(*) FROM rankings WHERE board_id=@BoardId";
            var count = await _con.ExecuteScalarAsync<long>(countQuery, new { BoardId = boardId });
            return count;
        }

        public async Task<RankingsDTO> GetRankingByUserIdAndBoardId(long userId, string boardId)
        {
            var query = @"SELECT * FROM rankings WHERE user_id=@UserId AND board_id=@BoardId";
            var result = await _con.QuerySingleOrDefaultAsync<RankingsDTO>(query, new { UserId = userId, BoardId = boardId });
            return result;
        }

        public async Task UpdateRanking(RankingsDTO ranking)
        {
            var updateQuery = @"UPDATE rankings SET score=@Score, stats=@Stats,
                replay_id=CASE WHEN score IS DISTINCT FROM @Score OR stats IS DISTINCT FROM @Stats THEN NULL ELSE replay_id END,
                replay_mark_id=CASE WHEN score IS DISTINCT FROM @Score OR stats IS DISTINCT FROM @Stats THEN NULL ELSE replay_mark_id END
                WHERE user_id=@UserId AND board_id=@BoardId";

            await _con.ExecuteAsync(updateQuery, new
            {
                Score = ranking.Score,
                Stats = ranking.Stats,
                UserId = ranking.UserId,
                BoardId = ranking.BoardId
            });
        }

        public async Task<bool> AttachReplayAsync(RankingsDTO ranking, long replayId, bool isShort)
        {
            var column = isShort ? "replay_mark_id" : "replay_id";
            return await _con.ExecuteAsync($@"UPDATE rankings SET {column}=@ReplayId
                WHERE id=@Id AND user_id=@UserId AND board_id=@BoardId
                AND score IS NOT DISTINCT FROM @Score AND stats IS NOT DISTINCT FROM @Stats",
                new { ranking.Id, ranking.UserId, ranking.BoardId, ranking.Score, ranking.Stats,
                    ReplayId = replayId.ToString(System.Globalization.CultureInfo.InvariantCulture) }) == 1;
        }

        public async Task UpdateRankingReplay(long userId, string boardId, long replayId)
        {
            var updateQuery = @"UPDATE rankings SET replay_id=@ReplayId WHERE user_id=@UserId AND board_id=@BoardId";

            await _con.ExecuteAsync(updateQuery, new
            {
                ReplayId = replayId,
                UserId = userId,
                BoardId = boardId
            });
        }

        public async Task UpdateAsync(RankingsDTO pData)
            => await _con.ExecuteAsync(@"UPDATE rankings SET username=@Username, country=@Country, board_id=@BoardId, score=@Score, stats=@Stats, replay_id=@ReplayId, replay_mark_id=@ReplayMarkId, src_id=@SourceId, rank=CAST(@Rank AS text), create_time=@CreateTime WHERE id=@Id AND user_id=@UserId", pData);

        public async Task<long> AddAsync(RankingsDTO pData)
        {
            var query =
@"INSERT INTO rankings (user_id, username, country, board_id, score, stats, create_time)
  VALUES
(@UserId, @Username, @Country, @BoardId, @Score, @Stats, @CreateTime)
  returning id";

            return await _con.ExecuteScalarAsync<long>(query, new { pData.UserId, pData.Username, pData.Country, pData.BoardId, pData.Score, pData.Stats, pData.CreateTime });
        }

        public async Task RemoveAsync(long id)
            => await _con.ExecuteAsync(@"DELETE FROM rankings WHERE id=@id", new { Id = id });







        public async Task RemoveCourseAsync(long userId, long courseId)
           => await _con.ExecuteAsync(@"DELETE FROM rankings WHERE user_id=@UserId AND id=@Id", new { UserId = userId, Id = courseId });

        public void CreateTable()
        {
            string query =
            @"CREATE TABLE IF NOT EXISTS rankings (
                id SERIAL PRIMARY KEY,
				user_id BIGINT REFERENCES users(id),
                username TEXT,
                country TEXT,
                board_id TEXT,
                score TEXT,
                replay_id TEXT,
                src_id TEXT,
                comment TEXT,
                stats TEXT,
                rank TEXT,
                create_time TIMESTAMP WITHOUT TIME ZONE
			);";

            _con.Execute(query);

            _con.Execute("ALTER TABLE rankings ADD COLUMN IF NOT EXISTS replay_mark_id TEXT");

            string query2 = @"CREATE INDEX IF NOT EXISTS rankings_user_id_idx ON rankings (user_id)";
            _con.Execute(query2);
        }
    }
}
