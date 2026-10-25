using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;

using GrimService.Utils;

namespace GrimService.Models.Xml
{
    [XmlRoot("bspec_driver_list")]
    public class BSpecDriverList
    {
        [XmlElement("driver")]
        public List<BSpecDriver> Drivers { get; set; }
    }


    [XmlRoot("driver")]
    public class BSpecDriver
    {
        [XmlAttribute("driver_id")]
        public long DriverId { get; set; }

        [XmlAttribute("user_id")]
        public string UserId { get; set; }

        [XmlAttribute("name")]
        public string DriverName { get; set; }

        [XmlAttribute("driver_parameter")]
        public string DriverParameter { get; set; }

        [XmlAttribute("level")]
        public string DriverLevel { get; set; }

        [XmlAttribute("helmet")]
        public string DriverHelmet { get; set; }

        [XmlAttribute("wear")]
        public string DriverWear { get; set; }

        [XmlAttribute("helmet_color")]
        public string HelmetColor { get; set; }

        [XmlAttribute("wear_color")]
        public string WearColor { get; set; }

        [XmlAttribute("experimental")]
        public string Experimental { get; set; }

        [XmlAttribute("speed")]
        public string DriverSpeed { get; set; }

        [XmlAttribute("physical")]
        public string DriverPhysical { get; set; }

        [XmlAttribute("mental")]
        public string DriverMental { get; set; }

        [XmlAttribute("heart")]
        public string DriverHeart { get; set; }

        [XmlAttribute("career")]
        public string DriverCareer { get; set; }

        [XmlAttribute("win_count")]
        public string WinCount { get; set; }

        [XmlAttribute("race_count")]
        public string RaceCount { get; set; }

        [XmlAttribute("condition")]
        public string Condition { get; set; }

        [XmlAttribute("online_credit")]
        public long OnlineCredit { get; set; }
        [XmlAttribute("online_experience")]
        public long OnlineExperience { get; set; }
        [XmlAttribute("online_win_count")]
        public long OnlineWinCount { get; set; }
        [XmlAttribute("online_race_count")]
        public long OnlineRaceCount { get; set; }

        [XmlAttribute("status")]
        public int Status { get; set; }

        [XmlAttribute("lock_user_id")]
        public string LockUserId { get; set; }

        [XmlAttribute("create_time")]
        public DateTime CreateTime { get; set; }
    }
}
