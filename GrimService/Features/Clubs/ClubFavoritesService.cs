using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GrimService.Database.Controllers;
using GrimService.Database.Tables;

namespace GrimService.Features.Clubs
{
    public enum ClubFavoriteStatus { Changed, InvalidId, MissingClub }
    public sealed record ClubFavoriteList(bool Allowed, IReadOnlyList<ClubsDTO> Clubs);
    public interface IClubFavoritesRepository
    {
        Task<IEnumerable<ClubsDTO>> ListAsync(long userId);
        Task<bool> AddAsync(long clubId, long userId);
        Task RemoveAsync(long clubId, long userId);
    }

    public sealed class ClubFavoritesRepository : IClubFavoritesRepository
    {
        private readonly ClubsDBManager clubs;
        public ClubFavoritesRepository(ClubsDBManager clubs) => this.clubs = clubs;
        public Task<IEnumerable<ClubsDTO>> ListAsync(long userId) => clubs.GetFavoriteClubsAsync(userId);
        public Task<bool> AddAsync(long clubId, long userId) => clubs.AddFavoriteAsync(clubId, userId);
        public Task RemoveAsync(long clubId, long userId) => clubs.RemoveFavoriteAsync(clubId, userId);
    }

    public sealed class ClubFavoritesService
    {
        private readonly IClubFavoritesRepository repository;
        public ClubFavoritesService(IClubFavoritesRepository repository) => this.repository = repository;

        public async Task<ClubFavoriteList> ListAsync(long userId, string authenticatedUsername, string requestedUsername)
        {
            if (!string.Equals(authenticatedUsername, requestedUsername, StringComparison.Ordinal))
                return new(false, Array.Empty<ClubsDTO>());
            return new(true, (await repository.ListAsync(userId)).ToArray());
        }

        public async Task<ClubFavoriteStatus> ChangeAsync(long clubId, long authenticatedUserId, bool add)
        {
            if (clubId <= 0) return ClubFavoriteStatus.InvalidId;
            if (add)
            {
                if (!await repository.AddAsync(clubId, authenticatedUserId)) return ClubFavoriteStatus.MissingClub;
            }
            else await repository.RemoveAsync(clubId, authenticatedUserId);
            return ClubFavoriteStatus.Changed;
        }
    }
}
