using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using GrimService.Database.Controllers;

namespace GrimService.Features.Clubs
{
    public static class ClubText
    {
        public static bool IsValid(string text, int max, bool required = false)
        {
            if (text == null) return !required;
            if (text.Length > max || (required && string.IsNullOrWhiteSpace(text))) return false;
            try { XmlConvert.VerifyXmlChars(text); return true; }
            catch (XmlException) { return false; }
        }
    }

    public enum ClubOperation { Invite, Delete, Leave, ListFriends }
    public enum ClubOperationStatus { Completed, Invalid, Forbidden, MissingClub, Rejected }
    public sealed record ClubActor(string Game, long UserId, string Username);
    public sealed record ClubOperationResult(ClubOperationStatus Status, int Value = 1,
        long ClubId = 0, IReadOnlyList<string> Friends = null);

    public interface IClubOperationsRepository
    {
        Task<int> InviteAsync(long clubId, long sender, string target);
        Task<int> DeleteAsync(long clubId, long owner);
        Task<int> LeaveAsync(long clubId, long userId);
        Task<IEnumerable<string>> FriendsAsync(long clubId, long userId);
    }

    public sealed class ClubOperationsRepository : IClubOperationsRepository
    {
        private readonly GT6SocialDBManager social;
        public ClubOperationsRepository(GT6SocialDBManager social) => this.social = social;
        public Task<int> InviteAsync(long clubId, long sender, string target) => social.InviteClubAsync(clubId, sender, target);
        public Task<int> DeleteAsync(long clubId, long owner) => social.DeleteClubAsync(clubId, owner);
        public Task<int> LeaveAsync(long clubId, long userId) => social.LeaveClubAsync(clubId, userId);
        public Task<IEnumerable<string>> FriendsAsync(long clubId, long userId) => social.FriendsAsync(clubId, userId);
    }

    public sealed class ClubOperationsService
    {
        private readonly ClubAccessService access;
        private readonly IClubOperationsRepository repository;
        public ClubOperationsService(ClubAccessService access, IClubOperationsRepository repository)
        { this.access = access; this.repository = repository; }

        public async Task<ClubOperationResult> ExecuteAsync(ClubOperation operation, long clubId, ClubActor actor, string target = null)
        {
            if (actor == null || actor.Game != "GT6") return new(ClubOperationStatus.Forbidden);
            if (clubId < 0 || !Enum.IsDefined(operation)) return new(ClubOperationStatus.Invalid);
            var permission = await access.ReadAsync(clubId, actor.UserId, actor.Username);
            if (permission == null) return new(ClubOperationStatus.MissingClub);
            if (!permission.CanRead) return new(ClubOperationStatus.Forbidden);

            switch (operation)
            {
                case ClubOperation.Invite:
                    if (!permission.IsOwner && (permission.Club.Is_Open != "1" || !permission.IsMember))
                        return new(ClubOperationStatus.Forbidden);
                    if (!ClubText.IsValid(target, 32, true)) return new(ClubOperationStatus.Invalid);
                    return new(await repository.InviteAsync(clubId, actor.UserId, target) > 0
                        ? ClubOperationStatus.Completed : ClubOperationStatus.Rejected);
                case ClubOperation.Delete:
                    if (!permission.IsOwner) return new(ClubOperationStatus.Forbidden);
                    return new(ClubOperationStatus.Completed, await repository.DeleteAsync(clubId, actor.UserId) > 0 ? 1 : -1);
                case ClubOperation.Leave:
                    if (permission.IsOwner) return new(ClubOperationStatus.Forbidden);
                    await repository.LeaveAsync(clubId, actor.UserId);
                    return new(ClubOperationStatus.Completed);
                case ClubOperation.ListFriends:
                    return new(ClubOperationStatus.Completed, ClubId: clubId,
                        Friends: (await repository.FriendsAsync(clubId, actor.UserId)).ToArray());
                default: throw new ArgumentOutOfRangeException(nameof(operation));
            }
        }
    }
}
