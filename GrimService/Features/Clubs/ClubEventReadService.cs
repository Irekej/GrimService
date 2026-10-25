using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GrimService.Database.Controllers;
using GrimService.Database.Tables;

namespace GrimService.Features.Clubs
{
    public interface IClubEventReadRepository
    {
        Task<IEnumerable<ClubEventDTO>> EventsAsync(long clubId, long viewerId, long eventId, string username);
        Task<IEnumerable<string>> FriendsAsync(long clubId, long viewerId, long eventId);
        Task<IEnumerable<ClubTemplateDTO>> TemplatesAsync(long clubId, long templateId);
        Task<IEnumerable<ClubResultDTO>> ResultsAsync(long clubId, long eventId);
    }

    public sealed class ClubEventReadRepository : IClubEventReadRepository
    {
        private readonly GT6SocialDBManager social;
        public ClubEventReadRepository(GT6SocialDBManager social) => this.social = social;
        public Task<IEnumerable<ClubEventDTO>> EventsAsync(long clubId, long viewerId, long eventId, string username) => social.EventsAsync(clubId, viewerId, eventId, username);
        public Task<IEnumerable<string>> FriendsAsync(long clubId, long viewerId, long eventId) => social.FriendsAsync(clubId, viewerId, eventId);
        public Task<IEnumerable<ClubTemplateDTO>> TemplatesAsync(long clubId, long templateId) => social.TemplatesAsync(clubId, templateId);
        public Task<IEnumerable<ClubResultDTO>> ResultsAsync(long clubId, long eventId) => social.ResultsAsync(clubId, eventId);
    }

    public sealed class ClubEventReadService
    {
        private readonly IClubEventReadRepository repository;
        public ClubEventReadService(IClubEventReadRepository repository) => this.repository = repository;
        public Task<IEnumerable<ClubEventDTO>> EventsAsync(long clubId, long viewerId, long eventId = 0, string username = null) => repository.EventsAsync(clubId, viewerId, eventId, username);
        public async Task<ClubEventDTO> FindAsync(long clubId, long viewerId, long eventId) => (await EventsAsync(clubId, viewerId, eventId)).SingleOrDefault();
        public Task<IEnumerable<string>> FriendsAsync(long clubId, long viewerId, long eventId) => repository.FriendsAsync(clubId, viewerId, eventId);
        public Task<IEnumerable<ClubTemplateDTO>> TemplatesAsync(long clubId, long templateId = 0) => repository.TemplatesAsync(clubId, templateId);
        public Task<IEnumerable<ClubResultDTO>> ResultsAsync(long clubId, long eventId) => repository.ResultsAsync(clubId, eventId);
    }
}
