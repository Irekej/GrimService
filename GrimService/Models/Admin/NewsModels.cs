using System;
using System.Collections.Generic;

namespace GrimService.Models.Admin
{
    public sealed class NewsPost
    {
        public int Id { get; set; }
        public string Game { get; set; } = "GT6";
        public string Server { get; set; } = "dev";
        public string Region { get; set; } = "us";
        public string Title { get; set; } = "";
        public string Headline { get; set; } = "";
        public string Body { get; set; } = "";
        public int Category1 { get; set; } = 1;
        public int Category2 { get; set; }
        public bool Published { get; set; }
        public bool Deleted { get; set; }
        public int Version { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<NewsImage> Images { get; set; } = new();
        public List<NewsTranslation> Translations { get; set; } = new();

        public void PrepareLanguageFields()
        {
            Translations ??= new();
            foreach (string region in new[] { "us", "gb", "pl", "es", "fr", "br", "pt", "jp", "de", "it" })
                if (region != Region && !Translations.Exists(t => t.Region == region))
                    Translations.Add(new NewsTranslation { Region = region });
        }
    }
    public sealed class NewsTranslation
    {
        public int NewsId { get; set; }
        public string Region { get; set; }
        public string Title { get; set; } = "";
        public string Headline { get; set; } = "";
        public string Body { get; set; } = "";
    }
    public sealed class NewsImage
    {
        public int Id { get; set; }
        public int NewsId { get; set; }
        public string Caption { get; set; } = "";
        public byte[] Data { get; set; }
    }
    public sealed class NewsIndexModel
    {
        public string Game { get; set; }
        public string Server { get; set; }
        public string Region { get; set; }
        public IEnumerable<NewsPost> Posts { get; set; }
    }
}
