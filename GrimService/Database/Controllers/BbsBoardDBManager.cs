using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using GrimService.Database.Tables;
using GrimService.Features.PlayerRecords;
using Microsoft.Extensions.Logging;

namespace GrimService.Database.Controllers
{
    public class BbsBoardDBManager : IDBManager<BbsDTO>
    {
        private readonly PlayerBoardStore store;

        public BbsBoardDBManager(ILogger<BbsBoardDBManager> logger, IDbConnection connection)
        {
            store = new PlayerBoardStore(connection);
        }

        public Task<BbsDTO> GetByIDAsync(long id) => store.Find(id);
        public Task<long> AddAsync(BbsDTO entry) => store.Append(entry);
        public Task UpdateAsync(BbsDTO entry) => store.Replace(entry);
        public Task RemoveAsync(long id) => store.Delete(id);
        public void CreateTable() => store.EnsureSchema();
        public Task<IEnumerable<BbsDTO>> GetAllCommentsOfBoard(int boardId) => store.ForBoard(boardId);
        public Task RemoveByBoardIdAsync(int boardId) => store.Clear(boardId);
    }
}
