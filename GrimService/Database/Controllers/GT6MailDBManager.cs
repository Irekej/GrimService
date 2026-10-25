using System;
using System.Data;
using System.Threading.Tasks;
using GrimService.Features.Messaging;
using GrimService.Features.Messaging.Storage;

namespace GrimService.Database.Controllers;

public sealed class GT6MailDBManager
{
    private readonly Gt6MailboxStore store;
    public GT6MailDBManager(IDbConnection con) => store = new(con);
    public void CreateTable() => store.EnsureSchema();
    public Task<long> SendAsync(long sender, long recipient, string subject, string body) => store.Send(sender, recipient, subject, body);
    public async Task<string> ListAsync(long user, long cursor, string by) => Gt6MailResponses.List(await store.Read(user, cursor, by));
    public Task<bool> ChangeAsync(long user, long id, string command) => store.Change(user, id, command);
}
