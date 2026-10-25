using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using GrimService.Database.Tables;
using GrimService.Features.Gifts.Storage;
using Microsoft.Extensions.Logging;

namespace GrimService.Database.Controllers;

public class ItemBoxDBManager : IDBManager<ItemBoxDTO>
{
    protected IDbConnection _con;
    private readonly GiftInboxStore store;
    public ItemBoxDBManager(ILogger<ItemBoxDBManager> logger,IDbConnection con) { _con=con;store=new(con); }
    public Task<ItemBoxDTO> GetByIDAsync(long id)=>store.Find(id);
    public Task<IEnumerable<ItemBoxDTO>> GetAllItemsOfUser(string userId)=>store.Read(userId,0);
    public Task<IEnumerable<ItemBoxDTO>> GetItemOfUser(long itemboxId,string userId)=>store.Read(userId,itemboxId);
    public Task<IEnumerable<ItemBoxDTO>> GetAllItemsOfUser2(string userId)=>store.Read(userId,0);
    public Task<ItemBoxDTO> GetNextUnreadItem(string userId)=>store.NextUnread(userId);
    public Task MarkAsRead(long id)=>store.MarkRead(id);
    public Task UpdateAsync(ItemBoxDTO pData)=>store.Replace(pData);
    public Task<long> AddAsync(ItemBoxDTO pData)=>store.Append(pData);
    public Task RemoveAsync(long id)=>store.Delete(id);
    public Task<bool> ReceiveAsync(long id,string receiver)=>store.Receive(id,receiver);
    public Task RemovePhotoAsync(long userId,long carId)=>store.DeleteOwned(userId,carId);
    public void CreateTable()=>store.EnsureSchema();
}
