using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.Xml.Serialization;

namespace GrimService.Models.Xml
{



    [XmlRoot("friend_list")]
    public class GT6FriendList
    {
        [XmlElement("profile")]
        public List<GT6Friend> Items { get; set; } = new List<GT6Friend>();
    }




    [XmlRoot("profile")]
    public class GT6Friend
    {
        [XmlElement("id")]
        public string UserId { get; set; }

        [XmlElement("country")]
        public string Country { get; set; }

        [XmlElement("nickname")]
        public string Nickname { get; set; }

        [XmlElement("photo_avatar")]
        public string PhotoIdAvatar { get; set; }

        public GT6Friend() { }

        public GT6Friend(string userId, string country, long photo_id_avatar)
        {
            UserId = userId;


        }
    }
}
