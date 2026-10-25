using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GrimService.Database.Controllers;
using GrimService.Database.Tables;

namespace GrimService.Features.Clubs
{
    public enum ClubJoinStatus { Joined, MissingClub, MembershipNotVisible }
    public interface IClubMembershipRepository
    {
        Task<IReadOnlyList<string>> MembersAsync(long clubId);
        Task<bool> JoinAndActivateAsync(long clubId, long userId);
        Task<IEnumerable<ClubsDTO>> MembershipsAsync(string username);
        Task PublishJoinedAsync(long clubId, long userId);
    }

    public sealed class ClubMembershipRepository : IClubMembershipRepository
    {
        private readonly ClubsDBManager clubs;
        private readonly GT6SocialDBManager social;
        public ClubMembershipRepository(ClubsDBManager clubs, GT6SocialDBManager social = null)
        { this.clubs = clubs; this.social = social; }
        public Task<IReadOnlyList<string>> MembersAsync(long clubId) => clubs.GetMemberNamesAsync(clubId);
        public Task<bool> JoinAndActivateAsync(long clubId, long userId) => clubs.JoinAndActivateAsync(clubId, userId);
        public Task<IEnumerable<ClubsDTO>> MembershipsAsync(string username) => clubs.GetAllClubsOfUser(username);
        public async Task PublishJoinedAsync(long clubId, long userId)
        {
            if (social == null) return;
            var club = await clubs.GetByIDAsync(clubId);
            await social.AddActivityAsync(userId, clubId, new[] { "CJ", ClubIdentity.ToPublic(clubId).ToString(),
                club.Title, club.Description, await clubs.GetClubPhotoIdAsync(clubId) ?? "" });
        }
    }

    public sealed class ClubMembershipService
    {
        private readonly IClubMembershipRepository repository;
        public ClubMembershipService(IClubMembershipRepository repository) => this.repository = repository;

        public async Task<ClubJoinStatus> JoinAsync(long clubId, long userId, string username)
        {
            if (clubId <= 0) return ClubJoinStatus.MissingClub;
            bool wasMember = (await repository.MembersAsync(clubId)).Contains(username);
            if (!await repository.JoinAndActivateAsync(clubId, userId)) return ClubJoinStatus.MissingClub;
            var memberships = await repository.MembershipsAsync(username);
            if (!memberships.Any(club => club.Id == clubId)) return ClubJoinStatus.MembershipNotVisible;
            if (!wasMember) await repository.PublishJoinedAsync(clubId, userId);
            return ClubJoinStatus.Joined;
        }
    }
}
