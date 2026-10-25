using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace GrimService.Features.EventCatalog
{




    public sealed class FileEventCatalog : IEventCatalog
    {
        private readonly string resourceRoot;

        public FileEventCatalog(string resourceRoot)
        {
            this.resourceRoot = Path.GetFullPath(resourceRoot ?? throw new ArgumentNullException(nameof(resourceRoot)));
        }

        public EventResource FindList(string server, string lastGame) =>
            Find(server, GameDirectory(lastGame), "event_list.xml");

        public EventResource FindEvent(string server, string lastGame, int folderId) =>
            folderId < 0 ? null : Find(server, GameDirectory(lastGame),
                "event_" + folderId.ToString(CultureInfo.InvariantCulture) + ".xml");

        public EventResource FindSettings(string server, string lastGame) =>
            Find(server, GameDirectory(lastGame), "setting.xml") ?? Find(server, null, "setting.xml");

        public EventResource FindImage(string server, string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || fileName.IndexOfAny(new[] { '/', '\\', ':', '\0' }) >= 0)
                return null;

            if (!fileName.EndsWith(".png", StringComparison.Ordinal) && !fileName.EndsWith(".img", StringComparison.Ordinal))
                return null;
            return Find(server, null, fileName);
        }

        private EventResource Find(string server, string game, string fileName)
        {
            if (string.IsNullOrEmpty(server) || !server.All(c => char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_'))
                return null;
            var directory = Path.Combine(resourceRoot, "event", server);
            var path = game == null ? Path.Combine(directory, fileName) : Path.Combine(directory, game, fileName);
            return File.Exists(path) ? new EventResource(path) : null;
        }


        private static string GameDirectory(string lastGame) => lastGame == "GT5" ? "GT5" : "GT6";
    }
}
