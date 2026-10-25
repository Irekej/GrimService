using System;
namespace GrimService.Database.Tables
{
    public class ClubEventDTO
    {
        public long Id { get; set; }
        public long ClubId { get; set; }
        public long ThreadId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Regulation { get; set; }
        public string PhotoId { get; set; }
        public int Open { get; set; }
        public int Mode { get; set; }
        public long RankingBoard { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime CreateTime { get; set; }
        public DateTime UpdateTime { get; set; }
        public int UserCount { get; set; }
        public int FriendCount { get; set; }
    }
    public class ClubTemplateDTO
    {
        public long Id { get; set; }
        public long ClubId { get; set; }
        public string Description { get; set; }
        public string Regulation { get; set; }
    }
    public class ClubResultDTO
    {
        public long Id { get; set; }
        public long EventId { get; set; }
        public long ClubId { get; set; }
        public string Username { get; set; }
        public string Result { get; set; }
        public DateTime CreateTime { get; set; }
    }
    public class GT6ActivityDTO
    {
        public long Id { get; set; }
        public long? ClubId { get; set; }
        public string Username { get; set; }
        public string[] Values { get; set; }
        public DateTime CreateTime { get; set; }
    }
}
