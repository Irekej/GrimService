using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using GrimService.Database.Tables;

namespace GrimService.Models
{



    public class Player
    {



        public UserDTO Data { get; }




        public SessionToken Token { get; set; }




        public DateTime LastUpdate { get; set; }




        public void SetLastUpdatedNow()
            => LastUpdate = DateTime.Now;

        public override string ToString()
            => Data.PSNUserId;

        public Player(UserDTO user, SessionToken token)
        {
            Data = user;
            Token = token;
        }
    }
}
