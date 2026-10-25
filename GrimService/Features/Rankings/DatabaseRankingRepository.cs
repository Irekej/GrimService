using System.Collections.Generic;
using System.Threading.Tasks;
using GrimService.Database.Controllers;
using GrimService.Database.Tables;

namespace GrimService.Features.Rankings
{

    public sealed class DatabaseRankingRepository : IRankingRepository
    {
        private readonly RankingsDBManager _database;
        public DatabaseRankingRepository(RankingsDBManager database) => _database = database;
        public Task<RankingSaveStatus> SaveBestAsync(RankingsDTO ranking) => _database.SaveBestAsync(ranking);

        public Task<IEnumerable<RankingsDTO>> ReadBoardAsync(string boardId) => _database.GetAllRankings(boardId);
        public Task<long> CountAsync(string boardId) => _database.CountAllRankingsBoard(boardId);
        public Task<long> FindPositionAsync(string username, string boardId) => _database.GetPlayerRankingPosition(username, boardId);
        public Task<long> PredictPositionAsync(string boardId, string score) => _database.GetScoreRankingPosition(boardId, score);
        public Task<RankingsDTO> FindAsync(long userId, string boardId) => _database.GetRankingByPlayerIdAndBoardId(userId, boardId);
        public async Task AddAsync(RankingsDTO ranking) => await _database.AddAsync(ranking);
        public Task UpdateAsync(RankingsDTO ranking) => _database.UpdateRanking(ranking);
    }
}
