using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using GrimService.Database.Controllers;
using GrimService.Database.Tables;
using GrimService.Models.Xml;

namespace GrimService.Features.Forum
{
    public sealed record ForumRequest(string Command, long ClubId, long UserId, int? ThreadType = null,
        int LatestComment = 0, long? ThreadId = null, long? CommentId = null,
        string Title = null, string Comment = null, int Activity = 0);
    public enum ForumStatus { Completed, Xml, Invalid, Forbidden, MissingThread, Unsupported }
    public sealed record ForumResult(ForumStatus Status, long Value = 0, string Xml = null);

    public interface IClubForumRepository
    {
        Task<bool> CanReadAsync(long clubId, long userId);
        Task<bool> ThreadExistsAsync(long clubId, long threadId, long userId);
        Task<IEnumerable<ClubThreadDTO>> ThreadsAsync(long clubId, int type, long userId);
        Task<IEnumerable<ClubCommentDTO>> CommentsAsync(long clubId, long threadId, long cursor, int direction, int count = 100);
        Task<long> CreateThreadAsync(long clubId, long userId, string title, string comment, bool activity);
        Task<long> CreateCommentAsync(long clubId, long threadId, long userId, string comment);
        Task<int> DeleteThreadAsync(long clubId, long threadId, long userId);
        Task<int> DeleteCommentAsync(long clubId, long threadId, long commentId, long userId);
    }

    public sealed class ClubForumRepository : IClubForumRepository
    {
        private readonly ClubContentDBManager db;
        public ClubForumRepository(ClubContentDBManager db) => this.db = db;
        public Task<bool> CanReadAsync(long clubId, long userId) => db.CanReadAsync(clubId, userId);
        public Task<bool> ThreadExistsAsync(long clubId, long threadId, long userId) => db.ThreadExistsAsync(clubId, threadId, userId);
        public Task<IEnumerable<ClubThreadDTO>> ThreadsAsync(long clubId, int type, long userId) => db.GetThreadsAsync(clubId, type, userId);
        public Task<IEnumerable<ClubCommentDTO>> CommentsAsync(long clubId, long threadId, long cursor, int direction, int count = 100) => db.GetCommentsAsync(clubId, threadId, cursor, direction, count);
        public Task<long> CreateThreadAsync(long clubId, long userId, string title, string comment, bool activity) => db.CreateThreadAsync(clubId, userId, title, comment, activity);
        public Task<long> CreateCommentAsync(long clubId, long threadId, long userId, string comment) => db.CreateCommentAsync(clubId, threadId, userId, comment);
        public Task<int> DeleteThreadAsync(long clubId, long threadId, long userId) => db.DeleteThreadAsync(clubId, threadId, userId);
        public Task<int> DeleteCommentAsync(long clubId, long threadId, long commentId, long userId) => db.DeleteCommentAsync(clubId, threadId, commentId, userId);
    }

    public sealed class ClubForumService
    {
        private readonly IClubForumRepository repository;
        public ClubForumService(IClubForumRepository repository) => this.repository = repository;
        private static bool Valid(string text, int max, bool empty = false)
        {
            if (text == null || text.Length > max || (!empty && string.IsNullOrWhiteSpace(text))) return false;
            try { XmlConvert.VerifyXmlChars(text); return true; }
            catch (XmlException) { return false; }
        }

        public async Task<ForumResult> ExecuteAsync(ForumRequest request)
        {
            if (request.ClubId <= 0 || !await repository.CanReadAsync(request.ClubId, request.UserId)) return new(ForumStatus.Forbidden);
            if (request.Command == "bbs2.getThreadList")
            {
                if (request.ThreadType != 0 && request.ThreadType != 1) return new(ForumStatus.Invalid);
                var root = new XElement("thread_list");
                foreach (var thread in await repository.ThreadsAsync(request.ClubId, request.ThreadType.Value, request.UserId))
                {
                    var latest = request.LatestComment == 0 ? null :
                        (await repository.CommentsAsync(request.ClubId, thread.Id, 0, 0, 1)).LastOrDefault();
                    root.Add(ClubForumXml.Thread(thread, latest));
                }
                return new(ForumStatus.Xml, Xml: root.ToString(SaveOptions.DisableFormatting));
            }
            if (request.Command == "bbs2.createThread")
            {
                if (!Valid(request.Title, 128) || !Valid(request.Comment, 140, true)) return new(ForumStatus.Invalid);
                return new(ForumStatus.Completed, await repository.CreateThreadAsync(request.ClubId, request.UserId, request.Title, request.Comment, request.Activity != 0));
            }
            if (!request.ThreadId.HasValue || request.ThreadId <= 0) return new(ForumStatus.Invalid);
            long threadId = request.ThreadId.Value;
            if (!await repository.ThreadExistsAsync(request.ClubId, threadId, request.UserId)) return new(ForumStatus.MissingThread);
            switch (request.Command)
            {
                case "bbs2.getCommentList":
                case "bbs2.getCommentListOlder":
                case "bbs2.getCommentListNewer":
                    int direction = request.Command == "bbs2.getCommentListNewer" ? 1 : request.Command == "bbs2.getCommentListOlder" ? -1 : 0;
                    if (direction != 0 && (!request.CommentId.HasValue || request.CommentId < 0)) return new(ForumStatus.Invalid);
                    return new(ForumStatus.Xml, Xml: ClubForumXml.Comments(await repository.CommentsAsync(request.ClubId, threadId, direction == 0 ? 0 : request.CommentId.Value, direction)));
                case "bbs2.createComment":
                    if (!Valid(request.Comment, 400)) return new(ForumStatus.Invalid);
                    return new(ForumStatus.Completed, await repository.CreateCommentAsync(request.ClubId, threadId, request.UserId, request.Comment));
                case "bbs2.deleteThread":
                    return new(ForumStatus.Completed, await repository.DeleteThreadAsync(request.ClubId, threadId, request.UserId) > 0 ? 1 : -1);
                case "bbs2.deleteComment":
                    if (!request.CommentId.HasValue || request.CommentId <= 0) return new(ForumStatus.Invalid);
                    return new(ForumStatus.Completed, await repository.DeleteCommentAsync(request.ClubId, threadId, request.CommentId.Value, request.UserId) > 0 ? 1 : -1);
                default: return new(ForumStatus.Unsupported);
            }
        }
    }
}
