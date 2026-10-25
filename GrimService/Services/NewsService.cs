using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Dapper;
using GrimService.Config;
using GrimService.Models.Admin;
using GrimService.Utils;
using Microsoft.Extensions.Options;

namespace GrimService.Services
{
    public sealed class NewsService
    {
        private readonly IDbConnection db;
        private readonly string root;
        private const string Columns = "id,game,server,region,title,headline,body,category1,category2,published,deleted,version,created_at AS CreatedAt,updated_at AS UpdatedAt";
        public NewsService(IDbConnection db, IOptions<GameServerOptions> options)
        { this.db = db; root = options.Value.XmlResourcePath; }
        public void CreateTable() => db.Execute(@"
            CREATE SEQUENCE IF NOT EXISTS game_news_id_seq START WITH 1000000;
            CREATE TABLE IF NOT EXISTS game_news (
                id INTEGER PRIMARY KEY DEFAULT nextval('game_news_id_seq'),
                game TEXT NOT NULL CHECK(game IN ('GT5','GT6')), server TEXT NOT NULL, region TEXT NOT NULL,
                title TEXT NOT NULL, headline TEXT NOT NULL, body TEXT NOT NULL,
                category1 INTEGER NOT NULL, category2 INTEGER NOT NULL,
                published BOOLEAN NOT NULL DEFAULT FALSE, deleted BOOLEAN NOT NULL DEFAULT FALSE,
                version INTEGER NOT NULL DEFAULT 1, created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
                updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(), updated_by TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS game_news_feed ON game_news(game,server,region,published);
            CREATE TABLE IF NOT EXISTS game_news_translations (
                news_id INTEGER NOT NULL REFERENCES game_news(id),
                region TEXT NOT NULL CHECK(region ~ '^[a-z]{2}$'),
                title TEXT NOT NULL, headline TEXT NOT NULL, body TEXT NOT NULL,
                PRIMARY KEY(news_id,region));
            CREATE TABLE IF NOT EXISTS game_news_images (
                id SERIAL PRIMARY KEY, news_id INTEGER NOT NULL REFERENCES game_news(id),
                caption TEXT NOT NULL DEFAULT '', data BYTEA NOT NULL);
            CREATE TABLE IF NOT EXISTS game_news_audit (
                id BIGSERIAL PRIMARY KEY, news_id INTEGER NOT NULL, actor TEXT NOT NULL,
                action TEXT NOT NULL, happened_at TIMESTAMPTZ NOT NULL DEFAULT NOW(), previous JSONB);
        ");
        public static void Scope(string game, string server, string region)
        {
            if (game != "GT5" && game != "GT6") throw new ArgumentException("Wybierz GT5 lub GT6.");
            if (!Regex.IsMatch(server ?? "", @"\A[a-zA-Z0-9_-]{1,32}\z") || !Regex.IsMatch(region ?? "", @"\A[a-z]{2}\z"))
                throw new ArgumentException("Nieprawidłowy serwer lub region.");
        }
        public async Task<IEnumerable<NewsPost>> List(string game, string server, string region)
        {
            Scope(game, server, region);
            var posts = (await db.QueryAsync<NewsPost>($"SELECT {Columns} FROM game_news WHERE game=@game AND server=@server AND (region=@region OR EXISTS (SELECT 1 FROM game_news_translations t WHERE t.news_id=game_news.id AND t.region=@region)) AND NOT deleted ORDER BY updated_at DESC,id DESC", new { game, server, region })).ToList();
            if (posts.Count > 0)
            {
                var translations = (await db.QueryAsync<NewsTranslation>("SELECT news_id AS NewsId,region,title,headline,body FROM game_news_translations WHERE news_id=ANY(@ids)", new { ids=posts.Select(p=>p.Id).ToArray() })).ToLookup(t=>t.NewsId);
                foreach(var post in posts)
                {
                    post.Translations=translations[post.Id].ToList();
                    ApplyLanguage(post,region);
                }
            }
            return posts;
        }
        public async Task<NewsPost> Get(int id, string game)
        {
            if (game != "GT5" && game != "GT6") throw new ArgumentException("Wybierz GT5 lub GT6.");
            var post = await db.QuerySingleOrDefaultAsync<NewsPost>($"SELECT {Columns} FROM game_news WHERE id=@id AND game=@game AND NOT deleted", new { id, game });
            if (post != null)
            {
                post.Images = (await db.QueryAsync<NewsImage>("SELECT id,news_id AS NewsId,caption FROM game_news_images WHERE news_id=@id ORDER BY id", new { id })).ToList();
                post.Translations = (await db.QueryAsync<NewsTranslation>("SELECT news_id AS NewsId,region,title,headline,body FROM game_news_translations WHERE news_id=@id ORDER BY region", new { id })).ToList();
            }
            return post;
        }
        public async Task<int> Save(NewsPost post, string actor, IReadOnlyList<NewsImage> uploads, int[] removeImages)
        {
            Scope(post.Game, post.Server, post.Region);
            if (string.IsNullOrWhiteSpace(post.Title) || post.Title.Length > 200 || (post.Headline?.Length ?? 0) > 500 || (post.Body?.Length ?? 0) > 30000)
                throw new ArgumentException("Podaj tytuł (do 200 znaków), nagłówek do 500 i treść do 30000 znaków.");
            if (post.Category1 < 0 || post.Category1 > 9999 || post.Category2 < 0 || post.Category2 > 9999) throw new ArgumentException("Nieprawidłowa kategoria newsa.");

            XmlConvert.VerifyXmlChars(post.Title); XmlConvert.VerifyXmlChars(post.Headline ?? ""); XmlConvert.VerifyXmlChars(post.Body ?? "");
            var translations = (post.Translations ?? new()).Where(t=>t != null &&
                (!string.IsNullOrWhiteSpace(t.Title)||!string.IsNullOrWhiteSpace(t.Headline)||!string.IsNullOrWhiteSpace(t.Body))).ToList();
            if (translations.Count > 100 || translations.Select(t=>t.Region).Distinct(StringComparer.Ordinal).Count()!=translations.Count)
                throw new ArgumentException("Nieprawidłowe dane formularza.");
            foreach(var translation in translations)
            {
                Scope(post.Game,post.Server,translation.Region);
                if(translation.Region==post.Region || string.IsNullOrWhiteSpace(translation.Title) || translation.Title.Length>200 ||
                    (translation.Headline?.Length??0)>500 || (translation.Body?.Length??0)>30000)
                    throw new ArgumentException("Podaj tytuł (do 200 znaków), nagłówek do 500 i treść do 30000 znaków.");
                XmlConvert.VerifyXmlChars(translation.Title);XmlConvert.VerifyXmlChars(translation.Headline??"");XmlConvert.VerifyXmlChars(translation.Body??"");
            }
            foreach (var image in uploads) XmlConvert.VerifyXmlChars(image.Caption ?? "");
            if (db.State != ConnectionState.Open) db.Open();
            using var tx = db.BeginTransaction();
            var id = post.Id;
            if (id == 0)
                id = await db.ExecuteScalarAsync<int>(@"INSERT INTO game_news(game,server,region,title,headline,body,category1,category2,published,updated_by)
                    VALUES(@Game,@Server,@Region,@Title,@Headline,@Body,@Category1,@Category2,@Published,@Actor) RETURNING id",
                    new { post.Game,post.Server,post.Region,post.Title,Headline=post.Headline??"",Body=post.Body??"",post.Category1,post.Category2,post.Published,Actor=actor }, tx);
            else
            {
                var old = await db.QuerySingleOrDefaultAsync<NewsPost>($"SELECT {Columns} FROM game_news WHERE id=@id AND game=@Game AND NOT deleted FOR UPDATE", new { id,post.Game },tx);
                if (old == null || old.Version != post.Version) throw new InvalidOperationException("News został zmieniony. Otwórz aktualną wersję.");
                await db.ExecuteAsync("INSERT INTO game_news_audit(news_id,actor,action,previous) SELECT id,@actor,'edit',to_jsonb(n)||jsonb_build_object('translations',(SELECT COALESCE(jsonb_agg(to_jsonb(t)),'[]'::jsonb) FROM game_news_translations t WHERE t.news_id=n.id)) FROM game_news n WHERE id=@id", new { id, actor }, tx);
                await db.ExecuteAsync(@"UPDATE game_news SET server=@Server,region=@Region,title=@Title,headline=@Headline,body=@Body,
                    category1=@Category1,category2=@Category2,published=@Published,version=version+1,updated_at=NOW(),updated_by=@Actor WHERE id=@Id AND game=@Game",
                    new {Id=id,post.Game,post.Server,post.Region,post.Title,Headline=post.Headline??"",Body=post.Body??"",post.Category1,post.Category2,post.Published,Actor=actor},tx);
            }
            await db.ExecuteAsync("DELETE FROM game_news_translations WHERE news_id=@id",new{id},tx);
            foreach(var translation in translations)
                await db.ExecuteAsync("INSERT INTO game_news_translations(news_id,region,title,headline,body) VALUES(@id,@Region,@Title,@Headline,@Body)",
                    new{id,translation.Region,translation.Title,Headline=translation.Headline??"",Body=translation.Body??""},tx);
            if (removeImages?.Length > 0)
                await db.ExecuteAsync("DELETE FROM game_news_images WHERE news_id=@id AND id=ANY(@removeImages)", new { id,removeImages },tx);
            var count = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM game_news_images WHERE news_id=@id", new { id },tx);
            if (count + uploads.Count > 8) throw new ArgumentException("News może zawierać maksymalnie 8 zdjęć.");
            foreach (var image in uploads) await db.ExecuteAsync("INSERT INTO game_news_images(news_id,caption,data) VALUES(@id,@Caption,@Data)", new {id,image.Caption,image.Data},tx);
            if (post.Id == 0) await db.ExecuteAsync("INSERT INTO game_news_audit(news_id,actor,action) VALUES(@id,@actor,'create')",new{id,actor},tx);
            tx.Commit(); return id;
        }
        public async Task Delete(int id, string game, int version, string actor)
        {
            if (db.State != ConnectionState.Open) db.Open();
            using var tx = db.BeginTransaction();
            var changed = await db.ExecuteAsync(@"UPDATE game_news SET deleted=TRUE,published=FALSE,version=version+1,updated_at=NOW(),updated_by=@actor
                WHERE id=@id AND game=@game AND version=@version AND NOT deleted",new{id,game,version,actor},tx);
            if (changed != 1) throw new InvalidOperationException("News został zmieniony. Otwórz aktualną wersję.");
            await db.ExecuteAsync("INSERT INTO game_news_audit(news_id,actor,action) VALUES(@id,@actor,'delete')",new{id,actor},tx);
            tx.Commit();
        }
        public async Task<byte[]> Image(int newsId, int imageId, string game, bool preview = false) =>
            await db.QuerySingleOrDefaultAsync<byte[]>(@"SELECT i.data FROM game_news_images i JOIN game_news n ON n.id=i.news_id
                WHERE n.id=@newsId AND i.id=@imageId AND n.game=@game AND NOT n.deleted AND (@preview OR n.published)",new{newsId,imageId,game,preview});
        private static string Stamp(DateTime time) => time.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'",CultureInfo.InvariantCulture);
        private static bool ApplyLanguage(NewsPost post,string region)
        {
            var translation=post.Translations.FirstOrDefault(t=>t.Region==region);
            if(translation==null && post.Region==region)return true;
            translation ??= post.Translations.FirstOrDefault(t=>t.Region=="us");
            if(translation==null)return post.Region=="us";
            post.Title=translation.Title;post.Headline=translation.Headline;post.Body=translation.Body;
            return true;
        }
        private static XElement Xml(NewsPost n) => new("news",new XElement("news_id",n.Id),
            new XElement("news_category_id1",n.Category1),new XElement("news_category_id2",n.Category2),new XElement("news_category_id3",0),
            new XElement("title",n.Title),new XElement("headline",n.Headline),new XElement("comment",n.Body),
            new XElement("news_time",Stamp(n.CreatedAt)),new XElement("lastupdate",Stamp(n.UpdatedAt)),
            new XElement("images",n.Images.Select(i=>new XElement("image",new XAttribute("id",i.Id),new XAttribute("caption",i.Caption)))));
        private string Legacy(string game,string server,string region,string file)
        {
            Scope(game,server,region);

            foreach (var lang in new[]{region,"us"}.Distinct())
            {
                var path=Path.Combine(root,"news",server,game,lang,file);
                if(File.Exists(path))return path;
            }
            return GT6NewsFiles.Resolve(root,server,region,file);
        }
        private static XElement ReadXml(string path)
        {
            using var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2000000});
            return XElement.Load(reader);
        }
        public async Task<string> Article(string game,string server,string region,int id)
        {
            Scope(game,server,region);
            if(id>=1000000)
            {
                var n=await Get(id,game);
                if(n==null || !n.Published || n.Server!=server || !ApplyLanguage(n,region))return null;
                return Xml(n).ToString(SaveOptions.DisableFormatting);
            }
            var path=Legacy(game,server,region,id+".xml");
            return path==null?null:ReadXml(path).ToString(SaveOptions.DisableFormatting);
        }
        public async Task<string> Feed(string game,string server,string region,int c1,int c2)
        {
            Scope(game,server,region);
            var output=new XElement("newslist");
            var container=game=="GT5"?new XElement("news"):output;
            if(game=="GT5")output.Add(container);
            var posts=await db.QueryAsync<NewsPost>($"SELECT {Columns} FROM game_news WHERE game=@game AND server=@server AND (region IN (@region,'us') OR EXISTS (SELECT 1 FROM game_news_translations t WHERE t.news_id=game_news.id AND t.region IN (@region,'us'))) AND category1=@c1 AND category2=@c2 AND published AND NOT deleted ORDER BY created_at DESC,id DESC LIMIT 100",new{game,server,region,c1,c2});
            foreach(var n in posts)container.Add(new XElement(game=="GT6"?"news_id":"id",new XAttribute("id",n.Id),new XAttribute("lastupdate",Stamp(n.UpdatedAt)),new XAttribute("news_time",Stamp(n.CreatedAt))));
            var path=Legacy(game,server,region,$"l{c1}_{c2}.xml");
            if(path!=null)
            {
                var ids=ReadXml(path).Descendants().Where(e=>e.Name=="id"||e.Name=="news_id"||e.Name=="file_id")
                    .Select(e=>int.TryParse((string)e.Attribute("id")??e.Value,out var id)?id:-1).Where(i=>i>=0&&i<1000000).Distinct().Take(100);
                foreach(var id in ids)
                {
                    var article=await Article(game,server,region,id); if(article==null)continue;
                    var xml=XElement.Parse(article); DateTimeOffset timestamp;
                    if(!DateTimeOffset.TryParse((string)xml.Element("lastupdate"),CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out timestamp)) timestamp=DateTimeOffset.UnixEpoch;
                    container.Add(new XElement(game=="GT6"?"news_id":"id",new XAttribute("id",id),new XAttribute("lastupdate",Stamp(timestamp.UtcDateTime)),new XAttribute("news_time",Stamp(timestamp.UtcDateTime))));
                }
            }
            return output.ToString(SaveOptions.DisableFormatting);
        }
        public async Task<string> Categories(string game,string server,string region)
        {
            var path=Legacy(game,server,region,"root.xml");
            var xml=path==null?new XElement("categories"):ReadXml(path);
            var pairs=await db.QueryAsync<NewsPost>("SELECT DISTINCT category1,category2 FROM game_news WHERE game=@game AND server=@server AND (region IN (@region,'us') OR EXISTS (SELECT 1 FROM game_news_translations t WHERE t.news_id=game_news.id AND t.region IN (@region,'us'))) AND published AND NOT deleted",new{game,server,region});
            foreach(var pair in pairs)if(!xml.Elements("category").Any(e=>(string)e.Attribute("id1")==pair.Category1.ToString()&&(string)e.Attribute("id2")==pair.Category2.ToString()))
                xml.Add(new XElement("category",new XAttribute("id1",pair.Category1),new XAttribute("id2",pair.Category2),new XAttribute("root",1),new XAttribute("name","News")));
            return xml.ToString(SaveOptions.DisableFormatting);
        }
    }
}
