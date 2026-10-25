using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using GrimService.Database.Tables;

namespace GrimService.Features.Photos;

public sealed class PlayerPhotoStore(IDbConnection connection)
{
    public Task<PhotoDTO> Find(long id) => connection.QueryFirstOrDefaultAsync<PhotoDTO>("SELECT * FROM photos WHERE id=@Id", new { Id = id });
    public Task<int?> Author(long id) => connection.QueryFirstOrDefaultAsync<int?>("SELECT user_id FROM photos WHERE id=@Id", new { Id = id });
    public Task<int?> Count(long owner) => connection.QueryFirstOrDefaultAsync<int?>("SELECT COUNT(id) FROM photos WHERE user_id=@Owner", new { Owner = owner });
    public Task<IEnumerable<PhotoDTO>> Owned(long owner, string type) => connection.QueryAsync<PhotoDTO>("SELECT * FROM photos WHERE user_id=@Owner AND type=@Type", new { Owner = owner, Type = type });
    public Task<IEnumerable<PhotoDTO>> Latest(string type) => connection.QueryAsync<PhotoDTO>("SELECT * FROM photos WHERE type=@Type ORDER BY id DESC LIMIT 10", new { Type = type });
    public Task<IEnumerable<PhotoDTO>> ForAdmin(long owner) => connection.QueryAsync<PhotoDTO>("SELECT * FROM photos WHERE user_id=@Owner ORDER BY create_time DESC NULLS LAST", new { Owner = owner });
    public async Task Replace(PhotoDTO photo) => await connection.ExecuteAsync(@"UPDATE photos SET username=@Username,
        comment=@Comment,car_name=@CarName,place=@Place,type=@Type,create_time=@CreateTime WHERE id=@Id AND user_id=@UserId", photo);
    public Task<long> Append(PhotoDTO photo) => connection.ExecuteScalarAsync<long>(@"INSERT INTO photos
        (user_id,username,comment,car_name,place,type,create_time)
        VALUES (@UserId,@Username,@Comment,@CarName,@Place,@Type,@CreateTime) RETURNING id", photo);
    public async Task Delete(long id) => await connection.ExecuteAsync("DELETE FROM photos WHERE id=@Id", new { Id = id });
    public async Task DeleteOwned(long owner, long id) => await connection.ExecuteAsync("DELETE FROM photos WHERE user_id=@Owner AND id=@Id", new { Owner = owner, Id = id });
    public void EnsureSchema() => connection.Execute(@"CREATE TABLE IF NOT EXISTS photos (
        id BIGSERIAL PRIMARY KEY,user_id BIGINT REFERENCES users(id),username TEXT,comment TEXT,
        car_name TEXT,place TEXT,type TEXT,create_time TIMESTAMP WITHOUT TIME ZONE);
        CREATE INDEX IF NOT EXISTS photos_user_id_idx ON photos(user_id)");
}
