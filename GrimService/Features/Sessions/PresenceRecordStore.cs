using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using GrimService.Database.Tables;

namespace GrimService.Features.Sessions;


public sealed class PresenceRecordStore(IDbConnection connection)
{
    private const string Projection="SELECT id,user_id,psn_user_id,stats,last_update_time AS PresenceUpdateTime FROM presence ";
    public Task<PresenceDTO> Find(long id) => connection.QueryFirstOrDefaultAsync<PresenceDTO>(Projection+"WHERE id=@Id",new {Id=id});
    public Task<PresenceDTO> FindName(string name) => connection.QueryFirstOrDefaultAsync<PresenceDTO>(Projection+"WHERE psn_user_id=@Name",new {Name=name});
    public Task<IEnumerable<PresenceDTO>> ForName(string name) => connection.QueryAsync<PresenceDTO>(Projection+"WHERE psn_user_id=@Name",new {Name=name});
    public async Task Replace(PresenceDTO presence) => await connection.ExecuteAsync("UPDATE presence SET stats=@Stats,last_update_time=NOW() WHERE id=@Id",presence);
    public Task<long> Append(PresenceDTO presence) => connection.ExecuteScalarAsync<long>(@"INSERT INTO presence
        (user_id,psn_user_id,stats,last_update_time) VALUES (@UserId,@PSNUserId,@Stats,@PresenceUpdateTime) RETURNING id",presence);
    public async Task Delete(long id) => await connection.ExecuteAsync("DELETE FROM presence WHERE id=@Id",new {Id=id});
    public async Task DeleteName(string name) => await connection.ExecuteAsync("DELETE FROM presence WHERE psn_user_id=@Name",new {Name=name});
    public void EnsureSchema() => connection.Execute(@"CREATE TABLE IF NOT EXISTS presence (
        id BIGSERIAL PRIMARY KEY,user_id BIGINT REFERENCES users(id),psn_user_id TEXT,stats TEXT,last_update_time TIMESTAMP WITHOUT TIME ZONE);
        CREATE INDEX IF NOT EXISTS presence_user_id_idx ON presence(user_id)");
}
