using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.Xml.Serialization;
using GrimService.Utils;

namespace GrimService.Models.Xml
{
    [XmlRoot(ElementName = "course_list")]
    public class CourseList
    {
        [XmlElement("course")]
        public List<Course> Courses { get; set; }
    }


    [XmlRoot("course")]
    public class Course
    {
        [XmlAttribute("course_id")]
        public long CourseId { get; set; }

        [XmlAttribute("comment")]
        public string Comment { get; set; }

        [XmlAttribute("height")]
        public string Height { get; set; }

        [XmlAttribute("one_way")]
        public string OneWay { get; set; }




        [XmlAttribute("status")]
        public string Status { get; set; }

        [XmlAttribute("straight")]
        public string Straight { get; set; }

        [XmlAttribute("title")]
        public string Title { get; set; }




        [XmlAttribute("theme")]
        public string Theme { get; set; }

        [XmlAttribute("length")]
        public string Length { get; set; }




        [XmlAttribute("user_id")]
        public string OwnerId { get; set; }

        [XmlAttribute("corners")]
        public string Corners { get; set; }

        [XmlAttribute("source_user_id")]
        public string SourceUserId { get; set; }

        [XmlAttribute("photo_id")]
        public long PhotoId { get; set; }

        [XmlAttribute("photo_hidden")]
        public string PhotoHidden { get; set; }

        [XmlAttribute("title_hidden")]
        public string TitleHidden { get; set; }

        [XmlAttribute("comment_hidden")]
        public string CommentHidden { get; set; }

        [XmlAttribute("update_time")]
        public string UpdateTimeString
        {
            get => FormatTime(UpdateTime == default ? CreateTime : UpdateTime);
            set => UpdateTime = DateTimeExtensions.FromRfc3339String(value);
        }
        [XmlIgnore]
        public DateTime UpdateTime { get; set; }

        [XmlAttribute("create_time")]
        public string CreateTimeString
        {
            get => FormatTime(CreateTime);
            set => CreateTime = DateTimeExtensions.FromRfc3339String(value);
        }
        [XmlIgnore]
        public DateTime CreateTime { get; set; }

        [XmlAttribute("transfer_time")]
        public string TransferTimeString
        {
            get => FormatTime(TransferTime == default ? CreateTime : TransferTime);
            set => TransferTime = DateTimeExtensions.FromRfc3339String(value);
        }
        [XmlIgnore]
        public DateTime TransferTime { get; set; }



        private static string FormatTime(DateTime value)
            => value == default ? null : value.ToRfc3339String();
    }




    /*
    /// <summary>
    /// GT6 Courses
    /// </summary>
    [XmlRoot("course")]
    public class Course
    {
        [XmlAttribute("course_id")]
        public long CourseId { get; set; }

        [XmlAttribute("create_time")]
        public string CreateTimeString { get; set; }
        [XmlIgnore]
        public DateTime CreateTime
        {
            get => DateTimeExtensions.FromRfc3339String(CreateTimeString);
            set => CreateTimeString = value.ToRfc3339String();
        }

        [XmlAttribute("update_time")]
        public string UpdateTimeString { get; set; }
        [XmlIgnore]
        public DateTime UpdateTime
        {
            get => DateTimeExtensions.FromRfc3339String(UpdateTimeString);
            set => UpdateTimeString = value.ToRfc3339String();
        }

        /// <summary>
        /// PSN Name of the user that owns this course (but not the creator).
        /// </summary>
        [XmlAttribute(AttributeName = "user_id")]
        public string OwnerId { get; set; }

        /// <summary>
        /// 0 = Awaiting Test Drive, 1 = Complete, 2 = Public
        /// </summary>
        [XmlAttribute("status")]
        public string Status { get; set; }

        [XmlAttribute("photo_id")]
        public long PhotoId { get; set; }

        [XmlAttribute("title")]
        public string Title { get; set; }

        [XmlAttribute("comment")]
        public string Comment { get; set; }

        [XmlAttribute("title_hidden")]
        public string TitleHidden { get; set; }

        [XmlAttribute("comment_hidden")]
        public int CommentHidden { get; set; }

        [XmlAttribute("photo_hidden")]
        public string PhotoHidden { get; set; }

        /// <summary>
        /// Theme name. Important: For lobbies, this has to be the scenery course code.
        /// </summary>
        [XmlAttribute("theme")]
        public string Theme { get; set; }

        [XmlAttribute("length")]
        public string Length { get; set; }

        [XmlAttribute("one_way")]
        public string OneWay { get; set; }

        [XmlAttribute("corners")]
        public string Corners { get; set; }

        /// <summary>
        /// Name of the creator that created this course.
        /// </summary>
        [XmlAttribute("source_user_id")]
        public string OriginalCreator { get; set; }

        [XmlAttribute("straight")]
        public string Straight { get; set; }

        [XmlAttribute("height")]
        public string Height { get; set; }
    }
    */
}
