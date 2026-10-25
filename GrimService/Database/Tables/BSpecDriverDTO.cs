using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrimService.Database.Tables
{
    public class BSpecDriverDTO
    {
        public long Id { get; set; }

        public int UserId { get; set; }

        public int DriverId { get; set; }

        public bool EntryLocked { get; set; }
        public string EntryLockUser { get; set; }
        public long OnlineCredit { get; set; }
        public long OnlineExperience { get; set; }
        public long OnlineWinCount { get; set; }
        public long OnlineRaceCount { get; set; }

        public string DriverName { get; set; }

        public string DriverLevel { get; set; }

        public string DriverHelmet { get; set; }

        public string DriverWear { get; set; }

        public string HelmetColor { get; set; }

        public string WearColor { get; set; }

        public string Experimental { get; set; }

        public string DriverSpeed { get; set; }

        public string DriverPhysical { get; set; }

        public string DriverMental { get; set; }

        public string DriverHeart { get; set; }

        public string DriverCareer { get; set; }

        public string WinCount { get; set; }

        public string RaceCount { get; set; }

        public string Condition { get; set; }

        public string DriverParameter { get; set; }




        public DateTime CreateTime { get; set; }


        public BSpecDriverDTO() { }

        public BSpecDriverDTO(int userId, string driverName, string driverLevel, string driverHelmet, string driverWear, string helmetColor, string wearColor, string experimental, string driverSpeed, string driverPhysical, string driverMental, string driverHeart, string driverCareer, string winCount, string raceCount, string condition, string driverParameter, DateTime createTime)
        {
            UserId = userId;
            DriverName = driverName;
            DriverLevel = driverLevel;
            DriverHelmet = driverHelmet;
            DriverWear = driverWear;
            HelmetColor = helmetColor;
            WearColor = wearColor;
            Experimental = experimental;
            DriverSpeed = driverSpeed;
            DriverPhysical = driverPhysical;
            DriverMental = driverMental;
            DriverHeart = driverHeart;
            DriverCareer = driverCareer;
            WinCount = winCount;
            RaceCount = raceCount;
            Condition = condition;
            DriverParameter = driverParameter;
            CreateTime = createTime;
        }
    }
}
