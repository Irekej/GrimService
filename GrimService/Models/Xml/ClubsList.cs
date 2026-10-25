using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.Xml.Serialization;
using GrimService.Utils;

namespace GrimService.Models.Xml
{
    [XmlRoot("clubs_list")]
    public class ClubsList
    {
        [XmlElement("community")]
        public List<ClubUser> Clubs { get; set; }
    }

    [XmlRoot("community")]
    public class ClubUser
    {
        [XmlElement("community_id")]
        public long ClubId { get; set; }

        [XmlElement("user_id")]
        public string UserId { get; set; }

        [XmlElement("country")]
        public string Country { get; set; }

        [XmlElement("title")]
        public string Title { get; set; }

        [XmlElement("description")]
        public string Description { get; set; }

        [XmlElement("recent")]
        public string Recent { get; set; }

        [XmlArray("user_id_list")]
        [XmlArrayItem("user_id")]
        public List<string> UserIdList { get; set; }

        [XmlArray("tags")]
        [XmlArrayItem("tag")]
        public List<string> Tags { get; set; }



        [XmlElement("open")]
        public string Open { get; set; }

        [XmlElement("photo_id")]
        public string PhotoId { get; set; }

        [XmlElement("user_count")]
        public string UserCount { get; set; }

        [XmlElement("friend_count")]
        public string FriendCount { get; set; }

        [XmlElement("active")]
        public bool IsActive { get; set; }

        [XmlElement("recent_update_time")]
        public DateTime RecentUpdateTime { get; set; }

        [XmlElement("create_time")]
        public DateTime CreateTime { get; set; }

        public ClubUser()
        {
            UserIdList = new List<string>();
            Tags = new List<string>();
        }
    }
}
