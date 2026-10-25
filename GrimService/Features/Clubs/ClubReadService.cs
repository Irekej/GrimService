using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GrimService.Database.Controllers;
using GrimService.Database.Tables;
using GrimService.Models.Xml;

namespace GrimService.Features.Clubs
{
    public interface IClubReadRepository
    {
        Task<IEnumerable<ClubsDTO>> MembershipsAsync(string username);
        Task<IEnumerable<ClubsDTO>> PublicAsync();
        Task<IEnumerable<ClubsDTO>> ByIdAsync(long clubId);
        Task<IReadOnlyList<string>> MembersAsync(long clubId);
        Task<string> PhotoAsync(long clubId);
        Task<int> FriendsAsync(long clubId, long viewerId);
        Task<bool> ActiveAsync(long clubId, long viewerId);
    }

    public sealed class ClubReadRepository : IClubReadRepository
    {
        private readonly ClubsDBManager clubs;
        public ClubReadRepository(ClubsDBManager clubs) => this.clubs = clubs;
        public Task<IEnumerable<ClubsDTO>> MembershipsAsync(string username) => clubs.GetAllClubsOfUser(username);
        public Task<IEnumerable<ClubsDTO>> PublicAsync() => clubs.GetPublicClubsAsync();
        public Task<IEnumerable<ClubsDTO>> ByIdAsync(long clubId) => clubs.GetClubById(clubId);
        public Task<IReadOnlyList<string>> MembersAsync(long clubId) => clubs.GetMemberNamesAsync(clubId);
        public Task<string> PhotoAsync(long clubId) => clubs.GetClubPhotoIdAsync(clubId);
        public Task<int> FriendsAsync(long clubId, long viewerId) => clubs.GetFriendCountAsync(clubId, viewerId);
        public Task<bool> ActiveAsync(long clubId, long viewerId) => clubs.IsActiveForUserAsync(clubId, viewerId);
    }

    public sealed class ClubReadService
    {
        private readonly IClubReadRepository repository;
        public ClubReadService(IClubReadRepository repository) => this.repository = repository;
        public async Task<List<ClubUser>> MembershipsAsync(string username, long viewerId) =>
            await MapAsync(await repository.MembershipsAsync(username), viewerId);
        public async Task<List<ClubUser>> PublicAsync(long viewerId) => await MapAsync(await repository.PublicAsync(), viewerId);
        public async Task<List<ClubUser>> ByIdAsync(long clubId, long viewerId) => await MapAsync(await repository.ByIdAsync(clubId), viewerId);

        public async Task<List<ClubUser>> MapAsync(IEnumerable<ClubsDTO> clubs, long viewerId)
        {
            var result = new List<ClubUser>();
            foreach (var club in clubs)
            {
                var members = await repository.MembersAsync(club.Id);
                var model = new ClubUser
                {
                    ClubId = ClubIdentity.ToPublic(club.Id), UserId = club.PSNUserId, Country = club.Country,
                    Title = club.Title, Description = club.Description, Recent = club.Recent, Open = club.Is_Open,
                    PhotoId = await repository.PhotoAsync(club.Id) ?? "", UserCount = members.Count.ToString(),
                    FriendCount = (await repository.FriendsAsync(club.Id, viewerId)).ToString(System.Globalization.CultureInfo.InvariantCulture),
                    RecentUpdateTime = club.RecentUpdateTime, CreateTime = club.CreateTime,
                    IsActive = await repository.ActiveAsync(club.Id, viewerId)
                };
                model.UserIdList.Add(club.PSNUserId);
                model.Tags.AddRange(new[] { club.Tag_1, club.Tag_2, club.Tag_3, club.Tag_4, club.Tag_5, club.Tag_6 }.Where(tag => !string.IsNullOrEmpty(tag)));
                result.Add(model);
            }
            return result;
        }
    }
}
