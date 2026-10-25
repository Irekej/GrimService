using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using GrimService.Database.Controllers;
using GrimService.Database.Tables;
using GrimService.Models.Xml;

namespace GrimService.Features.Friends;

public interface IFriendProfiles
{
    Task<IEnumerable<UserDTO>> Read(int owner);
}

public sealed class DatabaseFriendProfiles(FriendDBManager friends, UserDBManager users) : IFriendProfiles
{
    public async Task<IEnumerable<UserDTO>> Read(int owner)
    {
        var profiles = new List<UserDTO>();
        foreach (var friend in await friends.GetAllFriendsOfUser(owner))
        {
            var profile = await users.GetByIDAsync(friend.FriendId);
            if (profile != null) profiles.Add(profile);
        }
        return profiles;
    }
}

public sealed class FriendProfileService(IFriendProfiles profiles)
{
    public async Task<SimpleFriendList> ForGt5(int owner)
    {
        var list = new SimpleFriendList();
        foreach (var profile in await profiles.Read(owner))
            list.Items.Add(new SimpleFriend(profile.PSNUserId, profile.ASpecLevel, profile.BSpecLevel));
        return list;
    }

    public async Task<string> ForGt6(int owner)
    {
        using var text = new StringWriter();
        using var writer = XmlWriter.Create(text, new XmlWriterSettings {
            OmitXmlDeclaration = true, Indent = false, NewLineHandling = NewLineHandling.None
        });
        writer.WriteStartElement("friend_list");
        foreach (var profile in await profiles.Read(owner))
        {
            writer.WriteStartElement("profile");
            writer.WriteElementString("id", profile.PSNUserId);
            writer.WriteElementString("country", profile.Country);
            writer.WriteElementString("nickname", profile.Nickname);
            writer.WriteElementString("photo_avatar", profile.PhotoIdAvatar);
            writer.WriteEndElement();
        }
        writer.WriteEndElement();
        writer.Flush();
        return text.ToString();
    }
}
