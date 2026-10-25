using System;
using System.Xml.Serialization;

namespace GrimService.Models.Xml
{
    [XmlRoot("misc2")]
    public class misc2
    {
        [XmlAttribute("status")]
        public string matchingstatus { get; set; }

        [XmlAttribute("titleid")]
        public string matchingtitleid { get; set; }

        [XmlAttribute("port")]
        public string matchingport { get; set; }

        [XmlAttribute("id")]
        public string matchingid { get; set; }
    }
}
