using System.Collections.Generic;
using System.Globalization;
using System.Xml.Linq;

namespace GrimService.Models.Xml
{
    public static class ClubMembershipXml
    {



        public static string Serialize(IEnumerable<ClubUser> clubs, string userId)
        {
            var root = new XElement("club_user_list");
            foreach (var club in clubs)
                root.Add(new XElement("community_user",
                    new XAttribute("community_id", club.ClubId.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute("user_id", userId),
                    new XAttribute("level", "1")));
            return root.ToString(SaveOptions.DisableFormatting);
        }
    }
}
