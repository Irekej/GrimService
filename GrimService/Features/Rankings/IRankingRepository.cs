using System.Collections.Generic;
using System.Threading.Tasks;
using GrimService.Database.Tables;

namespace GrimService.Features.Rankings
{
    public interface IRankingRepository
    {
        Task<RankingSaveStatus> SaveBestAsync(RankingsDTO ranking);
        Task<IEnumerable<RankingsDTO>> ReadBoardAsync(string boardId);
        Task<long> CountAsync(string boardId);
        Task<long> FindPositionAsync(string username, string boardId);
        Task<long> PredictPositionAsync(string boardId, string score);
        Task<RankingsDTO> FindAsync(long userId, string boardId);
        Task AddAsync(RankingsDTO ranking);
        Task UpdateAsync(RankingsDTO ranking);
    }
}
