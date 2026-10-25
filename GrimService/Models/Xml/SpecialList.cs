using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.Xml.Serialization;

namespace GrimService.Models.Xml
{
    [XmlRoot("user_special_list")]
    public class SpecialList
    {
        [XmlElement("user_special")]
        public List<UserSpecial> Items { get; set; } = new List<UserSpecial>();
    }




    [XmlRoot("user_special")]
    public class UserSpecial
    {
        [XmlAttribute("user_id")]
        public string UserId { get; set; }

        [XmlAttribute("key")]
        public string Key { get; set; }

        [XmlAttribute("value")]
        public string Value { get; set; }

        [XmlAttribute("type")]
        public int Type { get; set; }

        public UserSpecial() { }

        public UserSpecial(string userId, int type, string key, string value)
        {
            UserId = userId;
            Key = key;
            Value = value;
            Type = type;
        }
    }
}
