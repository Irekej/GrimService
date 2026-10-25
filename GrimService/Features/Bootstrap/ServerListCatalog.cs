using System;
using System.Collections.Generic;

namespace GrimService.Features.Bootstrap
{
    public readonly record struct ServerListDocument(string RelativePath, bool InvalidRegion = false);

    public static class ServerListCatalog
    {
        private static readonly IReadOnlyDictionary<string, string> Profiles = CreateProfiles();

        public static ServerListDocument Select(string host, string region)
        {
            if (host == null || !Profiles.TryGetValue(host, out var document))
                return default;

            if (!IsRegion(region))
                return new ServerListDocument(null, InvalidRegion: true);

            return new ServerListDocument(region == "_default" ? "serverlist.xml" : $"{region}/{document}");
        }

        private static bool IsRegion(string region)
        {
            if (string.IsNullOrEmpty(region)) return false;
            foreach (char character in region)
                if (!char.IsAsciiLetterOrDigit(character) && character != '_' && character != '-')
                    return false;
            return true;
        }

        private static IReadOnlyDictionary<string, string> CreateProfiles()
        {
            var profiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var officialRegions = new[] {
                "scee", "scel", "scea", "sceb", "scei", "sceh",
                "uk", "us", "eu", "el", "es", "fr", "gb", "it", "jp", "kr", "nl", "de", "pl", "pt", "ru", "tr", "tw"
            };
            foreach (string region in officialRegions)
                profiles.Add($"granturismo.ps3.online.{region}.gran-turismo.com", "serverlistofw.xml");

            profiles.Add("test.5.61.32.177test.5.61.32.177", "serverlistofw.xml");
            profiles.Add("54.37.232.59", "serverlist.xml");
            profiles.Add("vps-0213c442.vps.ovh.net", "serverlist.xml");
            profiles.Add("test.5.61.32.177", "serverlist.xml");
            profiles.Add("192.168.1.110", "serverlistlocal.xml");
            return profiles;
        }
    }
}
