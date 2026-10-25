using System.Collections.Generic;
using System.Linq;
using GrimService.Models.Xml;

namespace GrimService.Features.Rankings
{

    public static class RankingResponses
    {
        public static RankingList List(IEnumerable<RankedResult> entries) =>
            new RankingList { Rankings = entries.Select(entry => Map(entry, true)).ToList() };

        public static RankingList Player(RankedResult entry) => new RankingList
        {
            Rankings = entry == null ? new List<Ranking>() : new List<Ranking> { Map(entry, false) }
        };

        private static Ranking Map(RankedResult entry, bool includeListFields)
        {
            var row = entry.Row;
            var result = new Ranking
            {
                UserId = row.Username, BoardId = row.BoardId, Score = row.Score,
                Stats = row.Stats, ReplayId = row.ReplayId, ReplayMarkId = row.ReplayMarkId,
                CreateTime = row.CreateTime, Rank = entry.Position
            };

            if (includeListFields) { result.RankingId = row.Id; result.Country = row.Country; }
            return result;
        }
    }
}
