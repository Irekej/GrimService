using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrimService.Database.Tables
{

    public class CourseGT5DTO
    {



        public long Id { get; set; }




        public long UserId { get; set; }




        public string PSNUserId { get; set; } = null;

        public string Title { get; set; } = null;

        public string Comment { get; set; } = null;

        public string Genre { get; set; } = null;

        public string Length { get; set; } = null;

        public string Weather { get; set; } = null;

        public string StartDatetime { get; set; } = null;

        public string Author { get; set; } = null;

        public string Elevation { get; set; } = null;

        public string Straight { get; set; } = null;

        public string Corner { get; set; } = null;

        public string Datetime { get; set; } = null;

        public string OnlineRace { get; set; } = null;

        public string WeatherValue0 { get; set; } = null;

        public string InitialRetention { get; set; } = null;

        public string WeatherAccel { get; set; } = null;

        public string TimeProgressSpeed { get; set; } = null;

        public string PhotoId { get; set; } = null;

        public DateTime UpdateTime { get; set; }

        public DateTime CreateTime { get; set; }

        public CourseGT5DTO() { }

        public CourseGT5DTO(long userId, string username, string title, string comment, string genre, string length, string weather, string start_datetime, string author, string elevation, string straight, string corner, string datetime, string online_race, string weather_value0, string initial_retention, string weather_accel, string time_progress_speed, DateTime updateTime, DateTime createTime)
        {
            UserId = userId;
            PSNUserId = username;
            Title = title;
            Comment = comment;
            Genre = genre;
            Length = length;
            Weather = weather;
            StartDatetime = start_datetime;
            Author = author;
            Elevation = elevation;
            Straight = straight;
            Corner = corner;
            Datetime = datetime;
            OnlineRace = online_race;
            WeatherValue0 = weather_value0;
            InitialRetention = initial_retention;
            WeatherAccel = weather_accel;
            TimeProgressSpeed = time_progress_speed;
            UpdateTime = updateTime;
            CreateTime = createTime;
        }

    }
}
