using System.Collections.Generic;
using System.IO;
using System;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace GrimService.Services
{

    public sealed class WebTranslations
    {
        public Dictionary<string, string[]> Messages { get; }
        public WebTranslations(IWebHostEnvironment environment)
        {


            using var bundled = typeof(WebTranslations).Assembly.GetManifestResourceStream("GrimService.WebMessages.json")
                ?? throw new InvalidOperationException("Bundled website translations are missing.");
            Messages = JsonSerializer.Deserialize<Dictionary<string, string[]>>(bundled)
                ?? throw new InvalidOperationException("Bundled website translations are invalid.");
            var path = Path.Combine(environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot"), "i18n", "messages.json");
            if (File.Exists(path))
            {
                var overrides = JsonSerializer.Deserialize<Dictionary<string, string[]>>(File.ReadAllText(path));
                if (overrides != null)
                    foreach (var entry in overrides)
                    {

                        if (entry.Value == null || entry.Value.Length < 4 || entry.Value.Length > 6) continue;
                        if (!Messages.TryGetValue(entry.Key, out var values)) values = new string[6];
                        if (values.Length < 6) Array.Resize(ref values, 6);
                        for (int i = 0; i < entry.Value.Length; i++)
                            if (!string.IsNullOrWhiteSpace(entry.Value[i])) values[i] = entry.Value[i];
                        Messages[entry.Key] = values;
                    }
            }
        }
    }

    public sealed class WebText
    {
        public const string CookieName = "grim.web.language.v2";
        public const string LegacyCookieName = "grim.web.language";
        private readonly IHttpContextAccessor context;
        private readonly WebTranslations translations;
        public WebText(IHttpContextAccessor context, WebTranslations translations)
        { this.context = context; this.translations = translations; }
        public static bool Supported(string language) => WebLanguage.Supported(language);
        public string Language {
            get {
                var cookies = context.HttpContext?.Request.Cookies;
                var value = cookies?[CookieName];
                if (!Supported(value)) value = cookies?[LegacyCookieName];
                return WebLanguage.Resolve(value, context.HttpContext?.Request.Headers["Accept-Language"].ToString());
            }
        }
        public Dictionary<string, string[]> Messages => translations.Messages;
        public string ReportStatus(string status) => this[status switch {
            "open" => "Nowe zgłoszenie", "reviewing" => "W trakcie weryfikacji",
            "resolved" => "Rozpatrzone", "dismissed" => "Odrzucone", _ => status
        }];
        public string this[string source] {
            get {
                if (string.IsNullOrEmpty(source)) return source ?? "";
                var key = source.Trim();
                if (!Messages.TryGetValue(key, out var values)) return source;
                var index = WebLanguage.Index(Language);
                var translated = values != null && values.Length > index && !string.IsNullOrWhiteSpace(values[index]) ? values[index]
                    : values != null && values.Length > 1 && !string.IsNullOrWhiteSpace(values[1]) ? values[1] : key;
                return source.Substring(0, source.Length - source.TrimStart().Length) + translated
                    + source.Substring(source.TrimEnd().Length);
            }
        }
    }
}
