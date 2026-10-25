using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using GrimService.Database.Tables;

namespace GrimService.Features.Gifts.Storage;

public sealed class GiftInboxStore(IDbConnection connection)
{
    public Task<ItemBoxDTO> Find(long id) => connection.QueryFirstOrDefaultAsync<ItemBoxDTO>("SELECT * FROM itembox WHERE id=@Id",new { Id=id });
    public Task<IEnumerable<ItemBoxDTO>> Read(string receiver,long cursor) => connection.QueryAsync<ItemBoxDTO>(
        "SELECT * FROM itembox WHERE receiver=@Receiver AND id>@Cursor ORDER BY id",new { Receiver=receiver, Cursor=cursor });
    public Task<ItemBoxDTO> NextUnread(string receiver) => connection.QueryFirstOrDefaultAsync<ItemBoxDTO>(
        "SELECT * FROM itembox WHERE receiver=@Receiver AND is_read=0 ORDER BY id LIMIT 1",new { Receiver=receiver });
    public async Task MarkRead(long id) => await connection.ExecuteAsync("UPDATE itembox SET is_read=1 WHERE id=@Id",new { Id=id });
    public Task<long> Append(ItemBoxDTO gift) => connection.ExecuteScalarAsync<long>(@"INSERT INTO itembox
        (user_id,sender,receiver,type,stats,comment,mac,create_time)
        VALUES (@UserId,@Sender,@Receiver,@Type,@Stats,@Comment,@Mac,@CreateTime) RETURNING id",gift);
    public async Task Replace(ItemBoxDTO gift) => await connection.ExecuteAsync(@"UPDATE itembox SET sender=@Sender,
        receiver=@Receiver,type=@Type,stats=@Stats,comment=@Comment,mac=@Mac,create_time=@CreateTime
        WHERE id=@Id AND user_id=@UserId",gift);
    public async Task<bool> Receive(long id,string receiver) => await connection.ExecuteAsync(
        "DELETE FROM itembox WHERE id=@Id AND receiver=@Receiver",new { Id=id, Receiver=receiver })==1;
    public async Task Delete(long id) => await connection.ExecuteAsync("DELETE FROM itembox WHERE id=@Id",new { Id=id });
    public async Task DeleteOwned(long owner,long id) => await connection.ExecuteAsync(
        "DELETE FROM itembox WHERE user_id=@Owner AND id=@Id",new { Owner=owner, Id=id });
    public void EnsureSchema() => connection.Execute(@"CREATE TABLE IF NOT EXISTS itembox (
        id BIGSERIAL PRIMARY KEY,user_id BIGINT REFERENCES users(id),sender TEXT,receiver TEXT,type TEXT,
        stats TEXT,comment TEXT,mac TEXT,create_time TIMESTAMP WITHOUT TIME ZONE);
        ALTER TABLE itembox ADD COLUMN IF NOT EXISTS is_read INTEGER NOT NULL DEFAULT 0;
        CREATE INDEX IF NOT EXISTS itembox_user_id_idx ON itembox(user_id);
        CREATE INDEX IF NOT EXISTS itembox_receiver_id_idx ON itembox(receiver,id)");
}
