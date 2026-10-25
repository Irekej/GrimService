using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using GrimService.Database.Tables;
using GrimService.Features.PlayerRecords;
using Microsoft.Extensions.Logging;

namespace GrimService.Database.Controllers
{
    public class UserSpecialDBManager : IDBManager<UserSpecialDTO>
    {
        private readonly PlayerRewardStore store;

        public UserSpecialDBManager(ILogger<UserSpecialDBManager> logger, IDbConnection connection)
        {
            store = new PlayerRewardStore(connection);
        }

        public Task<UserSpecialDTO> GetByIDAsync(long id) => store.Find(id);
        public Task<long> AddAsync(UserSpecialDTO entry) => store.Append(entry);
        public Task UpdateAsync(UserSpecialDTO entry) => store.Replace(entry);
        public Task RemoveAsync(long id) => store.Delete(id);
        public void CreateTable() => store.EnsureSchema();
        public Task<IEnumerable<UserSpecialDTO>> GetAllPresentsOfUserAsync(long userId, int type = -1) => store.ForUser(userId, type);
    }
}
