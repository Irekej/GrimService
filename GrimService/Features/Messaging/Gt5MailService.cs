using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GrimService.Database.Controllers;
using GrimService.Database.Tables;
using GrimService.Models.Xml;

namespace GrimService.Features.Messaging
{
    public interface IGt5MailRepository
    {
        Task<IEnumerable<GTMailDTO>> ListAsync(string username, long cursor, string mailbox);
        Task<long> AddAsync(GTMailDTO mail);
        Task<bool> DeleteAsync(long id, string username, bool sender);
    }

    public sealed class Gt5MailRepository : IGt5MailRepository
    {
        private readonly GTMailDBManager _mail;
        public Gt5MailRepository(GTMailDBManager mail) => _mail = mail;
        public Task<IEnumerable<GTMailDTO>> ListAsync(string username, long cursor, string mailbox) => mailbox switch
        {
            "receive" => _mail.GetAllMailsReceivedOfUserid(cursor, username),
            "send" => _mail.GetAllMailsSendedOfUserid(cursor, username),
            "all" => _mail.GetAllMailsOfUserid(cursor, username),
            _ => throw new ArgumentOutOfRangeException(nameof(mailbox))
        };
        public Task<long> AddAsync(GTMailDTO mail) => _mail.AddAsync(mail);
        public Task<bool> DeleteAsync(long id, string username, bool sender) => _mail.DeleteForUserAsync(id, username, sender);
    }

    public sealed class Gt5MailService
    {
        private readonly IGt5MailRepository _mail;
        private readonly IRecipientDirectory _recipients;
        public Gt5MailService(IGt5MailRepository mail, IRecipientDirectory recipients)
        { _mail = mail; _recipients = recipients; }

        public async Task<GTMailList> ListAsync(string username, long cursor, string mailbox)
        {
            if (cursor < 0 || !MailRules.IsMailbox(mailbox)) throw new ArgumentOutOfRangeException(nameof(cursor));
            var rows = await _mail.ListAsync(username, cursor, mailbox);
            return new GTMailList { gtmaillist = rows.Select(row => new gtmail
            {
                MailId = row.Id, FromUsername = row.FromUsername, ToUsername = row.ToUsername,
                FromNickname = row.FromNickname ?? row.FromUsername, ToNickname = row.ToNickname ?? row.ToUsername,
                Subject = row.Subject, Body = row.Body, CreateTime = row.CreateTime
            }).ToList() };
        }

        public async Task<MailSendResult> SendAsync(int userId, string from, string to, string subject, string body)
        {
            if (!MailRules.CanSend(to, subject, body)) return new(MailSendStatus.Invalid);
            if (await _recipients.FindAsync(to) == null) return new(MailSendStatus.RecipientMissing);
            long id = await _mail.AddAsync(new GTMailDTO(userId, from, to, subject, body, DateTime.Now));
            return new(MailSendStatus.Sent, id);
        }

        public Task<bool> DeleteAsync(long id, string username, bool sender) => id switch
        {
            0 => Task.FromResult(true),
            < 0 => Task.FromResult(false),
            _ => _mail.DeleteAsync(id, username, sender)
        };
    }
}
