namespace GrimService.Features.Messaging
{
    public static class MailRules
    {
        public static bool IsMailbox(string mailbox) => mailbox == "all" || mailbox == "send" || mailbox == "receive";
        public static bool CanSend(string to, string subject, string body) =>
            !string.IsNullOrWhiteSpace(to) && to.Length <= 32 && subject != null && body != null &&
            subject.Length <= 256 && body.Length <= 4000;
    }

    public enum MailSendStatus { Sent, Invalid, RecipientMissing }
    public sealed record MailSendResult(MailSendStatus Status, long Id = 0);
}
