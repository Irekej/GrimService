using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrimService.Database.Tables
{
    public class ClubsDTO
    {
        public long Id { get; set; }

        public int UserId { get; set; }

        public string PSNUserId { get; set; }

        public string Country { get; set; }

        public string Title { get; set; }

        public string Description { get; set; }

        public string Recent { get; set; }

        public string Tag_1 { get; set; }

        public string Tag_2 { get; set; }

        public string Tag_3 { get; set; }

        public string Tag_4 { get; set; }

        public string Tag_5 { get; set; }

        public string Tag_6 { get; set; }

        public string Is_Open { get; set; }

        public string PhotoId { get; set; }

        public DateTime RecentUpdateTime { get; set; }

        public DateTime CreateTime { get; set; }


        public ClubsDTO() { }

        public ClubsDTO(int userId, string psnuserid, string country, string title, string description, string recent, string tag1, string tag2, string tag3, string tag4, string tag5, string tag6, string open, string imageId, DateTime recentupdateTime, DateTime createTime)
        {
            UserId = userId;
            PSNUserId = psnuserid;
            Country = country;
            Title = title;
            Description = description;
            Recent = recent;
            Tag_1 = tag1;
            Tag_2 = tag2;
            Tag_3 = tag3;
            Tag_4 = tag4;
            Tag_5 = tag5;
            Tag_6 = tag6;
            Is_Open = open;
            PhotoId = imageId;
            RecentUpdateTime = recentupdateTime;
            CreateTime = createTime;
        }
    }
}