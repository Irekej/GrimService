using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.Xml;
using System.Xml.Serialization;

using GrimService.Database.Tables;
using GrimService.Utils;

namespace GrimService.Models.Xml
{
    [XmlRoot("friend_list")]
    public class UserProfileList
    {
        [XmlElement("user")]
        public List<UserProfile> UserProfile { get; set; }
    }

    [XmlRoot("user")]
    public class UserProfile
    {






        [XmlElement("id")]
        public string UserId { get; set; }

        [XmlElement("number")]
        public long Number { get; set; }

        [XmlElement("country")]
        public string Country { get; set; }

        [XmlElement("nickname_change")]
        public int NicknameChanges { get; set; }

        [XmlElement("license_level")]
        public string LicenseLevel { get; set; }

        [XmlElement("achievement")]
        public string Achievement { get; set; }

        [XmlElement("trophy")]
        public string Trophy { get; set; }

        [XmlElement("car_count")]
        public string CarCount { get; set; }

        [XmlElement("license_gold")]
        public string LicenseGold { get; set; }

        [XmlElement("odometer")]
        public string Odometer { get; set; }

        [XmlElement("aspec_level")]
        public string ASpecLevel { get; set; }

        [XmlElement("aspec_exp")]
        public string ASpecExp { get; set; }

        [XmlElement("bspec_level")]
        public string BSpecLevel { get; set; }

        [XmlElement("bspec_exp")]
        public string BSpecExp { get; set; }

        [XmlElement("win_count")]
        public string WinCount { get; set; }

        [XmlElement("license_silver")]
        public string LicenseSilver { get; set; }

        [XmlElement("license_bronze")]
        public string LicenseBronze { get; set; }

        [XmlElement("credit")]
        public string Credit { get; set; }

        [XmlElement("profile_level")]
        public string ProfileLevel { get; set; }

        [XmlElement("comment")]
        public string Comment { get; set; }

        [XmlArray("tags")]
        [XmlArrayItem("tag")]
        public List<string> Tags { get; set; }




        [XmlElement("recent")]
        public string Recent { get; set; }

        [XmlElement("recent_update")]
        public DateTime RecentUpdateTime { get; set; }


        [XmlElement("recent_update_time")]
        public string RecentUpdateTimeNative { get; set; }

        [XmlElement("welcomemessage")]
        public string WelcomeMessage { get; set; }

        [XmlElement("nickname")]
        public string Nickname { get; set; }

        [XmlElement("helmet")]
        public long HelmetId { get; set; }

        [XmlElement("helmet_color")]
        public long HelmetColorId { get; set; }

        [XmlElement("wear")]
        public long WearId { get; set; }

        [XmlElement("wear_color")]
        public long WearColorId { get; set; }

        [XmlElement("menu_color")]
        public long MenuColor { get; set; }

        [XmlElement("menu_matiere")]
        public long MenuMatiere { get; set; }

        [XmlElement("menu_helmet")]
        public long MenuHelmet { get; set; }

        [XmlElement("menu_helmet_color")]
        public long MenuHelmetColor { get; set; }

        [XmlElement("menu_wear")]
        public long MenuWear { get; set; }

        [XmlElement("menu_wear_color")]
        public long MenuWearColor { get; set; }

        [XmlElement("photo_avatar")]
        public string PhotoIdAvatar { get; set; }

        [XmlElement("photo_bg")]
        public string PhotoBackground { get; set; }

        [XmlElement("band_test")]
        public int BandTest { get; set; }

        [XmlElement("band_up")]
        public int BandUp { get; set; }

        [XmlElement("band_down")]
        public int BandDown { get; set; }

        [XmlElement("band_update_time")]
        public string BandUpdateTime { get; set; }

        public UserProfile()
        {
            Tags = new List<string>();
        }
    }
}
