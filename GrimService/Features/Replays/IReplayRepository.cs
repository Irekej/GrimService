using System.Threading.Tasks;
using GrimService.Database.Tables;

namespace GrimService.Features.Replays
{
    public interface IReplayRepository
    {
        Task<RankingsDTO> FindRankingAsync(long userId, string boardId);
        Task<ReplayDTO> FindReplayAsync(long id);
        Task<long> AddAsync(ReplayDTO replay);
        Task<bool> AttachAsync(RankingsDTO snapshot, long replayId, bool isShort);
        Task RemoveAsync(long id);
    }
}
