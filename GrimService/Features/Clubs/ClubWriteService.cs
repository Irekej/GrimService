using System;
using System.Linq;
using System.Threading.Tasks;
using GrimService.Database.Controllers;
using GrimService.Database.Tables;

namespace GrimService.Features.Clubs
{
    public sealed record ClubEditor(int UserId, string Username, string Country, string Game);
    public sealed record ClubCreateDraft(string Title, string Description, string Tags, string Open, string Recent, string ImageId);
    public sealed record ClubUpdateDraft(string Title = null, string Description = null, string Open = null, string Tags = null, string Recent = null);
    public enum ClubWriteStatus { Saved, Invalid, Forbidden, Failed }
    public sealed record ClubWriteResult(ClubWriteStatus Status, long PublicId = 0);

    public interface IClubWriteRepository
    {
        Task<long> CreateAsync(ClubsDTO club);
        Task<int> UpdateAsync(long clubId, long ownerId, ClubUpdateDraft draft, string[] tags);
        Task ActivityAsync(long userId, long clubId, string[] values);
    }

    public sealed class ClubWriteRepository : IClubWriteRepository
    {
        private readonly ClubsDBManager clubs;
        private readonly GT6SocialDBManager social;
        public ClubWriteRepository(ClubsDBManager clubs, GT6SocialDBManager social = null)
        { this.clubs = clubs; this.social = social; }
        public Task<long> CreateAsync(ClubsDTO club) => clubs.AddAsync(club);
        public Task<int> UpdateAsync(long clubId, long ownerId, ClubUpdateDraft draft, string[] tags) =>
            clubs.UpdateClubDetailsAsync(clubId, ownerId, draft.Title, draft.Description, draft.Open, tags, draft.Recent);
        public Task ActivityAsync(long userId, long clubId, string[] values) =>
            social == null ? Task.CompletedTask : social.AddActivityAsync(userId, clubId, values);
    }

    public sealed class ClubWriteService
    {
        private readonly IClubWriteRepository repository;
        public ClubWriteService(IClubWriteRepository repository) => this.repository = repository;

        public async Task<ClubWriteResult> CreateAsync(ClubEditor editor, ClubCreateDraft draft)
        {
            if (draft == null || draft.Title == null || draft.Description == null || draft.Tags == null ||
                draft.Open == null || draft.Recent == null || draft.ImageId == null) return new(ClubWriteStatus.Invalid);
            var tags = draft.Tags.Split(',').Distinct().ToArray();
            var row = new ClubsDTO(editor.UserId, editor.Username, editor.Country, draft.Title, draft.Description,
                draft.Recent, tags.ElementAtOrDefault(0) ?? "", tags.ElementAtOrDefault(1) ?? "", tags.ElementAtOrDefault(2) ?? "",
                tags.ElementAtOrDefault(3) ?? "", tags.ElementAtOrDefault(4) ?? "", tags.ElementAtOrDefault(5) ?? "",
                draft.Open, draft.ImageId, DateTime.Now, DateTime.Now);
            long id = await repository.CreateAsync(row);
            if (id <= 0) return new(ClubWriteStatus.Failed);
            long publicId = ClubIdentity.ToPublic(id);
            await repository.ActivityAsync(editor.UserId, id, new[] { "CC", publicId.ToString(), row.Title, row.Description });
            return new(ClubWriteStatus.Saved, publicId);
        }

        public async Task<ClubWriteResult> UpdateAsync(long clubId, ClubEditor editor, ClubUpdateDraft draft)
        {
            if (editor.Game != "GT6") return new(ClubWriteStatus.Forbidden);
            if (draft == null || (draft.Title != null && (string.IsNullOrWhiteSpace(draft.Title) || draft.Title.Length > 128)) ||
                draft.Description?.Length > 140 || (draft.Open != null && draft.Open != "0" && draft.Open != "1") ||
                draft.Tags?.Length > 256 || draft.Recent?.Length > 400 ||
                (draft.Title == null && draft.Description == null && draft.Open == null && draft.Tags == null && draft.Recent == null))
                return new(ClubWriteStatus.Invalid);
            var tags = draft.Tags?.Split(',', StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
            if (tags?.Length > 6) return new(ClubWriteStatus.Invalid);
            if (await repository.UpdateAsync(clubId, editor.UserId, draft, tags) <= 0) return new(ClubWriteStatus.Forbidden);
            if (draft.Recent != null)
                await repository.ActivityAsync(editor.UserId, clubId, new[] { "RU", editor.Username, draft.Recent });
            return new(ClubWriteStatus.Saved);
        }
    }
}
