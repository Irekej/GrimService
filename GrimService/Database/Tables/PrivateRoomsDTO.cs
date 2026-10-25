using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrimService.Database.Tables
{
    public class PrivateRoomsDTO
    {
        public int Id { get; set; }

        public long UserId { get; set; }

        public string Username { get; set; }

        public string Stats { get; set; }

        public DateTime CreateTime { get; set; }

        public PrivateRoomsDTO() { }

        public PrivateRoomsDTO(int userId, string username, string stats, DateTime createTime)
        {
            UserId = userId;
            Username = username;
            Stats = stats;
            CreateTime = createTime;
        }
    }
}
