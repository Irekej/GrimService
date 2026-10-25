using System.Threading.Tasks;
using GrimService.Database.Controllers;
using GrimService.Database.Tables;

namespace GrimService.Features.Replays
{
    public sealed class DatabaseReplayRepository : IReplayRepository
    {
        private readonly RankingsDBManager _rankings;
        private readonly ReplayDBManager _replays;

        public DatabaseReplayRepository(RankingsDBManager rankings, ReplayDBManager replays)
        { _rankings = rankings; _replays = replays; }

        public Task<RankingsDTO> FindRankingAsync(long userId, string boardId) =>
            _rankings.GetRankingByPlayerIdAndBoardId(userId, boardId);
        public Task<ReplayDTO> FindReplayAsync(long id) => _replays.GetByIDAsync(id);
        public Task<long> AddAsync(ReplayDTO replay) => _replays.AddAsync(replay);
        public Task<bool> AttachAsync(RankingsDTO snapshot, long replayId, bool isShort) =>
            _rankings.AttachReplayAsync(snapshot, replayId, isShort);
        public Task RemoveAsync(long id) => _replays.RemoveAsync(id);
    }
}
