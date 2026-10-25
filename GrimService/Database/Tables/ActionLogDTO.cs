using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrimService.Database.Tables
{
    public class ActionLogDTO
    {
        public int Id { get; set; }




        public int UserId { get; set; }




        public DateTime CreateTime { get; set; }

        public string Value1 { get; set; }
        public string Value2 { get; set; }
        public string Value3 { get; set; }
        public string Value4 { get; set; }
        public string Value5 { get; set; }

        public ActionLogDTO() { }

        public ActionLogDTO(int userId, DateTime createTime, 
            string value1 = "", string value2 = "", string value3 = "", string value4 = "", string value5 = "")
        {
            UserId = userId;
            CreateTime = createTime;
            Value1 = value1;
            Value2 = value2;
            Value3 = value3;
            Value4 = value4;
            Value5 = value5;
        }
    }
}
