using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GrimService.Features.EventCatalog
{

    public sealed class EventResource
    {
        private readonly string path;

        internal EventResource(string path) => this.path = path;

        public Task<string> ReadXmlAsync(CancellationToken cancellation = default) =>
            File.ReadAllTextAsync(path, cancellation);

        public async Task CopyToAsync(Stream destination, CancellationToken cancellation = default)
        {
            using var source = File.OpenRead(path);
            await source.CopyToAsync(destination, cancellation);
        }
    }
}
