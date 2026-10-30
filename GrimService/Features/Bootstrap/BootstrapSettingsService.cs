using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Dapper;

namespace GrimService.Features.Bootstrap
{
    public sealed class BootstrapOptionInput
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public string Group { get; set; }
        public string Name { get; set; }
        public string DefaultValue { get; set; }
        public string Kind { get; set; }
    }
    public sealed class BootstrapRegionInput
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }
    }
    public sealed class BootstrapSettingsModel
    {
        public string Profile { get; set; } = "dev";
        public long Revision { get; set; }
        public List<BootstrapOptionInput> Options { get; set; } = new();
        public List<BootstrapRegionInput> Regions { get; set; } = new();
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public IReadOnlyList<BootstrapProfile> Profiles => BootstrapDefaults.Profiles;
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public IReadOnlyList<BootstrapServer> Servers => BootstrapDefaults.Profiles.FirstOrDefault(p => p.Id == Profile)?.Servers ?? Array.Empty<BootstrapServer>();
    }
    public sealed class BootstrapSettingsService
    {
        private readonly IDbConnection db;
        public BootstrapSettingsService(IDbConnection db) => this.db = db;
        public void CreateTable() => db.Execute(@"CREATE TABLE IF NOT EXISTS bootstrap_configuration (
            profile TEXT PRIMARY KEY, payload JSONB NOT NULL, revision BIGINT NOT NULL,
            updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(), updated_by TEXT NOT NULL)");

        public async Task<BootstrapSettingsModel> Load(string profile)
        {
            ValidateProfile(profile);
            var row = await db.QuerySingleOrDefaultAsync<StoredSettings>(
                "SELECT payload::text AS Payload, revision AS Revision FROM bootstrap_configuration WHERE profile=@profile", new { profile });
            var data = row == null ? new SettingsPayload() : JsonSerializer.Deserialize<SettingsPayload>(row.Payload) ?? new();
            return MakeModel(profile, row?.Revision ?? 0, data);
        }
        public async Task<bool> Save(BootstrapSettingsModel input, string actor, bool reset)
        {
            ValidateProfile(input.Profile);
            if (input.Revision < 0) throw new ArgumentException("Nieprawidłowe dane formularza.");
            var payload = new SettingsPayload();
            if (!reset)
            {
                var schema = MakeModel(input.Profile, 0, new());
                if (input.Profile == "regions")
                {
                    if (input.Regions == null || input.Regions.Count != schema.Regions.Count ||
                        input.Regions.Select(r => r.Key).Distinct(StringComparer.Ordinal).Count() != schema.Regions.Count ||
                        input.Regions.Any(r => !schema.Regions.Any(s => s.Key == r.Key)) || !input.Regions.Any(r => r.Enabled))
                        throw new ArgumentException("Włącz co najmniej jeden poprawny region.");
                    foreach (var region in input.Regions)
                    {
                        if (string.IsNullOrWhiteSpace(region.Name) || region.Name.Length > 100) throw new ArgumentException("Nazwa regionu musi mieć od 1 do 100 znaków.");
                        XmlConvert.VerifyXmlChars(region.Name);
                    }
                    payload.Regions = input.Regions.Select(r => new BootstrapRegionInput { Key = r.Key, Name = r.Name, Enabled = r.Enabled }).ToList();
                }
                else
                {
                    if (input.Options == null || input.Options.Count != schema.Options.Count ||
                        input.Options.Select(o => o.Key).Distinct(StringComparer.Ordinal).Count() != schema.Options.Count)
                        throw new ArgumentException("Nieprawidłowe dane formularza.");
                    var expected = schema.Options.ToDictionary(o => o.Key, StringComparer.Ordinal);
                    foreach (var option in input.Options)
                    {
                        if (!expected.TryGetValue(option.Key ?? "", out var definition)) throw new ArgumentException("Nieprawidłowe dane formularza.");
                        ValidateValue(definition, option.Value);
                        payload.Options.Add(option.Key, option.Value);
                    }
                }
            }
            var saved = await db.QuerySingleOrDefaultAsync<long?>(@"INSERT INTO bootstrap_configuration(profile,payload,revision,updated_by)
                SELECT @Profile,CAST(@Payload AS jsonb),1,@Actor WHERE @Revision=0
                ON CONFLICT(profile) DO UPDATE SET payload=EXCLUDED.payload,revision=bootstrap_configuration.revision+1,
                    updated_at=NOW(),updated_by=EXCLUDED.updated_by WHERE bootstrap_configuration.revision=@Revision
                RETURNING revision", new { input.Profile, Payload = JsonSerializer.Serialize(payload), Actor = actor ?? "admin", input.Revision });
            if (!saved.HasValue && input.Revision > 0)
                saved = await db.QuerySingleOrDefaultAsync<long?>(@"UPDATE bootstrap_configuration SET payload=CAST(@Payload AS jsonb),
                    revision=revision+1,updated_at=NOW(),updated_by=@Actor WHERE profile=@Profile AND revision=@Revision RETURNING revision",
                    new { input.Profile, Payload = JsonSerializer.Serialize(payload), Actor = actor ?? "admin", input.Revision });
            return saved.HasValue;
        }
        public async Task<byte[]> ServerList(string path, bool https)
        {
            var profile = BootstrapDefaults.Profiles.FirstOrDefault(p => p.Path == path);
            if (profile == null) return null;
            return RenderServerList(await Load(profile.Id), https);
        }
        public async Task<byte[]> RegionList() => RenderRegionList(await Load("regions"));
        public static BootstrapSettingsModel MakeModel(string profile, long revision, SettingsPayload data)
        {
            ValidateProfile(profile);
            var model = new BootstrapSettingsModel { Profile = profile, Revision = revision };
            if (profile == "regions")
            {
                var defaults = new[] { new BootstrapRegionInput { Key = "dev", Name = "GrimService", Enabled = true },
                    new BootstrapRegionInput { Key = "rel", Name = "RPCS3 Emulator", Enabled = false } };
                model.Regions = defaults.Select(r => data.Regions?.FirstOrDefault(saved => saved.Key == r.Key) ?? r).ToList();
                return model;
            }
            var definition = BootstrapDefaults.Profiles.First(p => p.Id == profile);
            foreach (var server in definition.Servers)
                foreach (var option in server.Options)
                {
                    string key = server.Name + "/" + option.Key;
                    model.Options.Add(new BootstrapOptionInput { Key = key, Group = server.Name, Name = option.Key,
                        DefaultValue = option.Value, Value = data.Options != null && data.Options.TryGetValue(key, out var saved) ? saved : option.Value,
                        Kind = Kind(option.Key, option.Value) });
                }
            return model;
        }
        public static byte[] RenderServerList(BootstrapSettingsModel model, bool https)
        {
            var values = model.Options.ToDictionary(o => o.Key, o => o.Value, StringComparer.Ordinal);
            var profile = BootstrapDefaults.Profiles.First(p => p.Id == model.Profile);
            var root = new XElement("servers");
            foreach (var server in profile.Servers)
            {
                var element = new XElement("server", new XElement("name", server.Name));
                if (server.Host != null) element.Add(new XElement("host", server.Host));
                if (server.Port != null) element.Add(new XElement("port", server.Port));
                if (server.Url != null) element.Add(new XElement("url", https ? SecureUrl(server.Url) : server.Url));
                foreach (var option in server.Options)
                    element.Add(new XElement("option", new XAttribute("key", option.Key), new XAttribute("value", values[server.Name + "/" + option.Key])));
                root.Add(element);
            }
            return Xml(root);
        }
        public static byte[] RenderRegionList(BootstrapSettingsModel model) => Xml(new XElement("regions",
            model.Regions.Where(r => r.Enabled).Select(r => new XElement("region", new XAttribute("key", r.Key), new XAttribute("value", r.Name)))));
        private static string SecureUrl(string value) => value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? value
            : value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? "https://" + value.Substring(7) : "https://" + value;
        private static byte[] Xml(XElement root)
        {
            using var stream = new MemoryStream();
            using (var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true }))
                new XDocument(new XDeclaration("1.0", "utf-8", null), root).Save(writer);
            return stream.ToArray();
        }
        private static string Kind(string key, string value) =>
            value is "0" or "1" && (key.EndsWith(".enable", StringComparison.Ordinal) || key.StartsWith("patch.seq", StringComparison.Ordinal) ||
                key is "maintenance" or "disqualify_report" or "room.user_bgm" or "room.pause_rendering") ? "flag"
            : Regex.IsMatch(value, "^-?[0-9]+$") ? "number" : Regex.IsMatch(value, "^-?[0-9]+(,-?[0-9]+)+$") ? "numbers"
            : key is "maintenance.next" or "maintenance.next.display" ? "date" : "text";
        private static void ValidateValue(BootstrapOptionInput definition, string value)
        {
            if (value == null || value.Length > 1024) throw new ArgumentException("Nieprawidłowa wartość opcji serwera.");
            XmlConvert.VerifyXmlChars(value);
            bool valid = definition.Kind switch
            {
                "flag" => value is "0" or "1",
                "number" => long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _),
                "numbers" => Regex.IsMatch(value, "^-?[0-9]+(,-?[0-9]+)+$"),
                "date" => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _),
                _ => true
            };
            if (!valid) throw new ArgumentException("Nieprawidłowa wartość opcji serwera.");
        }
        private static void ValidateProfile(string profile)
        {
            if (profile != "regions" && !BootstrapDefaults.Profiles.Any(p => p.Id == profile)) throw new ArgumentException("Nieprawidłowy profil konfiguracji.");
        }
        public sealed class StoredSettings { public string Payload { get; set; } public long Revision { get; set; } }
        public sealed class SettingsPayload
        {
            public Dictionary<string, string> Options { get; set; } = new();
            public List<BootstrapRegionInput> Regions { get; set; }
        }
    }
}
