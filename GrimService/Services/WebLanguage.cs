using System;
using System.Globalization;

namespace GrimService.Services
{

    public static class WebLanguage
    {
        public static int Index(string language) => language == "pl" ? 0 : language == "en" ? 1
            : language == "es" ? 2 : language == "fr" ? 3 : language == "pt" ? 4 : language == "ja" ? 5 : -1;
        public static bool Supported(string language) => Index(language) >= 0;

        public static string Resolve(string cookie, string acceptLanguage)
        {

            if (Supported(cookie)) return cookie;
            var selected = "en";
            double best = 0;
            foreach (var entry in (acceptLanguage ?? "").Split(','))
            {
                var parts = entry.Trim().Split(';');
                var tag = parts[0].Trim().ToLowerInvariant();
                var language = tag.Split('-')[0];
                if (!Supported(language)) continue;
                double quality = 1;
                for (int i = 1; i < parts.Length; i++)
                {
                    var parameter = parts[i].Trim();
                    if (!parameter.StartsWith("q=", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!double.TryParse(parameter.Substring(2), NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture, out quality) || quality < 0 || quality > 1) quality = 0;
                }

                if (quality > best) { selected = language; best = quality; }
            }
            return selected;
        }
    }
}
