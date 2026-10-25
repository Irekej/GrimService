using System;
using System.Text.RegularExpressions;

namespace GrimService.Utils
{
    public static class InitPathNormalizer
    {


        private static readonly Regex InitPath = new Regex(
            @"\A/+(?:(?:https?:/+)?[a-z0-9][a-z0-9.-]*(?::[0-9]{1,5})?/+)?init/+(?:(?<region>[a-z0-9_-]+)/+serverlist\.xml|regionlist\.xml)/*\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(100));

        private static readonly Regex LocalePath = new Regex(
            @"\A/+ap/+locale/*\z",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled,
            TimeSpan.FromMilliseconds(100));

        public static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;



            int leadingSlashes = 0;
            while (leadingSlashes < path.Length && path[leadingSlashes] == '/')
                leadingSlashes++;
            if (leadingSlashes > 1)
                path = path.Substring(leadingSlashes - 1);

            if (path.Length > 4096)
                return path;


            if (LocalePath.IsMatch(path))
                return "/ap/locale/";

            var match = InitPath.Match(path);
            if (!match.Success)
                return path;

            return match.Groups["region"].Success
                ? $"/init/{match.Groups["region"].Value}/serverlist.xml"
                : "/init/regionlist.xml";
        }
    }
}
