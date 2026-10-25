using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.Xml.Serialization;
using GrimService.Utils;

namespace GrimService.Models.Xml
{
    [XmlRoot("course_list")]
    public class CourseListGT5
    {
        [XmlElement("course")]
        public List<CoursesGT5> CoursesGT5 { get; set; }
    }


    [XmlRoot("course")]
    public class CoursesGT5
    {
        [XmlAttribute("course_id")]
        public long CourseId { get; set; }

        [XmlAttribute("user_id")]
        public long UserId { get; set; }

        [XmlAttribute("psnuserid")]
        public string Psnuserid { get; set; }

        [XmlAttribute("title")]
        public string Title { get; set; }

        [XmlAttribute("comment")]
        public string Comment { get; set; }

        [XmlAttribute("length")]
        public string Length { get; set; }

        [XmlAttribute("weather")]
        public string Weather { get; set; }

        [XmlAttribute("start_datetime")]
        public string StartDatetime { get; set; }

        [XmlAttribute("author")]
        public string Author { get; set; }

        [XmlAttribute("elevation")]
        public string Elevation { get; set; }

        [XmlAttribute("straight")]
        public string Straight { get; set; }

        [XmlAttribute("corner")]
        public string Corner { get; set; }

        [XmlAttribute("datetime")]
        public string Datetime { get; set; }

        [XmlAttribute("online_race")]
        public string OnlineRace { get; set; }

        [XmlAttribute("weather_value0")]
        public string WeatherValue0 { get; set; }

        [XmlAttribute("initial_retention")]
        public string InitialRetention { get; set; }

        [XmlAttribute("weather_accel")]
        public string WeatherAccel { get; set; }

        [XmlAttribute("time_progress_speed")]
        public string TimeProgressSpeed { get; set; }

        [XmlAttribute("photo_id")]
        public string PhotoId { get; set; }

        [XmlAttribute("update_time")]
        public DateTime UpdateTime { get; set; }

        [XmlAttribute("create_time")]
        public DateTime CreateTime { get; set; }

    }

}
