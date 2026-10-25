using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using GrimService.Database.Tables;
using GrimService.Features.Sessions;
using Microsoft.Extensions.Logging;

namespace GrimService.Database.Controllers;

public class PresenceDBManager : IDBManager<PresenceDTO>
{
    protected IDbConnection _con;
    private readonly PresenceRecordStore store;
    public PresenceDBManager(ILogger<PresenceDBManager> logger,IDbConnection con) { _con=con;store=new(con); }
    public Task<PresenceDTO> GetByIDAsync(long id)=>store.Find(id);
    public Task<PresenceDTO> GetByPSNUserIdAsync(string psnId)=>store.FindName(psnId);
    public Task<IEnumerable<PresenceDTO>> GetStatsOfUser(string userId)=>store.ForName(userId);
    public Task UpdateAsync(PresenceDTO pData)=>store.Replace(pData);
    public Task<long> AddAsync(PresenceDTO pData)=>store.Append(pData);
    public Task RemoveAsync(long id)=>store.Delete(id);
    public Task RemoveByPSNUserIdAsync(string psnUserId)=>store.DeleteName(psnUserId);
    public void CreateTable()=>store.EnsureSchema();
}
