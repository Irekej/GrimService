using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using GrimService.Database.Tables;
using GrimService.Utils;

namespace GrimService.Models.Xml
{
    [XmlRoot("private_room")]
    public class PrivateRoom
    {

        [XmlElement("user_id")]
        public string UserId { get; set; }

        [XmlElement("nickname")]
        public string Nickname { get; set; }

        [XmlElement("stats")]
        public string Stats { get; set; }

        [XmlElement("create_time")]
        public string CreateTimeString { get; set; }

        public static PrivateRoom FromDatabaseObject(PrivateRoomsDTO config)
        {
            var privateconfig = new PrivateRoom();
            privateconfig.UserId = config.Username;
            privateconfig.Nickname = config.Username;
            privateconfig.Stats = config.Stats;
            privateconfig.CreateTime = config.CreateTime;

            return privateconfig;
        }

        [XmlIgnore]
        public DateTime CreateTime
        {
            get => DateTimeExtensions.FromRfc3339String(CreateTimeString);
            set => CreateTimeString = value.ToRfc3339String();
        }
    }
}
