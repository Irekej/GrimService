using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GrimService.Database.Tables;

namespace GrimService.Features.Replays
{
    public enum ReplayUploadStatus { Saved, MissingRanking, InvalidPayload, TooLarge, StaleRanking }
    public sealed record ReplayUploadResult(ReplayUploadStatus Status, long Id = 0, long Length = 0);


    public sealed class ReplayTransferService
    {
        public const int MaxUploadBytes = 16 * 1024 * 1024;
        private readonly IReplayRepository _repository;
        private readonly string _directory;

        public ReplayTransferService(IReplayRepository repository, string resourceRoot)
        {
            _repository = repository;
            _directory = Path.GetFullPath(Path.Combine(resourceRoot, "replay"));
        }


        public async Task<FileStream> OpenAsync(long id)
        {
            if (id <= 0 || await _repository.FindReplayAsync(id) == null) return null;
            FileStream stream;
            try { stream = new FileStream(PathFor(id), FileMode.Open, FileAccess.Read, FileShare.Read); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            if (stream.Length != 0) return stream;
            stream.Dispose();
            return null;
        }

        public async Task<ReplayUploadResult> UploadAsync(ReplayDTO replay, Stream input,
            long? declaredLength, CancellationToken cancellationToken)
        {
            var ranking = await _repository.FindRankingAsync(replay.UserId, replay.BoardId);
            if (ranking == null) return new(ReplayUploadStatus.MissingRanking);

            using var payload = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await input.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
            {
                if (payload.Length + read > MaxUploadBytes) return new(ReplayUploadStatus.TooLarge);
                await payload.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            if (payload.Length == 0 || (declaredLength.HasValue && declaredLength != payload.Length))
                return new(ReplayUploadStatus.InvalidPayload);

            Directory.CreateDirectory(_directory);
            long id = await _repository.AddAsync(replay);
            string path = PathFor(id);
            string temporary = Path.Combine(_directory, id + "." + Guid.NewGuid().ToString("N") + ".tmp");
            bool moved = false, published = false;
            try
            {
                await File.WriteAllBytesAsync(temporary, payload.ToArray(), cancellationToken);
                File.Move(temporary, path);
                moved = true;
                published = await _repository.AttachAsync(ranking, id, replay.IsShort == "1");
                return published ? new(ReplayUploadStatus.Saved, id, payload.Length) : new(ReplayUploadStatus.StaleRanking);
            }
            finally
            {

                try
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                    if (!published && moved) File.Delete(path);
                }
                finally { if (!published) await _repository.RemoveAsync(id); }
            }
        }

        private string PathFor(long id) => Path.Combine(_directory, id + ".dat");
    }
}
