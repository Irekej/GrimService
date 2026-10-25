using System;

namespace GrimService.Database.Tables
{
    public class ClubThreadDTO
    {
        public long Id { get; set; }
        public long ClubId { get; set; }
        public long UserId { get; set; }
        public string Username { get; set; }
        public string Title { get; set; }
        public string Comment { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime UpdateTime { get; set; }
        public int CommentCount { get; set; }
    }

    public class ClubCommentDTO
    {
        public long Id { get; set; }
        public long ThreadId { get; set; }
        public long UserId { get; set; }
        public string Username { get; set; }
        public string Comment { get; set; }
        public string PhotoAvatar { get; set; }
        public string PhotoId { get; set; }
        public string DeleteUserId { get; set; }
        public DateTime CreateTime { get; set; }
    }

    public class ClubImageDTO
    {
        public long Id { get; set; }
        public long ClubId { get; set; }
        public long? ThreadId { get; set; }
        public byte[] Data { get; set; }
    }
}
