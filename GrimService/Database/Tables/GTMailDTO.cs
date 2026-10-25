using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrimService.Database.Tables
{
    public class GTMailDTO
    {
        public long Id { get; set; }

        public int UserId { get; set; }

        public string FromUsername { get; set; }

        public string ToUsername { get; set; }
        public string FromNickname { get; set; }
        public string ToNickname { get; set; }

        public string Subject { get; set; }

        public string Body { get; set; }

        public DateTime CreateTime { get; set; }


        public GTMailDTO() { }

        public GTMailDTO(int userId, string fromusername, string tousername, string subject, string body, DateTime createTime)
        {
            UserId = userId;
            FromUsername = fromusername;
            ToUsername = tousername;
            Subject = subject;
            Body = body;
            CreateTime = createTime;
        }
    }
}
