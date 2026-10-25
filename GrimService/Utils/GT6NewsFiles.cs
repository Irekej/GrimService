using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
namespace GrimService.Utils
{
    public static class GT6NewsFiles
    {
        public static string Resolve(string root,string server,string region,string file)
        {
            if(!Regex.IsMatch(server??"",@"\A[a-zA-Z0-9_-]{1,32}\z")||!Regex.IsMatch(region??"",@"\A[a-zA-Z0-9_-]{1,16}\z")||
                !Regex.IsMatch(file??"",@"\A(?:[0-9]+|l[0-9]+_[0-9]+|root)\.xml\z"))return null;


            foreach(var pair in new[]{(server,region),(server,"us"),("dev",region),("dev","us")}.Distinct())
            {
                var path=Path.Combine(root,"news",pair.Item1,pair.Item2,file);
                if(File.Exists(path))return path;
            }
            return null;
        }
        private static XElement Load(string path)
        {
            using var reader=XmlReader.Create(path,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2000000});
            return XElement.Load(reader);
        }
        public static string Article(string root,string server,string region,int id)
        {
            var path=Resolve(root,server,region,id+".xml");
            return path==null?null:Load(path).ToString(SaveOptions.DisableFormatting);
        }
        public static string List(string root,string server,string region,int category1,int category2)
        {
            var path=Resolve(root,server,region,$"l{category1}_{category2}.xml");
            var list=new XElement("newslist");
            if(path==null)return list.ToString(SaveOptions.DisableFormatting);
            var ids=Load(path).Descendants().Where(e=>e.Name.LocalName=="id"||e.Name.LocalName=="news_id"||e.Name.LocalName=="file_id")
                .Select(e=>int.TryParse((string)e.Attribute("id")??e.Value,out var id)?id:-1).Where(id=>id>=0).Distinct().Take(100);
            foreach(var id in ids)
            {
                var articlePath=Resolve(root,server,region,id+".xml");
                if(articlePath==null)continue;
                var article=Load(articlePath);
                DateTimeOffset timestamp;
                var date=(string)article.Element("lastupdate")??(string)article.Element("news_time");
                if(!DateTimeOffset.TryParse(date,CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal,out timestamp))
                    timestamp=new DateTimeOffset(File.GetLastWriteTimeUtc(articlePath));
                var time=timestamp.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'",CultureInfo.InvariantCulture);

                list.Add(new XElement("news_id",new XAttribute("id",id),new XAttribute("lastupdate",time),new XAttribute("news_time",time)));
            }
            return list.ToString(SaveOptions.DisableFormatting);
        }
    }
}
