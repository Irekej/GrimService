using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Text.Json;
using System.Globalization;

namespace GrimService.Services
{

    public static class MuseumResources
    {
        public static void Validate(string server, string region, string language, string suffix)
        {
            if (!Regex.IsMatch(server ?? "", @"\A[a-zA-Z0-9_-]{1,32}\z") ||
                !Regex.IsMatch(region ?? "", @"\A[a-z]{2}\z") ||
                !Regex.IsMatch(language ?? "", @"\A[a-z]{2}\z") ||
                !Regex.IsMatch(suffix ?? "", @"\A(?:[1-9][0-9]{0,8}|l_[1-9][0-9]{0,8}|remove|root)\z"))
                throw new ArgumentException("Invalid museum resource request.");
        }
        public static string Resolve(string root, string server, string region, string language, string suffix)
        {
            Validate(server, region, language, suffix);


            foreach (var scope in new[] {
                (server,region,language), (server,"us",language), (server,"gb",language),
                (server,"us","us"), (server,"us","gb"), (server,"gb","gb"),
                ("dev",region,language), ("dev","us",language),
                ("dev","us","us"), ("dev","us","gb"), ("dev","gb","gb") }.Distinct())
            {
                var path = Path.Combine(root,"data2","museum",scope.Item1,scope.Item2,$"museum_{scope.Item3}_{suffix}.xml");
                if (File.Exists(path)) return Path.GetFullPath(path);
            }
            return null;
        }
        public static string Xml(string root, string server, string region, string language, string suffix)
        {
            var path = Resolve(root,server,region,language,suffix);

            if (path == null && int.TryParse(suffix, out var requestedId))
            {
                var custom = CustomCards(root, language).FirstOrDefault(x => (int)x.Element("fileid") == requestedId);
                if (custom != null) return custom.ToString(SaveOptions.DisableFormatting);
            }

            if (path == null) return suffix == "remove" ? "<museum_list />" : null;
            using var reader = XmlReader.Create(path,new XmlReaderSettings {
                DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4000000 });
            var xml = XElement.Load(reader);
            if (suffix == "l_1" && xml.Name == "museum_list")
            {
                foreach (var card in CustomCards(root, language))
                {
                    var id = (string)card.Element("fileid");
                    if (xml.Elements("id").Any(x => (string)x.Attribute("id") == id)) continue;
                    xml.Add(new XElement("id", new XAttribute("id", id),
                        new XAttribute("lastupdate", (string)card.Element("lastupdate")),
                        new XAttribute("dealer", (string)card.Element("dealer")),
                        new XAttribute("dealer_id", (string)card.Element("dealer_id"))));
                }
            }

            return xml.ToString(SaveOptions.DisableFormatting);
        }
        private static XElement[] CustomCards(string root, string language)
        {
            var path = Path.Combine(root, "data", "museum", "image", "classic-cars.json");
            if (!File.Exists(path)) return Array.Empty<XElement>();
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var updated = DateTimeOffset.Parse(document.RootElement.GetProperty("updated").GetString(), CultureInfo.InvariantCulture)
                .ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
            var cards = document.RootElement.GetProperty("cards").EnumerateArray().Select(card =>
            {
                var id = card.GetProperty("id").GetInt32();
                if (id < 900001 || id > 999999) throw new JsonException("Custom museum ID must be in 900001..999999.");
                var caption = card.GetProperty(language == "pl" ? "caption_pl" : "caption_en").GetString();
                return new XElement("museum_item", new XElement("fileid", id),
                    new XElement("dealer", card.GetProperty("dealer").GetString()),
                    new XElement("year", card.GetProperty("year").GetInt32()),
                    new XElement("caption", card.GetProperty("title").GetString() + " — " + caption),
                    new XElement("lastupdate", updated), new XElement("online", 1),
                    new XElement("dealer_id", card.GetProperty("dealer_id").GetInt32()));
            }).ToArray();
            if (cards.Select(x => (int)x.Element("fileid")).Distinct().Count() != cards.Length)
                throw new JsonException("Duplicate custom museum ID.");
            return cards;
        }
        public static string Image(string root, int id)
        {
            if (id <= 0) return null;
            var path=Path.GetFullPath(Path.Combine(root,"data","museum","image",$"museum_{id}.img"));
            return File.Exists(path) ? path : null;
        }
    }
}
