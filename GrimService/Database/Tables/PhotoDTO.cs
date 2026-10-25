using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrimService.Database.Tables
{
    public class PhotoDTO
    {
        public long Id { get; set; }




        public int UserId { get; set; }

        public string Username { get; set; }

        public string Comment { get; set; }
        public string CarName { get; set; }
        public string Place { get; set; }
        public string Type { get; set; }




        public DateTime CreateTime { get; set; }

        public PhotoDTO() { }

        public PhotoDTO(int userId, string username, string comment, string carName, string place, string type, DateTime createTime)
        {
            UserId = userId;
            Username = username;
            Comment = comment;
            CarName = carName;
            Place = place;
            Type = type;
            CreateTime = createTime;
        }
    }
}
