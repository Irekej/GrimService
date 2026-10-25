using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GrimService.Database.Tables;

namespace GrimService.Features.Rankings
{

    public sealed class RankingBoard
    {
        private readonly RankedResult[] _entries;

        public RankingBoard(IEnumerable<RankingsDTO> rows)
        {
            _entries = rows.OrderBy(row => decimal.Parse(row.Score, CultureInfo.InvariantCulture))
                .ThenBy(row => row.Id)
                .Select((row, index) => new RankedResult(row, index + 1L)).ToArray();
        }

        public RankedResult FindUser(string username) =>
            _entries.FirstOrDefault(entry => entry.Row.Username == username);


        public IEnumerable<RankedResult> Page(int begin, int end)
        {
            if (begin < 1 || end < begin) throw new ArgumentOutOfRangeException(nameof(begin));
            return _entries.Skip(begin - 1).Take(end - begin + 1);
        }

        public IEnumerable<RankedResult> ForUsers(IEnumerable<string> usernames)
        {
            var users = usernames.ToHashSet(StringComparer.Ordinal);
            return _entries.Where(entry => users.Contains(entry.Row.Username));
        }

        public IEnumerable<RankedResult> ForOwners(IEnumerable<long> ownerIds)
        {
            var owners = ownerIds.ToHashSet();
            return _entries.Where(entry => owners.Contains(entry.Row.UserId));
        }
    }

    public sealed record RankedResult(RankingsDTO Row, long Position);
}
