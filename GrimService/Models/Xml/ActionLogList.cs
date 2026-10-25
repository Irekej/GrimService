using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.Xml.Serialization;
using GrimService.Utils;

namespace GrimService.Models.Xml
{



    [XmlRoot("actionlog_list")]
    public class ActionLogList
    {
        [XmlElement("actionlog")]
        public List<ActionLog> Logs { get; set; } = new();
    }

    [XmlRoot("actionlog")]
    public class ActionLog
    {
        public static Dictionary<string, string> Tags = new()
        {
            { "NG", "NEW_GAME" },
            { "CB", "CAR_BUY" },
            { "CR", "CAR_RIDE" },
            { "FR", "FRIENDCAR_RIDE" },
            { "LL", "LICENSE_LEVEL" },
            { "LE", "LICENSE_EVENT" },
            { "AL", "ASPEC_LEVEL" },
            { "AE", "ASPEC_EVENT" },
            { "BL", "BSPEC_LEVEL" },
            { "BE", "BSPEC_EVENT" },
            { "SE", "SPECIAL_EVENT" },
            { "DC", "DRIVER_CLASS" },
            { "GS", "GIFT_SPECIAL" },
            { "GM", "GIFT_MUSEUMCARD" },
            { "GT", "GIFT_TUNEPARTS" },
            { "GO", "GIFT_OTHERPARTS" },
            { "GD", "GIFT_DRIVER_ITEM" },
            { "TU", "TROPHY_UNLOCK" },
        };










        [XmlAttribute("create_time")]
        public string CreateTimeString { get; set; }
        [XmlIgnore]
        public DateTime CreateTime
        {
            get => DateTimeExtensions.FromRfc3339String(CreateTimeString);
            set => CreateTimeString = value.ToRfc3339String();
        }










        [XmlAttribute("value1")]
        public string Value1 { get; set; }




        [XmlAttribute("value2")]
        public string Value2 { get; set; }




        [XmlAttribute("value3")]
        public string Value3 { get; set; }




        [XmlAttribute("value4")]
        public string Value4 { get; set; }




        [XmlAttribute("value5")]
        public string Value5 { get; set; }

    }
}
