using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrimService.Database.Tables
{
    public class RankingsDTO
    {
        public int Id { get; set; }

        public long UserId { get; set; }

        public string Username { get; set; }

        public string Country { get; set; }

        public string BoardId { get; set; }

        public string Score { get; set; }

        public string ReplayId { get; set; }
        public string ReplayMarkId { get; set; }

        public string SourceId { get; set; }

        public string Stats { get; set; }

        public long Rank { get; set; }

        public DateTime CreateTime { get; set; }

        public RankingsDTO() { }

        public RankingsDTO(int userId, string username, string country, string boardId, string scoreId, string stats, DateTime createTime)
        {
            UserId = userId;
            Username = username;
            Country = country;
            BoardId = boardId;
            Score = scoreId;
            Stats = stats;
            CreateTime = createTime;
        }
    }
}
