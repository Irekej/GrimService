using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;

using GrimService.Utils;

namespace GrimService.Models.Xml
{
    [XmlRoot("bspec_car_list")]
    public class BSpecCarList
    {
        [XmlElement("car")]
        public List<BSpecCar> Cars { get; set; }
    }


    [XmlRoot("car")]
    public class BSpecCar
    {
        [XmlAttribute("car_id")]
        public long CarId { get; set; }

        [XmlAttribute("user_id")]
        public string UserId { get; set; }

        [XmlAttribute("car_label")]
        public string CarLabel { get; set; }

        [XmlAttribute("car_parameter")]
        public string CarParameter { get; set; }

        [XmlAttribute("status")]
        public int Status { get; set; }

        [XmlAttribute("lock_user_id")]
        public string LockUserId { get; set; }

        [XmlAttribute("thumbnail_photo_id")]
        public string CarThumbnail { get; set; }

        [XmlAttribute("lock_time")]
        public DateTime CreateTime { get; set; }
    }
}
