using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using GrimService.Database.Tables;

namespace GrimService.Features.Messaging.Storage;

public sealed class Gt5MailboxStore(IDbConnection connection)
{
    private const string Projection = @"SELECT m.*,
        COALESCE((SELECT u.nickname FROM users u WHERE u.psn_user_id=m.from_username LIMIT 1),m.from_username) AS from_nickname,
        COALESCE((SELECT u.nickname FROM users u WHERE u.psn_user_id=m.to_username LIMIT 1),m.to_username) AS to_nickname
        FROM gtmail m ";

    public Task<GTMailDTO> Find(long id) => connection.QueryFirstOrDefaultAsync<GTMailDTO>("SELECT * FROM gtmail WHERE id=@Id",new { Id=id });
    public Task<int?> Author(long id) => connection.QueryFirstOrDefaultAsync<int?>("SELECT user_id FROM gtmail WHERE id=@Id",new { Id=id });
    public Task<int?> Count(long owner) => connection.QueryFirstOrDefaultAsync<int?>("SELECT COUNT(id) FROM gtmail WHERE user_id=@Owner",new { Owner=owner });
    public Task<IEnumerable<GTMailDTO>> SubmittedBy(long owner) => connection.QueryAsync<GTMailDTO>("SELECT * FROM gtmail WHERE user_id=@Owner",new { Owner=owner });

    public Task<IEnumerable<GTMailDTO>> Read(string user, long cursor, string mailbox)
    {
        string predicate = mailbox switch {
            "receive" => "m.to_username=@User AND NOT m.deleted_to",
            "send" => "m.from_username=@User AND NOT m.deleted_from",
            "all" => "((m.from_username=@User AND NOT m.deleted_from) OR (m.to_username=@User AND NOT m.deleted_to))",
            _ => throw new ArgumentOutOfRangeException(nameof(mailbox))
        };
        return connection.QueryAsync<GTMailDTO>(Projection+"WHERE m.id>@Cursor AND "+predicate+" ORDER BY m.id",new { User=user, Cursor=cursor });
    }

    public async Task<bool> Hide(long id, string user, bool sender)
    {
        string sql=sender ? "UPDATE gtmail SET deleted_from=TRUE WHERE id=@Id AND from_username=@User" :
            "UPDATE gtmail SET deleted_to=TRUE WHERE id=@Id AND to_username=@User";
        return await connection.ExecuteAsync(sql,new { Id=id, User=user })==1;
    }
    public Task<long> Append(GTMailDTO mail) => connection.ExecuteScalarAsync<long>(@"INSERT INTO gtmail
        (user_id,from_username,to_username,subject,body,create_time)
        VALUES (@UserId,@FromUsername,@ToUsername,@Subject,@Body,@CreateTime) RETURNING id",mail);
    public async Task Replace(GTMailDTO mail) => await connection.ExecuteAsync(@"UPDATE gtmail SET from_username=@FromUsername,
        to_username=@ToUsername,subject=@Subject,body=@Body,create_time=@CreateTime WHERE id=@Id AND user_id=@UserId",mail);
    public async Task Delete(long id) => await connection.ExecuteAsync("DELETE FROM gtmail WHERE id=@Id",new { Id=id });
    public async Task DeleteOwned(long owner,long id) => await connection.ExecuteAsync("DELETE FROM gtmail WHERE user_id=@Owner AND id=@Id",new { Owner=owner, Id=id });
    public void EnsureSchema() => connection.Execute(@"CREATE TABLE IF NOT EXISTS gtmail (
        id BIGSERIAL PRIMARY KEY,user_id BIGINT REFERENCES users(id),from_username TEXT,to_username TEXT,
        subject TEXT,body TEXT,create_time TIMESTAMP WITHOUT TIME ZONE);
        ALTER TABLE gtmail ADD COLUMN IF NOT EXISTS deleted_to BOOLEAN NOT NULL DEFAULT FALSE;
        ALTER TABLE gtmail ADD COLUMN IF NOT EXISTS deleted_from BOOLEAN NOT NULL DEFAULT FALSE;
        CREATE INDEX IF NOT EXISTS gtmail_user_id_idx ON gtmail(user_id);
        CREATE INDEX IF NOT EXISTS gtmail_visible_sender_idx ON gtmail(from_username,id) WHERE NOT deleted_from;
        CREATE INDEX IF NOT EXISTS gtmail_visible_recipient_idx ON gtmail(to_username,id) WHERE NOT deleted_to");
}
