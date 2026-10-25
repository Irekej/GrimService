using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using GrimService.Database.Tables;
using GrimService.Features.BSpec;
using Microsoft.Extensions.Logging;

namespace GrimService.Database.Controllers;

public class BSpecCarDBManager : IDBManager<BSpecCarDTO>
{
    protected IDbConnection _con;
    private readonly RemoteCarStore store;
    public BSpecCarDBManager(ILogger<BSpecCarDBManager> logger,IDbConnection con) { _con=con;store=new(con); }
    public Task<BSpecCarDTO> GetByIDAsync(long id)=>store.Find(id);
    public Task<int?> GetAuthorIdOfPhotoAsync(long id)=>store.Owner(id);
    public Task<int?> GetPhotoCountOfUserAsync(long id)=>store.Count(id);
    public Task<IEnumerable<BSpecCarDTO>> GetAllCarsOfUser(long userId)=>store.Owned(userId);
    public Task UpdateCarData(long id,long carThumbnail)=>store.SetThumbnail(id,carThumbnail);
    public Task<IEnumerable<BSpecCarDTO>> GetAllCarsOfId(long carId)=>store.ById(carId);
    [Obsolete("Remote cars have no photo type column; use GetAllCarsOfUser.")]
    public Task<IEnumerable<BSpecCarDTO>> GetAllPhotosOfUsers(string typeId)=>throw new NotSupportedException("Remote cars are not typed photos.");
    [Obsolete("Remote cars have no photo type column; use GetAllCarsOfUser.")]
    public Task<IEnumerable<BSpecCarDTO>> GetAllCarPhotosOfUser(long userId,string typeId)=>throw new NotSupportedException("Remote cars are not typed photos.");
    public Task UpdateAsync(BSpecCarDTO pData)=>store.Replace(pData);
    public Task<long> AddAsync(BSpecCarDTO pData)=>store.Append(pData);
    public Task RemoveAsync(long id)=>store.Delete(id);
    public Task RemoveCarAsync(long userId,long carId)=>store.DeleteOwned(userId,carId);
    public void CreateTable()=>store.EnsureSchema();
}
