using System;
using System.Collections.Generic;
using System.IO;

namespace GrimService.Services
{
    public static class TutorialAssets
    {
        private static readonly IReadOnlyList<string> Gt5Regions = Array.AsReadOnly(new[]
            { "BCAS20108", "BCES00569", "BCJS30001", "BCUS98114" });
        private static readonly IReadOnlyList<string> Gt6Regions = Array.AsReadOnly(new[]
            { "BCAS25018", "BCES01893", "BCES01977", "BCJS37016", "BCUS98296", "BCUS99247", "NPEA00502", "NPHA80269", "NPUA81049" });
        private static readonly IReadOnlyList<string> Gt6CrossplayRegions = Array.AsReadOnly(new[]
            { "BCAS25018", "BCES01893", "BCES01977", "BCJS37016", "BCUS98296", "BCUS99247" });

        public static IReadOnlyList<string> CrossplayRegions(string game) => game == "GT5" ? Gt5Regions
            : game == "GT6" ? Gt6CrossplayRegions : Array.Empty<string>();

        public static string RpcnTemplatePath(string contentRoot) => Path.Combine(contentRoot,
            "Resources", "tutorial-assets", "RPCN", "rpcn.yml");

        public static IReadOnlyList<string> Regions(string game) => game == "GT5" ? Gt5Regions
            : game == "GT6" ? Gt6Regions : Array.Empty<string>();

        public static string NormalizeRegion(string game, string region)
        {
            foreach (var allowed in Regions(game))
                if (string.Equals(allowed, region, StringComparison.OrdinalIgnoreCase)) return allowed;
            return null;
        }



        public static string PackagePath(string contentRoot, string game, string region)
        {
            var approved = NormalizeRegion(game, region);
            return approved == null ? null : Path.Combine(contentRoot, "Resources", "tutorial-assets",
                game + "-AutoInstall", approved + "_GRIMSERVICE_UNIVERSAL.pkg");
        }

        public static string ManualPath(string contentRoot, string game, string region)
        {
            var approved = NormalizeRegion(game, region);
            if (approved == null) return null;


            foreach (var folder in new[] { game.ToLowerInvariant() + "-manualinstallation", game + "-ManualInstall" })
            {
                var directory = Path.Combine(contentRoot, "Resources", "tutorial-assets", folder);
                if (!Directory.Exists(directory)) continue;
                string match = null;
                foreach (var file in Directory.EnumerateFiles(directory, "*.zip"))
                {
                    var name = Path.GetFileName(file);
                    if (!string.Equals(name, approved + ".zip", StringComparison.OrdinalIgnoreCase)
                        && !name.StartsWith(approved + " ", StringComparison.OrdinalIgnoreCase)) continue;

                    if (match != null) return null;
                    match = file;
                }
                if (match != null) return match;
            }
            return null;
        }

        public static string CrossplayPath(string contentRoot, string game, string region)
        {
            var approved = NormalizeRegion(game, region);
            if (approved == null || !CrossplayRegions(game).ContainsRegion(approved)) return null;
            foreach (var folder in new[] { game.ToLowerInvariant() + "-ps3-crossplay", game + "-PS3-Crossplay" })
            {
                var path = Path.Combine(contentRoot, "Resources", "tutorial-assets", folder,
                    approved + "_GrimRpcn_SPRX_MOD.zip");
                if (File.Exists(path)) return path;
            }
            return null;
        }

        private static bool ContainsRegion(this IReadOnlyList<string> regions, string region)
        {
            foreach (var allowed in regions) if (allowed == region) return true;
            return false;
        }
    }
}
