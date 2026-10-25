using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using GrimService.Database.Tables;

namespace GrimService.Models.Xml
{
    public static class ClubForumXml
    {
        private static XAttribute Attr(string name, object value) => new XAttribute(name, value ?? "");
        private static string Time(System.DateTime value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);


        public static XElement Comment(ClubCommentDTO comment) => new XElement("comment",
            Attr("comment_id", comment.Id), Attr("user_id", comment.Username), Attr("nickname", comment.Username),
            Attr("comment", comment.Comment), Attr("create_time", Time(comment.CreateTime)),
            Attr("photo_id", comment.PhotoId), Attr("photo_avatar", comment.PhotoAvatar), Attr("delete_user_id", comment.DeleteUserId));

        public static XElement Thread(ClubThreadDTO thread, ClubCommentDTO latest = null)
        {
            var element = new XElement("thread", Attr("thread_id", thread.Id), Attr("user_id", thread.Username),
                Attr("nickname", thread.Username), Attr("title", thread.Title), Attr("comment", thread.Comment),
                Attr("create_time", Time(thread.CreateTime)), Attr("update_time", Time(thread.UpdateTime)),
                Attr("comment_count", thread.CommentCount), Attr("hidden", 0));
            if (latest != null) element.Add(Comment(latest));
            return element;
        }

        public static string Comments(IEnumerable<ClubCommentDTO> comments) => new XElement("comment_list", comments.Select(Comment)).ToString(SaveOptions.DisableFormatting);
    }
}
