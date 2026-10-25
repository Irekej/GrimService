using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading.Tasks;

namespace GrimService.Database.Tables
{

    public class CourseDTO
    {



        public long Id { get; set; }




        public long UserId { get; set; }

        public string PSNUserId { get; set; }

        public string Comment { get; set; }

        public string Height { get; set; }

        public string OneWay { get; set; }

        public string Status { get; set; }

        public string Straight { get; set; }

        public string Title { get; set; }

        public string Theme { get; set; }

        public string Length { get; set; }

        public string OwnerId { get; set; }

        public string Corners { get; set; }

        public string SourceUserId { get; set; }

        public long PhotoId { get; set; }

        public string PhotoHidden { get; set; }

        public string CommentHidden { get; set; }

        public string TitleHidden { get; set; }

        public DateTime UpdateTime { get; set; }

        public DateTime CreateTime { get; set; }

        public DateTime TransferTime { get; set; }

        public CourseDTO() { }

        public CourseDTO(long userId, string status, string title, string ownerid, string sourceuserid, DateTime createTime)
        {
            UserId = userId;
            Status = status;
            Title = title;
            OwnerId = ownerid;
            SourceUserId = sourceuserid;
            CreateTime = createTime;
        }
    }


    /*
    public class CourseDTO
    {
        /// <summary>
        /// Row Id
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// Internal Database Id of the main user.
        /// </summary>
        public long UserId { get; set; }

        public string Title { get; set; }

        public string Comment { get; set; }

        public string Genre { get; set; }

        public string Length { get; set; }

        public string Weather { get; set; }

        public string Start_datetime { get; set; }

        public string Author { get; set; }

        public string Elevation { get; set; }

        public string Straight { get; set; }

        public string Corner { get; set; }

        public string Datetime { get; set; }

        public string Online_race { get; set; }

        public string Weather_value0 { get; set; }

        public string Initial_retention { get; set; }

        public string Weather_accel { get; set; }

        public string Time_progress_speed { get; set; }

        public DateTime CreateTime { get; set; }

        //GT6

        /// <summary>
        /// Internal Database Id of the friend.
        /// </summary>
        public long FriendId { get; set; }

        public DateTime UpdateTime { get; set; }

        public string Status { get; set; }

        public long PhotoId { get; set; }

        public string TitleHidden { get; set; }

        public string CommentHidden { get; set; }

        public string PhotoHidden { get; set; }

        public string Theme { get; set; }

        public string OneWay { get; set; }

        public string Corners { get; set; }

        /// <summary>
        /// Internal database id of the source user who created this track.
        /// </summary>
        public string SourceUserId { get; set; }

        public string Height { get; set; }
    }
    */
}
