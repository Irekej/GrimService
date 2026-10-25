using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.Xml.Serialization;
using GrimService.Utils;

namespace GrimService.Models.Xml
{
    [XmlRoot("items")]
    public class ItemBoxList
    {
        [XmlElement("item")]
        public List<ItemBox> Items { get; set; }
    }

    [XmlRoot("item")]
    public class ItemBox
    {
        [XmlAttribute("itembox_id")]
        public long ItemBoxId { get; set; }

        [XmlAttribute("sender")]
        public string Sender { get; set; }

        [XmlAttribute("receiver")]
        public string Receiver { get; set; }

        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlAttribute("stats")]
        public string Stats { get; set; }

        [XmlAttribute("comment")]
        public string Comment { get; set; }

        [XmlAttribute("mac")]
        public string MacAddress { get; set; }

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
