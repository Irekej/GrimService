using GrimService.Features.Friends;
using GrimService.Features.Photos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GrimService.Features.PlayerRecords;

public static class PlayerLibraryRegistration
{
    public static IServiceCollection AddPlayerLibraries(this IServiceCollection services)
    {
        services.TryAddTransient<IFriendProfiles, DatabaseFriendProfiles>();
        services.TryAddTransient<FriendProfileService>();
        services.TryAddTransient<IPhotoLibrary, DatabasePhotoLibrary>();
        services.TryAddTransient<PhotoLibraryService>();
        return services;
    }
}
