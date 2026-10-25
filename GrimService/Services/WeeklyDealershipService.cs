using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using GrimService.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GrimService.Services
{

    public sealed class WeeklyDealershipService
    {
        private static readonly DateTimeOffset Epoch = new(2020, 1, 6, 0, 0, 0, TimeSpan.Zero);
        private static readonly int[] Boundaries = { 20000, 50000, 100000, 250000, 1000000 };
        private readonly TimeProvider clock;
        private readonly ILogger<WeeklyDealershipService> logger;
        private readonly string catalogPath;
        private readonly object gate = new();
        private Car[][] groups;
        private int cachedWeek = -1;
        private string cachedXml;

        public sealed class Car
        {
            public string Code { get; set; }
            public int BasePrice { get; set; }
        }
        private sealed class Catalog
        {
            public Catalog() { }
            public Car[] Cars { get; set; }
        }
        public WeeklyDealershipService(IOptions<GameServerOptions> options, TimeProvider clock,
            ILogger<WeeklyDealershipService> logger)
        {
            this.clock = clock;
            this.logger = logger;
            catalogPath = Path.Combine(options.Value.XmlResourcePath, "event", "dev", "used_cars", "weekly_catalog.json");
        }

        private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        public static int PriceGroup(int price) => Boundaries.Count(limit => price >= limit);
        public static int WeekNumber(DateTimeOffset date)
        {
            var week = (int)Math.Floor((date.ToUniversalTime() - Epoch).TotalDays / 7);
            if (week < 0) throw new ArgumentOutOfRangeException(nameof(date));
            return week;
        }
        private static Car[][] Prepare(IEnumerable<Car> cars)
        {
            var all = cars.ToArray();
            if (all.Length == 0 || all.Any(c => c == null || !Regex.IsMatch(c.Code ?? "", @"\A[a-z0-9_]+\z") ||
                c.BasePrice < 100 || c.BasePrice > 20000000) || all.Select(c => c.Code).Distinct(StringComparer.Ordinal).Count() != all.Length)
                throw new InvalidDataException("Invalid GT5 weekly dealer catalog.");
            var result = Enumerable.Range(0, 6).Select(group => all.Where(c => PriceGroup(c.BasePrice) == group)
                .OrderBy(c => Hash("gt5-dealer-v1/" + c.Code), StringComparer.Ordinal).ToArray()).ToArray();

            if (result.Any(g => g.Length < 10)) throw new InvalidDataException("GT5 weekly dealer needs at least ten cars in each price group.");
            return result;
        }
        public string GetCurrentXml()
        {
            lock (gate)
            {
                if (groups == null)
                {
                    string catalogJson;
                    if(File.Exists(catalogPath)) catalogJson=File.ReadAllText(catalogPath);
                    else
                    {


                        using var resource=typeof(WeeklyDealershipService).Assembly.GetManifestResourceStream("GrimService.GT5WeeklyCatalog.json")
                            ?? throw new FileNotFoundException("GT5 dealer catalog missing from disk and application resources",catalogPath);
                        using var reader=new StreamReader(resource);
                        catalogJson=reader.ReadToEnd();
                        logger.LogWarning("GT5 weekly dealer catalog missing at {Path}; using bundled catalog",catalogPath);
                    }
                    var catalog = JsonSerializer.Deserialize<Catalog>(catalogJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    groups = Prepare(catalog?.Cars ?? Array.Empty<Car>());
                }
                var week = WeekNumber(clock.GetUtcNow());
                if (cachedWeek != week)
                {
                    cachedXml = Render(groups, week);
                    cachedWeek = week;
                    logger.LogInformation("GT5 weekly dealer: version={Version}, weekStartUtc={WeekStart}, cars=30, discount=20-40%",
                        20000 + week, Epoch.AddDays(week * 7));
                }
                return cachedXml;
            }
        }

        public static string Generate(IEnumerable<Car> cars, DateTimeOffset date) => Render(Prepare(cars), WeekNumber(date));
        private static string Render(Car[][] groups, int week)
        {
            var root = new XElement("cars");
            foreach (var language in new[] { "jp", "us", "gb", "fr", "de", "it", "es", "pt", "nl", "ru", "pl", "tr", "el", "tw", "kr", "bp", "ms" })
                root.Add(new XElement(language));


            root.Add(new XElement("version", 20000 + week),
                new XElement("new_date", Epoch.AddDays(week * 7).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)));
            int slot = 0;
            foreach (var group in groups)
                for (int i = 0; i < 5; i++)
                {


                    var car = group[(int)(((long)week * 5 + i) % group.Length)];
                    string key = "gt5-dealer-price-v1/" + week.ToString(CultureInfo.InvariantCulture) + "/" + car.Code;
                    int discount = 20 + Convert.ToInt32(Hash(key).Substring(0, 4), 16) % 21;
                    int targetPrice = (int)((long)car.BasePrice * (100 - discount) / 100);
                    string mileageKey = "gt5-dealer-mileage-v1/" + week.ToString(CultureInfo.InvariantCulture) + "/" + car.Code;
                    int mileage = 500 + (int)(Convert.ToUInt32(Hash(mileageKey).Substring(0, 8), 16) % 59501);


                    int price = (int)Math.Round(targetPrice / (1m - mileage / 200000m), MidpointRounding.AwayFromZero);
                    root.Add(new XElement("car", new XAttribute("usedcar_id", ++slot), new XAttribute("code", car.Code),
                        new XAttribute("price", price), new XAttribute("color", 0),


                        new XAttribute("odometer_min", mileage), new XAttribute("odometer_max", mileage + 1)));
                }
            return root.ToString(SaveOptions.DisableFormatting);
        }
    }
}
