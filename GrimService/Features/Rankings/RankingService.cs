using System;
using System.Globalization;
using System.Threading.Tasks;
using GrimService.Database.Tables;

namespace GrimService.Features.Rankings
{
    public sealed record RankingSubmission(int UserId, string Username, string Country,
        string BoardId, string Score, string Stats);
    public enum RankingSaveStatus { Invalid, NotImproved, Added, Updated }
    public sealed record RankingSaveResult(RankingSaveStatus Status, long Position = 0);


    public sealed class RankingService
    {
        private readonly IRankingRepository _repository;
        public RankingService(IRankingRepository repository) => _repository = repository;

        public async Task<RankingBoard> ReadBoardAsync(string boardId) =>
            new RankingBoard(await _repository.ReadBoardAsync(boardId));
        public Task<long> CountAsync(string boardId) => _repository.CountAsync(boardId);
        public Task<long> FindPositionAsync(string username, string boardId) => _repository.FindPositionAsync(username, boardId);
        public async Task<long> PredictPositionAsync(string boardId, string score)
        {
            long position = await _repository.PredictPositionAsync(boardId, score);
            return position == 0 ? 1 : position;
        }

        public static bool IsValidScore(string score) =>
            long.TryParse(score, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number >= 0;

        public async Task<RankingSaveResult> SubmitAsync(RankingSubmission submission)
        {
            if (submission == null || string.IsNullOrWhiteSpace(submission.BoardId) ||
                submission.Stats == null || !IsValidScore(submission.Score))
                return new(RankingSaveStatus.Invalid);

            var row = new RankingsDTO(submission.UserId, submission.Username, submission.Country,
                submission.BoardId, submission.Score, submission.Stats, DateTime.Now);
            var status = await _repository.SaveBestAsync(row);
            if (status is RankingSaveStatus.NotImproved or RankingSaveStatus.Invalid) return new(status);

            var board = await ReadBoardAsync(submission.BoardId);
            return new(status,
                board.FindUser(submission.Username)?.Position ?? -1);
        }
    }
}
