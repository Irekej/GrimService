using System;
using System.Threading.Tasks;
using GrimService.Database.Controllers;

namespace GrimService.Features.Messaging
{
    public interface IGt6MailRepository
    {
        Task<string> ListAsync(long userId, long cursor, string mailbox);
        Task<long> SendAsync(long sender, long recipient, string subject, string body);
        Task<bool> ChangeAsync(long userId, long id, string command);
    }

    public sealed class Gt6MailRepository : IGt6MailRepository
    {
        private readonly GT6MailDBManager _mail;
        public Gt6MailRepository(GT6MailDBManager mail) => _mail = mail;
        public Task<string> ListAsync(long userId, long cursor, string mailbox) => _mail.ListAsync(userId, cursor, mailbox);
        public Task<long> SendAsync(long sender, long recipient, string subject, string body) => _mail.SendAsync(sender, recipient, subject, body);
        public Task<bool> ChangeAsync(long userId, long id, string command) => _mail.ChangeAsync(userId, id, command);
    }

    public sealed class Gt6MailService
    {
        private readonly IGt6MailRepository _mail;
        private readonly IRecipientDirectory _recipients;
        public Gt6MailService(IGt6MailRepository mail, IRecipientDirectory recipients)
        { _mail = mail; _recipients = recipients; }

        public Task<string> ListAsync(long userId, long cursor, string mailbox)
        {
            if (cursor < 0 || !MailRules.IsMailbox(mailbox)) throw new ArgumentOutOfRangeException(nameof(cursor));
            return _mail.ListAsync(userId, cursor, mailbox);
        }

        public async Task<MailSendResult> SendAsync(long sender, string to, string subject, string body)
        {
            if (!MailRules.CanSend(to, subject, body)) return new(MailSendStatus.Invalid);
            var recipient = await _recipients.FindAsync(to);
            if (recipient == null) return new(MailSendStatus.RecipientMissing);
            long id = await _mail.SendAsync(sender, recipient.Value, subject, body);
            return new(MailSendStatus.Sent, id);
        }

        public Task<bool> ChangeAsync(long userId, long id, string command)
        {
            if (id <= 0 || (command != "mail.delete.from" && command != "mail.delete.to" && command != "mail.read.to"))
                return Task.FromResult(false);
            return _mail.ChangeAsync(userId, id, command);
        }
    }
}
