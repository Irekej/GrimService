using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using GrimService.Database.Tables;
using GrimService.Features.Messaging.Storage;
using Microsoft.Extensions.Logging;

namespace GrimService.Database.Controllers;

public class GTMailDBManager : IDBManager<GTMailDTO>
{
    protected IDbConnection _con;
    private readonly Gt5MailboxStore store;
    public GTMailDBManager(ILogger<GTMailDBManager> logger,IDbConnection con) { _con=con;store=new(con); }
    public Task<GTMailDTO> GetByIDAsync(long id)=>store.Find(id);
    public Task<int?> GetAuthorIdOfPhotoAsync(long id)=>store.Author(id);
    public Task<int?> GetPhotoCountOfUserAsync(long id)=>store.Count(id);
    public Task<IEnumerable<GTMailDTO>> GetAllMailsReceivedOfUser(string userId)=>store.Read(userId,0,"receive");
    public Task<IEnumerable<GTMailDTO>> GetAllMailsReceivedOfUserid(long mailId,string userId)=>store.Read(userId,mailId,"receive");
    public Task<IEnumerable<GTMailDTO>> GetAllMailsReceivedOfId(long userId)=>store.SubmittedBy(userId);
    public Task<IEnumerable<GTMailDTO>> GetAllMailsSendedOfUser(string userId)=>store.Read(userId,0,"send");
    public Task<IEnumerable<GTMailDTO>> GetAllMailsSendedOfUserid(long mailId,string userId)=>store.Read(userId,mailId,"send");
    public Task<IEnumerable<GTMailDTO>> GetAllMailsOfUserid(long mailId,string userId)=>store.Read(userId,mailId,"all");
    public Task<bool> DeleteForUserAsync(long id,string username,bool sender)=>store.Hide(id,username,sender);
    [Obsolete("GT5 mail has no photo type column; use a mailbox list method.")]
    public Task<IEnumerable<GTMailDTO>> GetAllCarPhotosOfUser(long userId,string typeId)
        => throw new NotSupportedException("Photo-type queries do not apply to GT5 mail.");
    public Task UpdateAsync(GTMailDTO pData)=>store.Replace(pData);
    public Task<long> AddAsync(GTMailDTO pData)=>store.Append(pData);
    public Task RemoveAsync(long id)=>store.Delete(id);
    public Task RemovePhotoAsync(long userId,long carId)=>store.DeleteOwned(userId,carId);
    public void CreateTable()=>store.EnsureSchema();
}
