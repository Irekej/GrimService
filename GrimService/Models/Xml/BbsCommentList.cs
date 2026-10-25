using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.Xml.Serialization;
using GrimService.Utils;

namespace GrimService.Models.Xml
{



    [XmlRoot("bbs_comment_list")]
    public class BbsCommentList
    {
        [XmlElement("bbs_comment")]
        public List<BbsComment> Comments { get; set; } = new();
    }

    public class BbsComment
    {



        [XmlAttribute("bbs_comment_id")]
        public long CommentId { get; set; }




        [XmlAttribute("bbs_board_id")]
        public long BoardId { get; set; }




        [XmlAttribute("user_id")]
        public string UserId { get; set; }




        [XmlAttribute("comment")]
        public string Comment { get; set; }




        [XmlAttribute("nickname")]
        public string Nickname { get; set; }




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
