using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrimService.Models
{



    public class SessionToken
    {



        public string Token { get; set; }




        public DateTime ExpiryDate { get; set; }

        public SessionToken(string token, DateTime expiry)
        {
            Token = token;
            ExpiryDate = expiry;
        }
    }
}
