using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrimService.Database.Tables
{
    public class UserSpecialDTO
    {



        public long Id { get; set; }




        public long UserId { get; set; }
        



        public int Type { get; set; }

        /* CAR_000x
         * 1: A gift for your participation in GT Academy 2013
         * 2: A gift for your participation in the BMW Z4 Challenge
         * 3-7: Entry Bonus: GT5 Final Event
         * 8: A gift for your participation in GT Academy 2014 */
        public string Key { get; set; }




        public string Value { get; set; }
    }
}
