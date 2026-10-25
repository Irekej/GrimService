namespace GrimService.Controllers.Data
{
    public class MatchingStore
    {
        public int Id { get; set; }

        public string Username { get; set; }

        public long EventId { get; set; }

        public long MatchingId { get; set; }

        public long MaxEntry { get; set; }

        public long SendBand { get; set; }

        public long ReceiveBand { get; set; }

        public long NatType { get; set; }

        public long PDIUserId { get; set; }

        public string CreateTime { get; set; }

        public MatchingStore() { }

        public MatchingStore(string createTime)
        {
            CreateTime = createTime;
        }
    }
}
