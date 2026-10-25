using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.Xml.Serialization;
using GrimService.Utils;

namespace GrimService.Models.Xml
{



    [XmlRoot("photos")]
    public class PhotoList
    {
        [XmlElement("photo")]
        public List<Photo> Photos { get; set; } = new();
    }

    [XmlRoot("photo")]
    public class Photo
    {



        [XmlAttribute("photo_id")]
        public long PhotoId { get; set; }

        /*
        [XmlAttribute("user_id")]
        public string Username { get; set; }
        */




        [XmlAttribute("place")]
        public string Place { get; set; }




        [XmlAttribute("car_name")]
        public string CarName { get; set; }




        [XmlAttribute("user_id")]
        public string UserId { get; set; }




        [XmlAttribute("comment")]
        public string Comment { get; set; }

        [XmlAttribute("type")]
        public string Type { get; set; }




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
