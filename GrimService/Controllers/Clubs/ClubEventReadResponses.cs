using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using GrimService.Database.Tables;
using GrimService.Features.Clubs;
using GrimService.Models.Xml;
using Microsoft.AspNetCore.Mvc;

namespace GrimService.Controllers.Clubs
{
    public static class ClubEventReadResponses
    {
        private static ContentResult Xml(string root, object rows) => new ContentResult
        { ContentType = "application/xml", Content = new XElement(root, rows).ToString(SaveOptions.DisableFormatting) };

        public static ContentResult Events(IEnumerable<ClubEventDTO> rows) => Xml("community_event-list", rows.Select(GT6SocialXml.Event));
        public static ContentResult Memberships(IEnumerable<ClubEventDTO> rows, string username) => Xml("event_user_list",
            rows.Select(row => new XElement("community_event_user", new XAttribute("community_event_id", ClubIdentity.ToPublic(row.Id)),
                new XAttribute("user_id", username), new XAttribute("nickname", username), new XAttribute("level", 1))));
        public static ContentResult Friends(IEnumerable<string> users) => Xml("friend_list", users.Select(user => new XElement("user", new XAttribute("user_id", user))));
        public static ContentResult Templates(IEnumerable<ClubTemplateDTO> rows) => Xml("community_event_template-list", rows.Select(GT6SocialXml.Template));
        public static ContentResult Results(IEnumerable<ClubResultDTO> rows) => Xml("event_result_list", rows.Select(GT6SocialXml.Result));
    }
}
