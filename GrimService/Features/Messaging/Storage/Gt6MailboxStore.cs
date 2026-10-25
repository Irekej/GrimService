using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace GrimService.Features.Messaging.Storage;

public sealed class Gt6MailboxStore(IDbConnection connection)
{
    public void EnsureSchema() => connection.Execute(@"CREATE TABLE IF NOT EXISTS gt6_mail (
        id BIGINT PRIMARY KEY,sender BIGINT NOT NULL REFERENCES users(id),
        recipient BIGINT NOT NULL REFERENCES users(id),subject TEXT NOT NULL,body TEXT NOT NULL,
        created TIMESTAMPTZ NOT NULL DEFAULT NOW(),deleted_from BOOLEAN NOT NULL DEFAULT FALSE,
        deleted_to BOOLEAN NOT NULL DEFAULT FALSE,read_at TIMESTAMPTZ);
        CREATE INDEX IF NOT EXISTS gt6_mail_sender_idx ON gt6_mail(sender,id);
        CREATE INDEX IF NOT EXISTS gt6_mail_recipient_idx ON gt6_mail(recipient,id)");


    public Task<long> Send(long sender, long recipient, string subject, string body) => connection.ExecuteScalarAsync<long>(@"
        INSERT INTO gt6_mail(id,sender,recipient,subject,body)
        VALUES(nextval(pg_get_serial_sequence('gtmail','id')),@Sender,@Recipient,@Subject,@Body) RETURNING id",
        new { Sender=sender, Recipient=recipient, Subject=subject, Body=body });

    public Task<IEnumerable<Gt6MailMessage>> Read(long user, long cursor, string mailbox) => connection.QueryAsync<Gt6MailMessage>(@"
        SELECT m.id,m.subject,m.body,m.created,s.psn_user_id AS FromName,r.psn_user_id AS ToName,
            s.nickname AS FromNickname,r.nickname AS ToNickname
        FROM gt6_mail m JOIN users s ON s.id=m.sender JOIN users r ON r.id=m.recipient
        WHERE m.id>@Cursor AND ((@Mailbox IN ('all','send') AND m.sender=@User AND NOT m.deleted_from)
            OR (@Mailbox IN ('all','receive') AND m.recipient=@User AND NOT m.deleted_to))
        ORDER BY m.id LIMIT 500", new { User=user, Cursor=cursor, Mailbox=mailbox });

    public async Task<bool> Change(long user, long id, string command)
    {
        string sql = command switch {
            "mail.delete.from" => "UPDATE gt6_mail SET deleted_from=TRUE WHERE id=@Id AND sender=@User",
            "mail.delete.to" => "UPDATE gt6_mail SET deleted_to=TRUE WHERE id=@Id AND recipient=@User",
            "mail.read.to" => "UPDATE gt6_mail SET read_at=COALESCE(read_at,NOW()) WHERE id=@Id AND recipient=@User",
            _ => null
        };
        return sql != null && await connection.ExecuteAsync(sql,new { User=user, Id=id }) == 1;
    }
}
