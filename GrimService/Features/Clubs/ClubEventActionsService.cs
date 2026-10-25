using System.Threading.Tasks;
using GrimService.Database.Controllers;

namespace GrimService.Features.Clubs
{
    public sealed record ClubEventParticipant(long UserId, string Game, bool IsMember, bool IsOwner);

    public interface IClubEventActionsRepository
    {
        Task<bool> MembershipAsync(long clubId, long eventId, long userId, bool join);
        Task<int> InviteAsync(long eventId, long senderId, string target);
        Task<long> AddResultAsync(long clubId, long eventId, long userId, string result);
        Task<int> DeleteResultAsync(long clubId, long eventId, long resultId, long userId);
    }

    public sealed class ClubEventActionsRepository : IClubEventActionsRepository
    {
        private readonly GT6SocialDBManager social;
        public ClubEventActionsRepository(GT6SocialDBManager social) => this.social = social;
        public Task<bool> MembershipAsync(long clubId, long eventId, long userId, bool join) => social.EventMembershipAsync(clubId, eventId, userId, join);
        public Task<int> InviteAsync(long eventId, long senderId, string target) => social.InviteAsync(eventId, senderId, target);
        public Task<long> AddResultAsync(long clubId, long eventId, long userId, string result) => social.AddResultAsync(clubId, eventId, userId, result);
        public Task<int> DeleteResultAsync(long clubId, long eventId, long resultId, long userId) => social.DeleteResultAsync(clubId, eventId, resultId, userId);
    }

    public sealed class ClubEventActionsService
    {
        private readonly IClubEventActionsRepository repository;
        public ClubEventActionsService(IClubEventActionsRepository repository) => this.repository = repository;
        private static bool CanParticipate(long? eventId, ClubEventParticipant actor) =>
            eventId.HasValue && actor.Game == "GT6" && actor.IsMember;

        public async Task<ClubEventWriteResult> MembershipAsync(long clubId, long? eventId, ClubEventParticipant actor, bool join)
        {
            if (!CanParticipate(eventId, actor)) return new(ClubEventWriteStatus.Forbidden);
            bool accepted = await repository.MembershipAsync(clubId, eventId.Value, actor.UserId, join);
            return new(ClubEventWriteStatus.Saved, accepted ? 1 : -1);
        }

        public async Task<ClubEventWriteResult> InviteAsync(long? eventId, ClubEventParticipant actor, string target)
        {
            if (!CanParticipate(eventId, actor)) return new(ClubEventWriteStatus.Forbidden);
            if (!ClubText.IsValid(target, 32, true)) return new(ClubEventWriteStatus.Invalid);
            return new(ClubEventWriteStatus.Saved, await repository.InviteAsync(eventId.Value, actor.UserId, target) > 0 ? 1 : -1);
        }

        public async Task<ClubEventWriteResult> AddResultAsync(long clubId, long? eventId, ClubEventParticipant actor, string result)
        {
            if (!CanParticipate(eventId, actor)) return new(ClubEventWriteStatus.Forbidden);
            if (!ClubText.IsValid(result, 1000000, true)) return new(ClubEventWriteStatus.Invalid);
            long id = await repository.AddResultAsync(clubId, eventId.Value, actor.UserId, result);
            return new(ClubEventWriteStatus.Saved, id > 0 ? id : -1);
        }

        public async Task<ClubEventWriteResult> DeleteResultAsync(long clubId, long? eventId, ClubEventParticipant actor, long? resultId)
        {
            if (actor.Game != "GT6" || !actor.IsOwner || !eventId.HasValue) return new(ClubEventWriteStatus.Forbidden);
            if (!resultId.HasValue || resultId <= 0) return new(ClubEventWriteStatus.Invalid);
            return new(ClubEventWriteStatus.Saved,
                await repository.DeleteResultAsync(clubId, eventId.Value, resultId.Value, actor.UserId) > 0 ? 1 : -1);
        }
    }
}
