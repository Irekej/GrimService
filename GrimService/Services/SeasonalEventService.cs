using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using GrimService.Config;
using Microsoft.Extensions.Options;

namespace GrimService.Services
{
    public sealed class SeasonalDocument
    {
        public string Server { get; set; }
        public string Game { get; set; }
        public int Folder { get; set; }
        public string Revision { get; set; }
        public string Xml { get; set; }
        public string ListingXml { get; set; }
        public bool Confirm { get; set; }
        public bool Withdraw { get; set; }
    }


    public sealed class SeasonalEventService
    {
        private readonly string root;
        private static readonly object Gate = new();
        public SeasonalEventService(IOptions<GameServerOptions> options)
            => root = Path.GetFullPath(Path.Combine(options.Value.XmlResourcePath, "event"));

        public string[] Servers() => Directory.Exists(root) ? Directory.GetDirectories(root)
            .Select(Path.GetFileName).Where(s => Regex.IsMatch(s, "^[a-zA-Z0-9_-]+$")).OrderBy(s => s).ToArray() : Array.Empty<string>();

        private string FolderPath(string server, string game)
        {
            if (game != "GT5" && game != "GT6") throw new ArgumentException("Wybierz GT5 lub GT6.");
            if (server == null || !Regex.IsMatch(server, "^[a-zA-Z0-9_-]+$")) throw new ArgumentException("Nieprawidłowy serwer.");
            var path = Path.Combine(root, server, game);
            if (!Directory.Exists(path)) throw new ArgumentException("Brak katalogu zasobów dla tej gry i serwera.");

            foreach (var item in new[] { root, Path.Combine(root, server), path })
                if ((File.GetAttributes(item) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("Katalog nie może być dowiązaniem.");
            return path;
        }

        private static string EventPath(string dir, int folder)
        {
            if (folder <= 0) throw new ArgumentException("ID folderu musi być dodatnie.");
            return Path.Combine(dir, $"event_{folder}.xml");
        }
        private static string Read(string path)
        {
            if (!File.Exists(path)) return "";
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("Plik nie może być dowiązaniem.");
            if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new ArgumentException("XML przekracza 4 MB.");
            return File.ReadAllText(path);
        }
        public static XDocument Parse(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml) || xml.Length > 4 * 1024 * 1024) throw new ArgumentException("Brak XML lub przekroczony limit 4 MB.");
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings {
                DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4 * 1024 * 1024 });
            var doc = XDocument.Load(reader);
            if (doc.Descendants().Any(e => e.Ancestors().Count() > 32)) throw new ArgumentException("XML jest zbyt głęboko zagnieżdżony.");
            return doc;
        }
        private static string Hash(string content) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
        private static string Revision(string dir, int folder) => Hash(Read(EventPath(dir, folder)) + "\0" + Read(Path.Combine(dir, "event_list.xml")));
        private static string Value(XElement node) => (string)node?.Attribute("value") ?? node?.Value;
        private static string Title(XElement node) => (string)node?.Element("title")?.Element("PL") ?? (string)node?.Element("title")?.Element("GB") ?? "Bez tytułu";
        private static string Localized(XElement node) => node?.Elements().FirstOrDefault(e => e.Name == "PL" && !string.IsNullOrWhiteSpace(e.Value))?.Value
            ?? node?.Elements().FirstOrDefault(e => e.Name == "GB" && !string.IsNullOrWhiteSpace(e.Value))?.Value
            ?? node?.Elements().FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.Value))?.Value ?? "";
        private static string English(XElement node) => new[] { "GB", "US", "EN" }
            .Select(language => (string)node?.Element(language)).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
        private static string Category(XElement e) => Value(e.Element("game_mode")) == "ONLINE_BATTLE" ? "quickmatch"
            : Value(e.Element("play_style")?.Element("bspec_type")) == "ONLY_B" || Value(e.Element("game_mode")) == "ARENA" ? "bspec" : "seasonal";
        private static string Kind(XElement e) => (Value(e.Element("game_mode")) ?? "").Contains("DRIFT") ? "drift"
            : (Value(e.Element("game_mode")) ?? "").Contains("TIME_ATTACK") ? "time" : "race";

        private long NextId(string server)
        {
            long next = 1;
            foreach (var game in new[] { "GT5", "GT6" })
            {
                var dir = Path.Combine(root, server, game);
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Files(dir))
                    foreach (Match m in Regex.Matches(Read(file), "<(?:event_id|board_id)\\s+value=\"([0-9]+)\""))
                        if (long.TryParse(m.Groups[1].Value, out var used)) next = Math.Max(next, checked(used + 1));
            }
            return next;
        }


        public SeasonalDocument Create(string server, string game, string category, string kind)
        {
            lock (Gate)
            {
                if (category != "seasonal" && !(category == "quickmatch" && game == "GT6") && !(category == "bspec" && game == "GT5"))
                    throw new ArgumentException("Kategoria nie jest dostępna dla tej gry.");
                var remote = category == "bspec" && kind == "remote";
                if (!remote && (!new[] { "race", "time", "drift" }.Contains(kind) || category != "seasonal" && kind != "race"))
                    throw new ArgumentException("Nieprawidłowy rodzaj eventu.");
                var dir = FolderPath(server, game);
                var candidates = new List<(int Folder, XElement Event)>();
                foreach (var file in Files(dir))
                {
                    try { candidates.AddRange(Parse(Read(file)).Descendants("events").Elements("event")
                        .Select(e => (int.Parse(Path.GetFileNameWithoutExtension(file).Substring(6)), e))); }
                    catch (Exception e) when (e is XmlException || e is ArgumentException) { /* Legacy invalid files are not templates. */ }
                }
                var template = candidates.FirstOrDefault(c => Category(c.Event) == category && (remote ? Value(c.Event.Element("game_mode")) == "ARENA" : Kind(c.Event) == kind && Value(c.Event.Element("game_mode")) != "ARENA"));
                if (template.Event == null && category == "seasonal") template = candidates.FirstOrDefault(c => Category(c.Event) == "seasonal");
                if (template.Event == null) throw new ArgumentException("Brak zgodnego szablonu w zasobach tej gry. Zaimportuj XML wzorcowy.");
                SeasonalDocument result;
                XDocument xml;
                XElement ev;
                if (category == "quickmatch")
                {
                    result = Load(server, game, template.Folder);
                    xml = Parse(result.Xml);
                    if (xml.Descendants("events").Elements("event").Count() >= 5)
                        throw new ArgumentException("Lista Quick Match ma już 5 eventów. Usuń jeden w edytorze, zatwierdź i spróbuj ponownie.");
                    var gp = new XElement(template.Event.Parent.Parent);
                    gp.Element("events").ReplaceNodes(new XElement(template.Event));
                    xml.Root.Add(gp);
                    ev = gp.Element("events").Element("event");
                    var id = NextId(server);
                    if (id > 0xFFFFFF) throw new ArgumentException("ID Quick Match przekracza 24-bitowy zakres klienta.");
                    ev.Element("event_id").SetAttributeValue("value", id);
                    foreach (var board in ev.Descendants("board_id")) board.SetAttributeValue("value", id);
                }
                else
                {
                    result = Clone(server, game, template.Folder);
                    xml = Parse(result.Xml);
                    var chosen = xml.Descendants("events").Elements("event").FirstOrDefault(e => Category(e) == category && (remote ? Value(e.Element("game_mode")) == "ARENA" : Kind(e) == kind))
                        ?? xml.Descendants("events").Elements("event").First();
                    var gp = new XElement(chosen.Parent.Parent);
                    gp.Element("events").ReplaceNodes(new XElement(chosen));
                    xml.Root.ReplaceNodes(gp);
                    ev = gp.Element("events").Element("event");
                    var id = NextId(server);
                    ev.Element("event_id").SetAttributeValue("value", id);
                    foreach (var board in ev.Descendants("board_id")) board.SetAttributeValue("value", id);
                }
                var listing = Parse(result.ListingXml);
                foreach (var title in ev.Element("information")?.Element("title")?.Elements() ?? Enumerable.Empty<XElement>()) title.Value = "Nowy event";
                foreach (var title in ev.Element("information")?.Element("one_line_title")?.Elements() ?? Enumerable.Empty<XElement>()) title.Value = "Nowy event";
                foreach (var description in ev.Element("information")?.Element("description")?.Elements() ?? Enumerable.Empty<XElement>()) description.Value = "";
                foreach (var title in listing.Root.Element("title")?.Elements() ?? Enumerable.Empty<XElement>()) title.Value = "Nowy event";
                if (category == "quickmatch") listing = Parse(result.ListingXml);
                result.Xml = xml.ToString(); result.ListingXml = listing.ToString();
                return Normalize(result);
            }
        }
        private static IEnumerable<string> Files(string dir) => Directory.GetFiles(dir, "event_*.xml")
            .Where(p => Regex.IsMatch(Path.GetFileName(p), "^event_[0-9]+\\.xml$")).OrderBy(p => p);

        public object List(string server, string game)
        {
            lock (Gate)
            {
                var dir = FolderPath(server, game);
                var listing = Parse(Read(Path.Combine(dir, "event_list.xml")));
                var ids = listing.Root.Elements("event").Select(e => (string)e.Element("id"))
                    .Concat(Files(dir).Select(p => Path.GetFileNameWithoutExtension(p).Substring(6))).Distinct();
                return ids.Where(s => int.TryParse(s, out var n) && n > 0).Select(s => {
                    var id = int.Parse(s);
                    var item = listing.Root.Elements("event").FirstOrDefault(e => (string)e.Element("id") == s);
                    string error = null;
                    string[] modes = Array.Empty<string>();
                    object[] entries = Array.Empty<object>();
                    try {
                        var xml = Parse(Read(EventPath(dir, id)));
                        modes = xml.Descendants("game_mode").Select(Value).Distinct().ToArray();
                        entries = xml.Descendants("events").Elements("event").Select(e => (object)new {
                            id = Value(e.Element("event_id")), title = Title(e.Element("information")),
                            description = Localized(e.Element("information")?.Element("description")),
                            track = (string)e.Element("track")?.Element("course_code")?.Attribute("label") ?? "—",
                            category = Category(e), kind = Kind(e)
                        }).ToArray();
                    }
                    catch (Exception e) when (e is XmlException || e is ArgumentException) { error = e.Message; }
                    return new { id, title = Title(item), published = item != null, modes, error, entries };
                }).ToArray();
            }
        }


        public SeasonalPublicEvent[] PublicEvents(string game)
        {
            lock (Gate)
            {
                var dir = FolderPath("dev", game);
                var list = Parse(Read(Path.Combine(dir, "event_list.xml")));
                var result = new List<SeasonalPublicEvent>();
                var folders = list.Root.Elements("event").Select(e => (string)e.Element("id"));
                foreach (var folderText in folders.Distinct())
                {
                    if (!int.TryParse(folderText, out var folder) || folder <= 0) continue;
                    var xml = Parse(Read(EventPath(dir, folder)));
                    foreach (var e in xml.Descendants("event"))
                    {
                        var id = Value(e.Element("event_id"));
                        if (string.IsNullOrEmpty(id)) continue;
                        var info = e.Element("information");
                        result.Add(new SeasonalPublicEvent {
                            Folder=folder, Id=id, Title=English(info?.Element("title")),
                            Description=English(info?.Element("description")),
                            Begin=Value(e.Element("begin_date")), End=Value(e.Element("end_date")),
                            Mode=Value(e.Element("game_mode")), Category=Category(e),
                            Course=(string)e.Descendants("course_code").FirstOrDefault()?.Attribute("label"),
                            BoardId=Value(e.Element("ranking")?.Element("board_id")),
                            Parameters=e.Elements().Where(n=>new[]{"race","race_parameter","regulation","entry_set","play_style"}.Contains(n.Name.LocalName))
                                .SelectMany(n=>n.Descendants().Where(v=>!v.HasElements).Select(v=>
                                    n.Name.LocalName+"/"+v.Name.LocalName+"="+((string)v.Attribute("label")??Value(v))))
                                .Take(100).ToArray()
                        });
                    }
                }
                return result.ToArray();
            }
        }

        public SeasonalDocument Load(string server, string game, int folder)
        {
            lock (Gate)
            {
                var dir = FolderPath(server, game);
                var list = Parse(Read(Path.Combine(dir, "event_list.xml")));
                var item = list.Root.Elements("event").SingleOrDefault(e => (string)e.Element("id") == folder.ToString());
                var savedListing = Read(Path.Combine(dir, $"event_{folder}.listing.xml"));
                if (item == null && savedListing.Length > 0) item = Parse(savedListing).Root;
                return new SeasonalDocument { Server = server, Game = game, Folder = folder, Revision = Revision(dir, folder),
                    Xml = Read(EventPath(dir, folder)), ListingXml = (item ?? NewListing(folder)).ToString() };
            }
        }
        private static XElement NewListing(int folder) => new("event", new XElement("id", folder),
            new XElement("title", new XElement("PL", "Nowy sezon"), new XElement("GB", "New season")),
            new XElement("description", new XElement("PL", ""), new XElement("GB", "")),
            new XElement("event_type", 0), new XElement("event_id_list", ""));

        public SeasonalDocument Clone(string server, string game, int source)
        {
            lock (Gate)
            {
                var dir = FolderPath(server, game);
                var result = Load(server, game, source);
                var doc = Parse(result.Xml);
                var list = Parse(Read(Path.Combine(dir, "event_list.xml")));
                int id = checked(Files(dir).Select(p => int.Parse(Path.GetFileNameWithoutExtension(p).Substring(6)))
                    .Concat(list.Root.Elements("event").Select(e => int.TryParse((string)e.Element("id"), out var n) ? n : 0)).DefaultIfEmpty(0).Max() + 1);

                long next = 1;
                foreach (var g in new[] { "GT5", "GT6" })
                {
                    var other = Path.Combine(root, server, g);
                    if (!Directory.Exists(other)) continue;
                    foreach (var f in Files(other))
                    {

                        foreach (Match m in Regex.Matches(Read(f), "<(?:event_id|board_id)\\s+value=\"([0-9]+)\""))
                            if (long.TryParse(m.Groups[1].Value, out long used)) next = Math.Max(next, checked(used + 1));
                    }
                }
                foreach (var folder in doc.Descendants("folder_id")) folder.SetAttributeValue("value", id);
                var boardMap = new Dictionary<string, string>();
                foreach (var ev in doc.Descendants("events").Elements("event"))
                {
                    ev.Element("event_id").SetAttributeValue("value", next);
                    foreach (var board in ev.Descendants("board_id")) { boardMap[Value(board)] = next.ToString(); board.SetAttributeValue("value", next); }
                    next++;
                }
                var item = Parse(result.ListingXml).Root;
                foreach (var board in item.Descendants("board_id"))
                    if (boardMap.TryGetValue(board.Value, out var replacement)) board.Value = replacement;
                item.SetElementValue("id", id);
                var start = DateTime.UtcNow;
                foreach (var date in doc.Descendants().Where(e => e.Name == "begin_date" || e.Name == "end_date"))
                    date.Value = (date.Name == "begin_date" ? start : start.AddDays(30)).ToString("yyyy/MM/dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                foreach (var date in item.Descendants().Where(e => e.Name == "begin_date" || e.Name == "end_date"))
                    date.Value = (date.Name == "begin_date" ? start : start.AddDays(30)).ToString("yyyy-MM-ddTHH:mm:ss+00:00", System.Globalization.CultureInfo.InvariantCulture);
                result.Folder = id;
                result.Xml = doc.ToString();
                result.ListingXml = item.ToString();
                result.Revision = Revision(dir, id);
                return Normalize(result);
            }
        }

        public SeasonalDocument Normalize(SeasonalDocument request)
        {
            var dir = FolderPath(request.Server, request.Game);
            EventPath(dir, request.Folder);
            var doc = Parse(request.Xml);
            var item = Parse(request.ListingXml).Root;
            if (doc.Root.Name != "xml" || !doc.Root.Elements("GameParameter").Any() || item.Name != "event")
                throw new ArgumentException("Wymagany XML /xml/GameParameter i wpis listy /event.");
            if ((string)item.Element("id") != request.Folder.ToString()) throw new ArgumentException("ID wpisu listy nie pasuje do folderu.");
            var ids = new List<long>();
            foreach (var gp in doc.Root.Elements("GameParameter"))
            {
                if (Value(gp.Element("folder_id")) != request.Folder.ToString()) throw new ArgumentException("folder_id nie pasuje do edytowanego pliku.");
                var events = gp.Elements("events").Elements("event").ToArray();
                if (events.Length == 0) throw new ArgumentException("GameParameter musi zawierać event.");
                foreach (var ev in events)
                {
                    if (!long.TryParse(Value(ev.Element("event_id")), out var id) || id <= 0 || ids.Contains(id)) throw new ArgumentException("ID eventów muszą być dodatnie i unikalne.");
                    ids.Add(id);


                    var logoImage = ev.Element("information")?.Element("logo_image_path");
                    if (request.Game == "GT6" && logoImage != null)
                    {
                        var imageName = ((string)logoImage.Attribute("value") ?? logoImage.Value).Trim();
                        if (imageName.Length == 0) logoImage.Remove();
                        else
                        {
                            if (imageName.Length > 128 || !Regex.IsMatch(imageName, @"\A[a-zA-Z0-9_-]+\.(png|img)\z"))
                                throw new ArgumentException("Wpisz samą nazwę pliku PNG/IMG, bez adresu URL i katalogów.");
                            logoImage.RemoveNodes();
                            logoImage.SetAttributeValue("value", imageName);
                        }
                    }
                    var logoLayout = ev.Element("information")?.Element("logo_image_layout");
                    if (request.Game == "GT6" && logoLayout != null &&
                        (!int.TryParse(Value(logoLayout), out var layout) || layout < 0 || layout > 4))
                        throw new ArgumentException("Układ kafelka GT6 musi mieć wartość od 0 do 4.");
                    if (string.IsNullOrWhiteSpace(Value(ev.Element("game_mode"))) || ev.Element("track") == null)
                        throw new ArgumentException($"Event {id}: wymagany game_mode i track.");
                    foreach (var count in ev.Element("race")?.Elements().Where(e => e.Name == "racers_max" || e.Name == "entry_max") ?? Enumerable.Empty<XElement>())
                        if (!int.TryParse(Value(count), out var n) || n < 1 || n > 16)
                            throw new ArgumentException($"Event {id}: liczba uczestników musi wynosić od 1 do 16.");
                }
                if (gp.Element("event_id_list") != null) gp.SetElementValue("event_id_list", string.Join(",", events.Select(e => Value(e.Element("event_id")))));
            }
            foreach (var parent in doc.Descendants().Concat(item.DescendantsAndSelf()).Where(e => e.Element("begin_date") != null && e.Element("end_date") != null))
            {
                if (!DateTime.TryParse(parent.Element("begin_date").Value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var begin)
                    || !DateTime.TryParse(parent.Element("end_date").Value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var end) || end <= begin)
                    throw new ArgumentException("Daty początku i końca są niepoprawne (koniec musi być późniejszy).");
            }
            item.SetElementValue("event_id_list", string.Join(",", ids));
            var boards = doc.Descendants("events").Elements("event").Elements("ranking").Elements("board_id").Select(Value).ToArray();
            if (boards.Any(b => !long.TryParse(b, out var n) || n <= 0) || boards.Distinct().Count() != boards.Length)
                throw new ArgumentException("Rankingi muszą mieć dodatnie, unikalne ID.");
            var quickMatches = doc.Descendants("events").Elements("event").Where(e => Category(e) == "quickmatch").ToArray();
            if (quickMatches.Length > 5 || quickMatches.Any(e => long.Parse(Value(e.Element("event_id"))) > 0xFFFFFF))
                throw new ArgumentException("Quick Match: maksymalnie 5 eventów, ID nie większe niż 16777215.");
            var rankingList = item.Element("ranking_list");
            if (rankingList == null && doc.Descendants("ranking").Elements("board_id").Any())
            {
                rankingList = new XElement("ranking_list"); item.Add(rankingList);
            }
            if (rankingList != null)
            {
                var oldEntries = rankingList.Elements("ranking").ToArray();
                rankingList.RemoveNodes();
                foreach (var ev in doc.Descendants("events").Elements("event").Where(e => e.Element("ranking")?.Element("board_id") != null))
                {
                    var board = Value(ev.Element("ranking").Element("board_id"));
                    var existing = oldEntries.FirstOrDefault(e => (string)e.Element("board_id") == board);
                    var entry = existing != null ? new XElement(existing) : new XElement("ranking",
                        ev.Element("information")?.Element("title") is XElement title ? new XElement(title) : new XElement("title", new XElement("GB", "Ranking")));
                    entry.SetElementValue("board_id", board);
                    if (ev.Element("information")?.Element("title") is XElement updatedTitle)
                    {
                        entry.Element("title")?.Remove(); entry.AddFirst(new XElement(updatedTitle));
                    }
                    rankingList.Add(entry);
                }
            }
            return new SeasonalDocument { Server = request.Server, Game = request.Game, Folder = request.Folder,
                Revision = request.Revision, Xml = doc.ToString(), ListingXml = item.ToString(), Confirm = request.Confirm, Withdraw = request.Withdraw };
        }

        public string Publish(SeasonalDocument request, string actor)
        {
            if (!request.Confirm) throw new ArgumentException("Zatwierdź publikację.");
            lock (Gate)
            {
                var normalized = Normalize(request);
                var dir = FolderPath(request.Server, request.Game);
                using var publicationLock = new FileStream(Path.Combine(dir, ".publish.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                if (request.Revision != Revision(dir, request.Folder)) throw new InvalidOperationException("Pliki zmieniły się od otwarcia edytora. Otwórz aktualną wersję; nie nadpisano zmian.");
                var listPath = Path.Combine(dir, "event_list.xml");
                var eventPath = EventPath(dir, request.Folder);
                var list = Parse(Read(listPath));
                var ids = Parse(normalized.Xml).Descendants("events").Elements("event").Select(e => Value(e.Element("event_id"))).ToHashSet();
                foreach (var file in Files(dir).Where(p => p != eventPath))
                    foreach (Match m in Regex.Matches(Read(file), "<event_id\\s+value=\"([0-9]+)\""))
                        if (ids.Contains(m.Groups[1].Value)) throw new ArgumentException("ID eventu jest już użyte w innym folderze.");
                var oldXml = Read(eventPath);
                var oldIds = Regex.Matches(oldXml, "<(?:event_id|board_id)\\s+value=\"([0-9]+)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToHashSet();
                var newIds = Parse(normalized.Xml).Descendants().Where(e => e.Name == "event_id" || e.Name == "board_id")
                    .Select(Value).Where(id => !oldIds.Contains(id)).ToHashSet();
                foreach (var g in new[] { "GT5", "GT6" })
                {
                    var other = Path.Combine(root, request.Server, g);
                    if (!Directory.Exists(other)) continue;
                    foreach (var f in Files(other).Where(f => f != eventPath))
                        foreach (Match m in Regex.Matches(Read(f), "<(?:event_id|board_id)\\s+value=\"([0-9]+)\""))
                            if (newIds.Contains(m.Groups[1].Value)) throw new ArgumentException("Nowe ID eventu/rankingu jest już zajęte. Odśwież listę i utwórz nowy szkic.");
                }
                var entry = list.Root.Elements("event").SingleOrDefault(e => (string)e.Element("id") == request.Folder.ToString());
                if (request.Withdraw) entry?.Remove();
                else if (entry == null) list.Root.Add(Parse(normalized.ListingXml).Root); else entry.ReplaceWith(Parse(normalized.ListingXml).Root);
                var backup = Path.Combine(dir, ".history", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(backup);
                var oldEvent = Read(eventPath);
                File.WriteAllText(Path.Combine(backup, "event_list.xml"), Read(listPath));
                if (File.Exists(eventPath)) File.WriteAllText(Path.Combine(backup, Path.GetFileName(eventPath)), oldEvent);
                File.WriteAllText(Path.Combine(backup, "audit.txt"), $"UTC: {DateTime.UtcNow:O}\nUser: {actor}\nGame: {request.Game}\nFolder: {request.Folder}\nWithdraw: {request.Withdraw}\n");

                if (request.Withdraw) Replace(Path.Combine(dir, $"event_{request.Folder}.listing.xml"), normalized.ListingXml);
                Replace(eventPath, normalized.Xml);
                try { Replace(listPath, list.ToString()); }
                catch { if (oldEvent.Length > 0) Replace(eventPath, oldEvent); else File.Delete(eventPath); throw; }
                return Path.GetFileName(backup);
            }
        }
        private static void Replace(string path, string content)
        {
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { File.WriteAllText(temp, content, new UTF8Encoding(false)); File.Move(temp, path, true); }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        public object Catalog(string server, string game)
        {
            var dir = FolderPath(server, game);
            var options = new Dictionary<string, HashSet<string>>();
            var sections = new Dictionary<string, string>();
            foreach (var file in Files(dir))
            {
                XDocument doc;
                try { doc = Parse(Read(file)); } catch (Exception e) when (e is XmlException || e is ArgumentException) { continue; }
                foreach (var node in doc.Descendants())
                {
                    foreach (var attr in node.Attributes())
                    {
                        var key = node.Parent?.Name.LocalName + ">" + node.Name.LocalName + "@" + attr.Name.LocalName;
                        if (!options.ContainsKey(key)) options[key] = new();
                        if (attr.Value.Length < 100 && options[key].Count < 500) options[key].Add(attr.Value);
                    }
                    if (node.Parent?.Name == "event" && node.Parent.Parent?.Name == "events") sections.TryAdd(node.Name.LocalName, node.ToString());
                }
            }
            return new { options = options.ToDictionary(p => p.Key, p => p.Value.OrderBy(s => s).ToArray()), sections, nextId = NextId(server) };
        }
    }
}
