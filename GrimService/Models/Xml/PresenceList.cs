using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;

using GrimService.Utils;

namespace GrimService.Models.Xml
{
    [XmlRoot(ElementName = "presence_list")]
    public class PresenceList
    {
        [XmlElement("user_presence")]
        public List<Presence> Presences { get; set; } = new();
    }

    [XmlRoot(ElementName = "user_presence")]
    public class Presence
    {
        [XmlAttribute(AttributeName = "id")]
        public string Id { get; set; }

        [XmlAttribute(AttributeName = "stats")]
        public string Stats { get; set; }

        [XmlAttribute(AttributeName = "type")]
        public int Type { get; set; }

        [XmlAttribute(AttributeName = "update_time")]
        public string UpdateTimeString { get; set; }
        [XmlIgnore]
        public DateTime UpdateTime
        {
            get => DateTimeExtensions.FromRfc3339String(UpdateTimeString);
            set => UpdateTimeString = value.ToRfc3339String();
        }
    }

    public enum PresenceState
    {
        Offline,
        OutOfContext,
        SameContext,
        Error
    }
}
