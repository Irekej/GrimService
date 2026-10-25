using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrimService.Database.Tables
{
    public class BSpecCarDTO
    {
        public long Id { get; set; }

        public int UserId { get; set; }

        public int CarId { get; set; }

        public string CarLabel { get; set; }

        public string CarParameter { get; set; }

        public string CarThumbnail { get; set; }




        public DateTime CreateTime { get; set; }


        public BSpecCarDTO() { }

        public BSpecCarDTO(int userId, string carLabel, string carParameter, string carThumbnail, DateTime createTime)
        {
            UserId = userId;
            CarLabel = carLabel;
            CarParameter = carParameter;
            CarThumbnail = carThumbnail;
            CreateTime = createTime;
        }
    }
}
