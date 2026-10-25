using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;

using GrimService.Utils;

namespace GrimService.Models.Xml
{
    [XmlRoot("ranking_list")]
    public class RankingList
    {
        [XmlElement("ranking")]
        public List<Ranking> Rankings { get; set; }
    }

    [XmlRoot("ranking")]
    public class Ranking
    {
        [XmlAttribute("ranking_id")]
        public int RankingId { get; set; }

        [XmlAttribute("board_id")]
        public string BoardId { get; set; }

        [XmlAttribute("score")]
        public string Score { get; set; } 

        [XmlAttribute("replay_id")]
        public string ReplayId { get; set; }

        [XmlAttribute("replay_mark_id")]
        public string ReplayMarkId { get; set; }

        [XmlAttribute("rank")]
        public long Rank { get; set; }

        [XmlAttribute("src_id")]
        public string SourceId { get; set; }

        [XmlAttribute("user_id")]
        public string UserId { get; set; }

        [XmlAttribute("comment")]
        public string Comment { get; set; }

        [XmlAttribute("stats")]
        public string Stats { get; set; }

        [XmlAttribute("nickname")]
        public string Nickname { get; set; }

        [XmlAttribute("country")]
        public string Country { get; set; }

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
