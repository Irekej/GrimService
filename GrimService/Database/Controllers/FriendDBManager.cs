using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using GrimService.Database.Tables;
using GrimService.Features.Friends;
using Microsoft.Extensions.Logging;

namespace GrimService.Database.Controllers;

public class FriendDBManager : IDBManager<FriendDTO>
{
    protected IDbConnection _con;
    private readonly FriendDirectoryStore store;
    public FriendDBManager(ILogger<FriendDBManager> logger, IDbConnection con) { _con = con; store = new(con); }
    public Task<FriendDTO> GetByIDAsync(long id) => store.Find(id);
    public Task<IEnumerable<FriendDTO>> GetAllFriendsOfUser(long id) => store.Owned(id);
    public Task<IEnumerable<FriendDTO>> GetAllFriendsIdOfUser(long id) => store.Ids(id);
    public Task<bool> IsFriendedToUser(long userId, long targetId) => store.Contains(userId, targetId);
    public Task UpdateAsync(FriendDTO pData) => store.Replace(pData);
    public Task<long> AddAsync(FriendDTO friendData) => store.Append(friendData);
    public Task UpdateFriendList(int userid, string[] currentFriendList) => store.Synchronize(userid, currentFriendList);
    public Task RemoveAsync(long id) => store.Delete(id);
    public Task RemoveFriendAsync(long userId, long friendId) => store.DeleteOwned(userId, friendId);
    public void CreateTable() => store.EnsureSchema();
}
