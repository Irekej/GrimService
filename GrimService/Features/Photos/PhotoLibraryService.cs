using System.Collections.Generic;
using System.Threading.Tasks;
using GrimService.Database.Controllers;
using GrimService.Database.Tables;
using GrimService.Models.Xml;

namespace GrimService.Features.Photos;

public interface IPhotoLibrary
{
    Task<int?> Owner(string username);
    Task<IEnumerable<PhotoDTO>> Owned(int userId, string type);
    Task<IEnumerable<PhotoDTO>> Latest();
}

public sealed class DatabasePhotoLibrary(UserDBManager users, PhotoDBManager photos) : IPhotoLibrary
{
    public Task<int?> Owner(string username) => users.GetInternalIdOfUserAsync(username);
    public Task<IEnumerable<PhotoDTO>> Owned(int userId, string type) => photos.GetAllPhotosOfUser(userId, type);
    public Task<IEnumerable<PhotoDTO>> Latest() => photos.GetAllPhotosOfUsers("3");
}

public sealed class PhotoLibraryService(IPhotoLibrary library)
{
    public async Task<PhotoList> Owned(string username, string type)
    {
        int? owner = await library.Owner(username);
        if (!owner.HasValue) return null;
        var list = new PhotoList();
        foreach (var row in await library.Owned(owner.Value, type))
            list.Photos.Add(new Photo {
                PhotoId = row.Id, CarName = row.CarName, Comment = row.Comment, CreateTime = row.CreateTime,
                Place = row.Place, UserId = username, Type = row.Type
            });
        return list;
    }

    public async Task<PhotoList2> Stream()
    {
        var list = new PhotoList2();
        foreach (var row in await library.Latest())
            list.Photos.Add(new Photo2 {
                PhotoId = row.Id, UserId = row.Username, CarName = row.CarName, Comment = row.Comment,
                Place = row.Place, Type = row.Type, CreateTime = row.CreateTime
            });
        return list;
    }
}
