using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;

using GrimService.Utils;

namespace GrimService.Models.Xml
{
    [XmlRoot("gtmail_list")]
    public class GTMailList
    {
        [XmlElement("gtmail")]
        public List<gtmail> gtmaillist { get; set; }
    }

    [XmlRoot("gtmail")]
    public class gtmail
    {
        [XmlAttribute("mail_id")]
        public long MailId { get; set; }

        [XmlAttribute("from")]
        public string FromUsername { get; set; }

        [XmlAttribute("to")]
        public string ToUsername { get; set; }

        [XmlAttribute("from_nickname")]
        public string FromNickname { get; set; }

        [XmlAttribute("to_nickname")]
        public string ToNickname { get; set; }

        [XmlAttribute("subject")]
        public string Subject { get; set; }

        [XmlAttribute("body")]
        public string Body { get; set; }

        [XmlAttribute("create_time")]
        public string CreateTimeString { get; set; }
        [XmlIgnore]
        public DateTime CreateTime
        {
            get => DateTimeExtensions.FromRfc3339String(CreateTimeString);
            set => CreateTimeString = value.ToRfc3339String();
        }

    }
}
