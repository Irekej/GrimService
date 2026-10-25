using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GrimService.Database.Tables
{
    public class UserDTO
    {



        public int Id { get; set; }




        public string PSNUserId { get; set; }




        public string IPAddress { get; set; }




        public string Country { get; set; }




        public string MacAddress { get; set; }





        public int NicknameChanges { get; set; } = DefaultNicknameChangeCount;
        public const int DefaultNicknameChangeCount = 3;




        public string LicenseLevel { get; set; }




        public string Achievement { get; set; }




        public string Trophy { get; set; }




        public string CarCount { get; set; }




        public string LicenseGold { get; set; }




        public string Odometer { get; set; }




        public string ASpecLevel { get; set; }




        public string ASpecExp { get; set; }




        public string BSpecLevel { get; set; }




        public string BSpecExp { get; set; }




        public string WinCount { get; set; }




        public string LicenseSilver { get; set; }




        public string LicenseBronze { get; set; }




        public string Credit { get; set; }




        public string ProfileLevel { get; set; }




        public string Comment { get; set; }




        public string TagIdCSV { get; set; }




        public string Recent { get; set; }




        public DateTime RecentUpdateTime { get; set; }




        public string Nickname { get; set; }




        public int Helmet { get; set; }




        public int HelmetColor { get; set; }




        public int Wear { get; set; }




        public int WearColor { get; set; }




        public int MenuColor { get; set; }




        public int MenuMatiere { get; set; }




        public int MenuHelmet { get; set; }




        public int MenuHelmetColor { get; set; }




        public int MenuWear { get; set; }




        public int MenuWearColor { get; set; }




        public string WelcomeMessage { get; set; }




        public string BanStatus { get; set; }




        public string PhotoIdAvatar { get; set; }




        public string PhotoBackground { get; set; }




        public int BandTest { get; set; }




        public int BandUp { get; set; }




        public int BandDown { get; set; }




        public DateTime BandUpdateTime { get; set; }




        public string LastGame { get; set; }




        public string DiscordId { get; set; }




        public long NumbersOfLogin { get; set; }




        public DateTime LastActivity { get; set; }




        public string PlayerToken { get; set; }




        public DateTime TokenExpiry { get; set; }




        public string Platform { get; set; }

        public string AuthProvider { get; set; }
        public long? RpcnUserId { get; set; }




        public string ConsoleId { get; set; }















    }
}
