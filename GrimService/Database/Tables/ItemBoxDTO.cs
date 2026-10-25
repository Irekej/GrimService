using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrimService.Database.Tables
{
    public class ItemBoxDTO
    {
        public long Id { get; set; }

        public int UserId { get; set; }

        public string Sender { get; set; }

        public string Receiver { get; set; }

        public string Type { get; set; }

        public string Stats { get; set; }

        public string Comment { get; set; }

        public string Mac { get; set; }

        public DateTime CreateTime { get; set; }


        public ItemBoxDTO() { }

        public ItemBoxDTO(int userId, string sender, string receiver, string type, string stats, string comment, string mac, DateTime createTime)
        {
            UserId = userId;
            Sender = sender;
            Receiver = receiver;
            Type = type;
            Stats = stats;
            Comment = comment;
            Mac = mac;
            CreateTime = createTime;
        }
    }
}
