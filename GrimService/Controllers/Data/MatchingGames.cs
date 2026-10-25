namespace GrimService.Controllers.Data
{
    public class MatchingGames
    {
        public int Id { get; set; }

        public long GameId { get; set; }

        public long EventId { get; set; }

        public long MatchingId { get; set; }

        public long PlayersCount { get; set; }

        public long Level { get; set; }
        public long MaxEntry { get; set; }

        public string CreateTime { get; set; }

        public MatchingGames() { }

        public MatchingGames(string createTime)
        {
            CreateTime = createTime;
        }
    }
}
