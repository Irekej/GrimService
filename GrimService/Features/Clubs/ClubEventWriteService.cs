using System;
using System.Globalization;
using System.Threading.Tasks;
using GrimService.Database.Controllers;
using GrimService.Database.Tables;

namespace GrimService.Features.Clubs
{
    public sealed record ClubEventEditor(long UserId, string Game, bool IsOwner);
    public sealed record ClubEventDraft(string Title = null, string Description = null, string Regulation = null,
        string Open = null, string StartTime = null, string EndTime = null, int? Mode = null,
        long? RankingBoard = null, bool Activity = false, string BbsComment = null);
    public enum ClubEventWriteStatus { Saved, Invalid, Forbidden }
    public sealed record ClubEventWriteResult(ClubEventWriteStatus Status, long Value = 1);

    public interface IClubEventWriteRepository
    {
        Task<long> SaveAsync(ClubEventDTO clubEvent, long userId, bool create, bool activity, string comment);
        Task<int> DeleteAsync(long clubId, long eventId, long userId);
    }

    public sealed class ClubEventWriteRepository : IClubEventWriteRepository
    {
        private readonly GT6SocialDBManager social;
        public ClubEventWriteRepository(GT6SocialDBManager social) => this.social = social;
        public Task<long> SaveAsync(ClubEventDTO clubEvent, long userId, bool create, bool activity, string comment) =>
            social.SaveEventAsync(clubEvent, userId, create, activity, comment);
        public Task<int> DeleteAsync(long clubId, long eventId, long userId) => social.DeleteEventAsync(clubId, eventId, userId);
    }

    public sealed class ClubEventWriteService
    {
        private readonly IClubEventWriteRepository repository;
        public ClubEventWriteService(IClubEventWriteRepository repository) => this.repository = repository;

        public async Task<ClubEventWriteResult> SaveAsync(long clubId, ClubEventDTO current,
            ClubEventEditor editor, ClubEventDraft draft, bool create)
        {
            if (editor.Game != "GT6" || !editor.IsOwner) return new(ClubEventWriteStatus.Forbidden);
            if (draft == null || (!create && current == null)) return new(ClubEventWriteStatus.Invalid);
            var row = current == null ? new ClubEventDTO { ClubId = clubId, Open = 1, Mode = 1 } : Copy(current);
            row.Title = draft.Title ?? row.Title;
            row.Description = draft.Description ?? row.Description;
            row.Regulation = draft.Regulation ?? row.Regulation;
            if (!ClubText.IsValid(row.Title, 128, true) || !ClubText.IsValid(row.Description, 140, true) ||
                !ClubText.IsValid(row.Regulation, 1000000, true)) return new(ClubEventWriteStatus.Invalid);
            if (draft.Open != null)
            {
                if (draft.Open != "0" && draft.Open != "1") return new(ClubEventWriteStatus.Invalid);
                row.Open = int.Parse(draft.Open);
            }
            if (draft.StartTime != null)
            {
                if (!DateTimeOffset.TryParse(draft.StartTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time))
                    return new(ClubEventWriteStatus.Invalid);
                row.StartTime = time.UtcDateTime;
            }
            if (draft.EndTime != null)
            {
                if (!DateTimeOffset.TryParse(draft.EndTime, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time))
                    return new(ClubEventWriteStatus.Invalid);
                row.EndTime = time.UtcDateTime;
            }
            if (row.StartTime == default || row.EndTime <= row.StartTime) return new(ClubEventWriteStatus.Invalid);
            if (create)
            {
                if (draft.Mode.HasValue) row.Mode = draft.Mode.Value;
                if (draft.RankingBoard.HasValue) row.RankingBoard = draft.RankingBoard.Value;
            }
            string comment = draft.BbsComment ?? "";
            if (!ClubText.IsValid(comment, 400)) return new(ClubEventWriteStatus.Invalid);
            long saved = await repository.SaveAsync(row, editor.UserId, create, draft.Activity, comment);
            return new(ClubEventWriteStatus.Saved, saved < 0 ? -1 : create ? ClubIdentity.ToPublic(saved) : 1);
        }

        public async Task<ClubEventWriteResult> DeleteAsync(long clubId, ClubEventDTO current, ClubEventEditor editor)
        {
            if (editor.Game != "GT6" || !editor.IsOwner || current == null) return new(ClubEventWriteStatus.Forbidden);
            return new(ClubEventWriteStatus.Saved, await repository.DeleteAsync(clubId, current.Id, editor.UserId) > 0 ? 1 : -1);
        }

        private static ClubEventDTO Copy(ClubEventDTO row) => new ClubEventDTO
        {
            Id = row.Id, ClubId = row.ClubId, ThreadId = row.ThreadId, Title = row.Title, Description = row.Description,
            Regulation = row.Regulation, PhotoId = row.PhotoId, Open = row.Open, Mode = row.Mode, RankingBoard = row.RankingBoard,
            StartTime = row.StartTime, EndTime = row.EndTime, CreateTime = row.CreateTime, UpdateTime = row.UpdateTime,
            UserCount = row.UserCount, FriendCount = row.FriendCount
        };
    }
}
