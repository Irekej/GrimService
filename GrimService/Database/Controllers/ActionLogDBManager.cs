using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using GrimService.Database.Tables;
using GrimService.Features.PlayerRecords;
using Microsoft.Extensions.Logging;

namespace GrimService.Database.Controllers
{
    public class ActionLogDBManager : IDBManager<ActionLogDTO>
    {
        private readonly ActionHistoryStore store;

        public ActionLogDBManager(ILogger<ActionLogDBManager> logger, IDbConnection connection)
        {
            store = new ActionHistoryStore(connection);
        }

        public Task<ActionLogDTO> GetByIDAsync(long id) => store.Find(id);
        public Task<long> AddAsync(ActionLogDTO entry) => store.Append(entry);
        public Task UpdateAsync(ActionLogDTO entry) => store.Replace(entry);
        public Task RemoveAsync(long id) => store.Delete(id);
        public void CreateTable() => store.EnsureSchema();
        public Task<IEnumerable<ActionLogDTO>> GetAllActionsOfUser(int userId) => store.ForUser(userId);
    }
}
