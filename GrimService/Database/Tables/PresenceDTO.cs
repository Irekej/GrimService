using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrimService.Database.Tables
{
    public class PresenceDTO
    {



        public int Id { get; set; }




        public long UserId { get; set; }




        public string PSNUserId { get; set; }




        public string Stats { get; set; }




        public DateTime PresenceUpdateTime { get; set; }

    }
}
