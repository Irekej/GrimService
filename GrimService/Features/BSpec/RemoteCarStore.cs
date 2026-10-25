using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using GrimService.Database.Controllers;
using GrimService.Database.Tables;

namespace GrimService.Features.BSpec;

public sealed class RemoteCarStore(IDbConnection connection)
{
    public Task<BSpecCarDTO> Find(long id) => connection.QueryFirstOrDefaultAsync<BSpecCarDTO>("SELECT * FROM bspeccar WHERE id=@Id",new {Id=id});
    public Task<int?> Owner(long id) => connection.QueryFirstOrDefaultAsync<int?>("SELECT user_id FROM bspeccar WHERE id=@Id",new {Id=id});
    public Task<int?> Count(long owner) => connection.QueryFirstOrDefaultAsync<int?>("SELECT COUNT(id) FROM bspeccar WHERE user_id=@Owner",new {Owner=owner});
    public Task<IEnumerable<BSpecCarDTO>> Owned(long owner) => connection.QueryAsync<BSpecCarDTO>("SELECT * FROM bspeccar WHERE user_id=@Owner",new {Owner=owner});
    public Task<IEnumerable<BSpecCarDTO>> ById(long id) => connection.QueryAsync<BSpecCarDTO>("SELECT * FROM bspeccar WHERE id=@Id",new {Id=id});
    public async Task SetThumbnail(long id,long thumbnail) => await connection.ExecuteAsync("UPDATE bspeccar SET car_thumbnail=CAST(@Thumbnail AS text) WHERE id=@Id",new {Id=id,Thumbnail=thumbnail});
    public async Task Replace(BSpecCarDTO car) => await connection.ExecuteAsync(@"UPDATE bspeccar SET car_label=@CarLabel,
        car_parameter=@CarParameter,car_thumbnail=@CarThumbnail,create_time=@CreateTime WHERE id=@Id AND user_id=@UserId",car);
    public Task<long> Append(BSpecCarDTO car) => BSpecSlotLimit.InsertAsync(connection,car.UserId,false,@"INSERT INTO bspeccar
        (user_id,car_label,car_parameter,car_thumbnail,create_time)
        VALUES (@UserId,@CarLabel,@CarParameter,@CarThumbnail,@CreateTime) RETURNING id",car);
    public async Task Delete(long id) => await connection.ExecuteAsync("DELETE FROM bspeccar WHERE id=@Id",new {Id=id});
    public async Task DeleteOwned(long owner,long id) => await connection.ExecuteAsync("DELETE FROM bspeccar WHERE user_id=@Owner AND id=@Id",new {Owner=owner,Id=id});
    public void EnsureSchema() => connection.Execute(@"CREATE TABLE IF NOT EXISTS bspeccar (
        id BIGSERIAL PRIMARY KEY,user_id BIGINT REFERENCES users(id),car_label TEXT,car_parameter TEXT,
        car_thumbnail TEXT,create_time TIMESTAMP WITHOUT TIME ZONE);
        CREATE INDEX IF NOT EXISTS bspeccar_user_id_idx ON bspeccar(user_id)");
}
