using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.Xml;
using System.Xml.Serialization;
using GrimService.Utils;

namespace GrimService.Models.Xml
{



    [XmlRoot("grim")]
    public class TicketResult
    {



        [XmlElement("result")]
        public string Result { get; set; }




        [XmlElement("user_id")]
        public string UserId { get; set; }




        [XmlElement("nickname")]
        public string Nickname { get; set; }




        [XmlElement("user_no")]
        public int UserNumber { get; set; }




        [XmlElement("server_time")]
        public string ServerTimeString { get; set; }
        [XmlIgnore]
        public DateTime ServerTime
        {
            get => DateTimeExtensions.FromRfc3339String(ServerTimeString);
            set => ServerTimeString = value.ToRfc3339String();
        }
    }
}
