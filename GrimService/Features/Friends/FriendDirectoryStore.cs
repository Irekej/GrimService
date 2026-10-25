using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using GrimService.Database.Tables;

namespace GrimService.Features.Friends;

public sealed class FriendDirectoryStore(IDbConnection connection)
{
    public Task<FriendDTO> Find(long id) => connection.QueryFirstOrDefaultAsync<FriendDTO>("SELECT * FROM friends WHERE id=@Id", new { Id = id });
    public Task<IEnumerable<FriendDTO>> Owned(long owner) => connection.QueryAsync<FriendDTO>("SELECT * FROM friends WHERE userid=@Owner", new { Owner = owner });
    public Task<IEnumerable<FriendDTO>> Ids(long owner) => connection.QueryAsync<FriendDTO>("SELECT friendid FROM friends WHERE userid=@Owner", new { Owner = owner });
    public Task<bool> Contains(long owner, long target) => connection.ExecuteScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM friends WHERE userid=@Owner AND friendid=@Target)", new { Owner = owner, Target = target });
    public async Task Replace(FriendDTO friend) => await connection.ExecuteAsync("UPDATE friends SET friendid=@FriendId WHERE id=@Id AND userid=@UserId", friend);
    public Task<long> Append(FriendDTO friend) => connection.ExecuteScalarAsync<long>("INSERT INTO friends(userid,friendid) VALUES (@UserId,@FriendId) RETURNING id", friend);
    public async Task Delete(long id) => await connection.ExecuteAsync("DELETE FROM friends WHERE id=@Id", new { Id = id });
    public async Task DeleteOwned(long owner, long target) => await connection.ExecuteAsync("DELETE FROM friends WHERE userid=@Owner AND friendid=@Target", new { Owner = owner, Target = target });

    public async Task Synchronize(int owner, string[] names)
    {
        ArgumentNullException.ThrowIfNull(names);
        bool openedHere = connection.State != ConnectionState.Open;
        if (openedHere) connection.Open();
        try
        {
            using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

            var exists = await connection.QuerySingleOrDefaultAsync<long?>("SELECT id FROM users WHERE id=@Owner FOR UPDATE", new { Owner = owner }, transaction);
            if (!exists.HasValue) throw new InvalidOperationException("Friend list owner does not exist");
            await connection.ExecuteAsync("DELETE FROM friends WHERE userid=@Owner", new { Owner = owner }, transaction);
            await connection.ExecuteAsync(@"INSERT INTO friends(userid,friendid)
                SELECT @Owner,id FROM users WHERE psn_user_id=ANY(@Names) AND id<>@Owner",
                new { Owner = owner, Names = names.Distinct(StringComparer.Ordinal).ToArray() }, transaction);
            transaction.Commit();
        }
        finally { if (openedHere) connection.Close(); }
    }

    public void EnsureSchema() => connection.Execute(@"CREATE TABLE IF NOT EXISTS friends (
        id SERIAL PRIMARY KEY,userid INTEGER,friendid INTEGER);
        CREATE INDEX IF NOT EXISTS friends_userid_idx ON friends(userid);
        CREATE INDEX IF NOT EXISTS friends_friendid_idx ON friends(friendid)");
}
