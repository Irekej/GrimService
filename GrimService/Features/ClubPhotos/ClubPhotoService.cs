using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using GrimService.Database.Controllers;
using GrimService.Features.Clubs;
using GrimService.Features.Images;

namespace GrimService.Features.ClubPhotos
{
    public enum ClubPhotoKind { Club, Comment, Event }
    public enum ClubPhotoStatus { Uploaded, Invalid, Forbidden, TooLarge }
    public sealed record ClubPhotoRequest(long ClubId, long UserId, ClubPhotoKind Kind,
        long? ThreadId = null, long? EventId = null, string Comment = null);
    public sealed record ClubPhotoResult(ClubPhotoStatus Status, long Id = 0);

    public interface IClubPhotoRepository
    {
        Task<bool> IsMemberAsync(long clubId, long userId);
        Task<bool> ThreadExistsAsync(long clubId, long threadId, long userId);
        Task<long> SetClubAsync(long clubId, long userId, byte[] bytes);
        Task<long> SetEventAsync(long clubId, long eventId, long userId, byte[] bytes);
        Task<long> AddCommentAsync(long clubId, long threadId, long userId, string comment, byte[] bytes);
    }

    public sealed class ClubPhotoRepository : IClubPhotoRepository
    {
        private readonly ClubContentDBManager db;
        public ClubPhotoRepository(ClubContentDBManager db) => this.db = db;
        public Task<bool> IsMemberAsync(long clubId, long userId) => db.IsMemberAsync(clubId, userId);
        public Task<bool> ThreadExistsAsync(long clubId, long threadId, long userId) => db.ThreadExistsAsync(clubId, threadId, userId);
        public Task<long> SetClubAsync(long clubId, long userId, byte[] bytes) => db.SetClubImageAsync(clubId, userId, bytes);
        public Task<long> SetEventAsync(long clubId, long eventId, long userId, byte[] bytes) => db.SetEventImageAsync(clubId, eventId, userId, bytes);
        public Task<long> AddCommentAsync(long clubId, long threadId, long userId, string comment, byte[] bytes) => db.CreateCommentAsync(clubId, threadId, userId, comment, bytes);
    }

    public sealed class ClubPhotoService
    {
        private readonly IClubPhotoRepository repository;
        public ClubPhotoService(IClubPhotoRepository repository) => this.repository = repository;

        public async Task<ClubPhotoResult> UploadAsync(ClubPhotoRequest request, Stream input, CancellationToken cancellationToken)
        {
            if (request.ClubId <= 0 || !await repository.IsMemberAsync(request.ClubId, request.UserId))
                return new(ClubPhotoStatus.Forbidden);
            if (!Enum.IsDefined(request.Kind)) return new(ClubPhotoStatus.Invalid);
            if (request.Kind == ClubPhotoKind.Event && (!request.EventId.HasValue || request.EventId <= 0))
                return new(ClubPhotoStatus.Invalid);
            if (request.Kind == ClubPhotoKind.Comment)
            {
                if (!request.ThreadId.HasValue || request.Comment == null || request.Comment.Length > 400)
                    return new(ClubPhotoStatus.Invalid);
                try { XmlConvert.VerifyXmlChars(request.Comment); }
                catch (XmlException) { return new(ClubPhotoStatus.Invalid); }
                if (!await repository.ThreadExistsAsync(request.ClubId, request.ThreadId.Value, request.UserId))
                    return new(ClubPhotoStatus.Invalid);
            }
            using var payload = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await input.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
            {
                if (payload.Length + read > GTConstants.MaxPhotoSize) return new(ClubPhotoStatus.TooLarge);
                await payload.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            var bytes = payload.ToArray();
            if (bytes.Length < 4 || bytes[0] != 0xff || bytes[1] != 0xd8) return new(ClubPhotoStatus.Invalid);
            try
            {
                var info = GameImages.Inspect(bytes);
                if (info.Kind != ImageFileKind.Jpeg || info.Width > GTConstants.MaxPhotoWidth || info.Height > GTConstants.MaxPhotoHeight)
                    return new(ClubPhotoStatus.Invalid);
            }
            catch (ArgumentException)
            { return new(ClubPhotoStatus.Invalid); }
            long id = request.Kind switch
            {
                ClubPhotoKind.Comment => await repository.AddCommentAsync(request.ClubId, request.ThreadId.Value, request.UserId, request.Comment, bytes),
                ClubPhotoKind.Event => await repository.SetEventAsync(request.ClubId, ClubIdentity.ToDatabase(request.EventId.Value), request.UserId, bytes),
                _ => await repository.SetClubAsync(request.ClubId, request.UserId, bytes)
            };
            return new(ClubPhotoStatus.Uploaded, id);
        }
    }
}
