using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GrimService.Database.Controllers;
using GrimService.Database.Tables;

namespace GrimService.Features.Clubs
{
    public interface IClubAccessRepository
    {
        Task<ClubsDTO> FindAsync(long clubId);
        Task<IReadOnlyList<string>> MembersAsync(long clubId);
        Task<int> FriendCountAsync(long clubId, long userId);
        Task<bool> IsInvitedAsync(long clubId, long userId);
    }

    public sealed class ClubAccessRepository : IClubAccessRepository
    {
        private readonly ClubsDBManager clubs;
        private readonly GT6SocialDBManager social;
        public ClubAccessRepository(ClubsDBManager clubs, GT6SocialDBManager social)
        { this.clubs = clubs; this.social = social; }
        public Task<ClubsDTO> FindAsync(long clubId) => clubs.GetByIDAsync(clubId);
        public Task<IReadOnlyList<string>> MembersAsync(long clubId) => clubs.GetMemberNamesAsync(clubId);
        public Task<int> FriendCountAsync(long clubId, long userId) => clubs.GetFriendCountAsync(clubId, userId);
        public Task<bool> IsInvitedAsync(long clubId, long userId) => social.IsClubInvitedAsync(clubId, userId);
    }

    public sealed record ClubAccess(ClubsDTO Club, bool IsOwner, bool IsMember, bool CanRead);

    public sealed class ClubAccessService
    {
        private readonly IClubAccessRepository repository;
        public ClubAccessService(IClubAccessRepository repository) => this.repository = repository;

        public async Task<ClubAccess> ReadAsync(long clubId, long userId, string username)
        {
            var club = await repository.FindAsync(clubId);
            if (club == null) return null;
            bool owner = club.UserId == userId;
            bool member = (await repository.MembersAsync(clubId)).Contains(username);
            bool canRead = owner || member || club.Is_Open == "1" ||
                await repository.FriendCountAsync(clubId, userId) > 0 || await repository.IsInvitedAsync(clubId, userId);
            return new(club, owner, member, canRead);
        }
    }
}
