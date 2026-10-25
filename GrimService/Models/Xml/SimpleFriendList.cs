using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.Xml.Serialization;

namespace GrimService.Models.Xml
{



    [XmlRoot("friends")]
    public class SimpleFriendList
    {
        [XmlElement("profile")]
        public List<SimpleFriend> Items { get; set; } = new List<SimpleFriend>();
    }




    [XmlRoot("profile")]
    public class SimpleFriend
    {



        [XmlAttribute("id")]
        public string UserId { get; set; }




        [XmlAttribute("aspec_level")]
        public string ASpecLevel { get; set; }




        [XmlAttribute("bspec_level")]
        public string BSpecLevel { get; set; }

        public SimpleFriend() { }

        public SimpleFriend(string userId, string aspec_level, string bspec_level)
        {
            UserId = userId;
            ASpecLevel = aspec_level;
            BSpecLevel = bspec_level;
        }
    }
}
