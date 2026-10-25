using System.Collections.Generic;
using System.IO;
using System.Xml;
using GrimService.Models.Xml;
using Microsoft.AspNetCore.Mvc;

namespace GrimService.Controllers.Clubs
{
    public static class ClubListResponses
    {
        public static ContentResult List(IEnumerable<ClubUser> clubs) => new ContentResult
        { Content = Serialize(clubs), ContentType = "application/xml", StatusCode = 200 };

        public static ContentResult Memberships(IEnumerable<ClubUser> clubs, string username) => new ContentResult
        { Content = ClubMembershipXml.Serialize(clubs, username), ContentType = "application/xml", StatusCode = 200 };

        public static string Serialize(IEnumerable<ClubUser> clubslist)
        {
            var settings = new XmlWriterSettings
            {
                OmitXmlDeclaration = true,
                Indent = false,
                NewLineHandling = NewLineHandling.None
            };

            using (var stringWriter = new StringWriter())
            using (var xmlWriter = XmlWriter.Create(stringWriter, settings))
            {
                xmlWriter.WriteStartElement("club_list");
                foreach (var club in clubslist)
                {
                    xmlWriter.WriteStartElement("community");
                    xmlWriter.WriteElementString("community_id", club.ClubId.ToString());
                    xmlWriter.WriteStartElement("member_list");
                    foreach (var user in club.UserIdList)
                    {
                        xmlWriter.WriteElementString("user_id", user);
                    }
                    xmlWriter.WriteEndElement();
                    xmlWriter.WriteElementString("open", club.Open);
                    xmlWriter.WriteElementString("active", club.IsActive ? "1" : "0");
                    xmlWriter.WriteElementString("level", "1");
                    xmlWriter.WriteElementString("country", club.Country);
                    xmlWriter.WriteElementString("title", club.Title);
                    xmlWriter.WriteElementString("description", club.Description);
                    xmlWriter.WriteElementString("recent", club.Recent);
                    xmlWriter.WriteStartElement("tags");
                    foreach (var tag in club.Tags)
                    {
                        xmlWriter.WriteElementString("tag", tag);
                    }
                    xmlWriter.WriteEndElement();
                    xmlWriter.WriteElementString("photo_id", club.PhotoId);
                    xmlWriter.WriteElementString("user_count", club.UserCount);
                    xmlWriter.WriteElementString("friend_count", club.FriendCount);
                    xmlWriter.WriteElementString("recent_update_time", club.RecentUpdateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"));
                    xmlWriter.WriteElementString("create_time", club.CreateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"));
                    xmlWriter.WriteEndElement();
                }
                xmlWriter.WriteEndElement();
                xmlWriter.Flush();
                return stringWriter.ToString();
            }
        }
    }
}
