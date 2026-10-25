using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using GrimService.Database.Tables;
using GrimService.Features.Photos;
using Microsoft.Extensions.Logging;

namespace GrimService.Database.Controllers;

public class PhotoDBManager : IDBManager<PhotoDTO>
{
    protected IDbConnection _con;
    private readonly PlayerPhotoStore store;
    public PhotoDBManager(ILogger<PhotoDBManager> logger, IDbConnection con) { _con = con; store = new(con); }
    public Task<PhotoDTO> GetByIDAsync(long id) => store.Find(id);
    public Task<int?> GetAuthorIdOfPhotoAsync(long id) => store.Author(id);
    public Task<int?> GetPhotoCountOfUserAsync(long id) => store.Count(id);
    public Task<IEnumerable<PhotoDTO>> GetAllPhotosOfUser(long userId, string typeId) => store.Owned(userId, typeId);
    public Task<IEnumerable<PhotoDTO>> GetAllPhotosOfUsers(string typeId) => store.Latest(typeId);
    public Task<IEnumerable<PhotoDTO>> GetAllCarPhotosOfUser(long userId, string typeId) => store.Owned(userId, typeId);
    public Task<IEnumerable<PhotoDTO>> GetAllForAdminAsync(long userId) => store.ForAdmin(userId);
    public Task UpdateAsync(PhotoDTO pData) => store.Replace(pData);
    public Task<long> AddAsync(PhotoDTO pData) => store.Append(pData);
    public Task RemoveAsync(long id) => store.Delete(id);
    public Task RemoveForAdminAsync(long userId, long photoId) => store.DeleteOwned(userId, photoId);
    public Task RemovePhotoAsync(long userId, long photoId) => store.DeleteOwned(userId, photoId);
    public void CreateTable() => store.EnsureSchema();
}
