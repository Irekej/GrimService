using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using GrimService.Utils;

namespace GrimService.Features.Messaging;

public sealed class Gt6MailMessage
{
    public long Id { get; set; }
    public string FromName { get; set; }
    public string ToName { get; set; }
    public string FromNickname { get; set; }
    public string ToNickname { get; set; }
    public string Subject { get; set; }
    public string Body { get; set; }
    public DateTime Created { get; set; }
}

public static class Gt6MailResponses
{
    public static string List(IEnumerable<Gt6MailMessage> messages) => new XElement("gtmail_list",
        messages.Select(message => new XElement("gtmail",
            new XAttribute("mail_id", message.Id), new XAttribute("from", message.FromName),
            new XAttribute("to", message.ToName),
            new XAttribute("from_nickname", message.FromNickname ?? message.FromName),
            new XAttribute("to_nickname", message.ToNickname ?? message.ToName),
            new XAttribute("subject", message.Subject), new XAttribute("body", message.Body),
            new XAttribute("create_time", message.Created.ToRfc3339String())))).ToString(SaveOptions.DisableFormatting);
}
