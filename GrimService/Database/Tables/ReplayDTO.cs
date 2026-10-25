using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrimService.Database.Tables
{
    public class ReplayDTO
    {
        public int Id { get; set; }

        public long UserId { get; set; }

        public string Username { get; set; }

        public string BoardId { get; set; }

        public string IsShort { get; set; }

        public DateTime CreateTime { get; set; }

        public ReplayDTO() { }

        public ReplayDTO(int userId, string username, string boardId, string isShort, DateTime createTime)
        {
            UserId = userId;
            Username = username;
            BoardId = boardId;
            IsShort = isShort;
            CreateTime = createTime;
        }
    }
}
