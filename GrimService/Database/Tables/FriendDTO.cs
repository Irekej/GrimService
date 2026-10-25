using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrimService.Database.Tables
{
    public class FriendDTO
    {



        public long Id { get; set; }




        public long UserId { get; set; }




        public long FriendId { get; set; }

        public FriendDTO() { }

        public FriendDTO(long userId, long friendId)
        {

            UserId = userId;
            FriendId = friendId;
        }
    }
}
