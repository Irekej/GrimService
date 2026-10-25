using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using GrimService.Models.Xml;

namespace GrimService.Services
{


    public sealed class BSpecRaceProtocol
    {
        private readonly Dictionary<string,string> _values;
        public BSpecRaceProtocol(GrimRequest request)
        {
            var values=request.Params?.ParamList ?? throw new FormatException("Missing params");
            if(values.Count>300 || values.Any(p=>p.Key==null || (p.Text?.Length??0)>4096) ||
                values.Select(p=>p.Key).Distinct(StringComparer.Ordinal).Count()!=values.Count)
                throw new FormatException("Invalid params");
            _values=values.ToDictionary(p=>p.Key,p=>p.Text??"",StringComparer.Ordinal);
        }
        public string Text(string key,int max=256)
        {
            if(!_values.TryGetValue(key,out var value)||value.Length>max)throw new FormatException(key);
            return value;
        }
        public long Number(string key,long min=0,long max=long.MaxValue)
        {
            if(!long.TryParse(Text(key),NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out var n)||n<min||n>max)
                throw new FormatException(key);
            return n;
        }
        public long[] Drivers()
        {
            int size=(int)Number("size",1,16);
            var ids=Enumerable.Range(0,size).Select(i=>Number("driver_id"+i.ToString("D2"),1)).ToArray();
            if(ids.Distinct().Count()!=size)throw new FormatException("Duplicate driver");
            return ids;
        }


        public sealed record Row(long DriverId,long Owner,string User,string Driver,string Car,
            int Grid,int Position,long Total,long Best,int Level,int Credit,int Experience);
        public static int? WinningPosition(Row[] rows)
        {


            var positions = rows.Select(r => r.Position).OrderBy(p => p).ToArray();
            if (positions.Length == 0) return null;
            if (positions.SequenceEqual(Enumerable.Range(0, positions.Length))) return 0;
            if (positions.SequenceEqual(Enumerable.Range(1, positions.Length))) return 1;
            return null;
        }
        public Row[] Results()
        {
            var ids=Drivers();
            return ids.Select((id,i)=>
            {
                string s=i.ToString("D2",CultureInfo.InvariantCulture);
                return new Row(id,Number("user_number"+s,1),Text("user"+s,32),Text("driver"+s),Text("car"+s),
                    (int)Number("grid"+s,0,16),(int)Number("result"+s,0,16),Number("total"+s),Number("best"+s),
                    (int)Number("driver_level"+s,0,40),(int)Number("online_credit"+s,0,int.MaxValue),
                    (int)Number("online_experience"+s,0,int.MaxValue));
            }).ToArray();
        }
        public string Fingerprint => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            new XElement("params",_values.OrderBy(p=>p.Key,StringComparer.Ordinal).Select(p=>
                new XElement("param",new XAttribute("key",p.Key),p.Value))).ToString(SaveOptions.DisableFormatting))));
        public (int Type,string Xml) Monitor()
        {




            int type=(int)Number("type",0,int.MaxValue);
            var keys=new[]{"slot_id","lap_num","lap_mileage","value1","value2","value3","value4"};
            var xml=new XElement("monitor",new XAttribute("type",type),
                keys.Select(key=>new XAttribute(key,Text(key,4096))));
            return (type,xml.ToString(SaveOptions.DisableFormatting));
        }
        public XElement Summary(long id)
        {
            var start=Timestamp(Number("start_time"));var end=Timestamp(Number("end_time"));
            if(end<start)throw new FormatException("Reversed times");
            return new XElement("summary",new XAttribute("summary_id",id),new XAttribute("course_label",Text("course_label")),
                new XAttribute("lap",Number("lap",0,1000)),new XAttribute("race_info_minute",Number("race_info_minute",0,1440)),
                new XAttribute("start_time",start.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz",CultureInfo.InvariantCulture)),
                new XAttribute("end_time",end.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz",CultureInfo.InvariantCulture)));
        }


        private static DateTimeOffset Timestamp(long seconds)
        {
            try { return DateTimeOffset.FromUnixTimeSeconds(seconds); }
            catch(ArgumentOutOfRangeException) { throw new FormatException("Timestamp"); }
            catch(OverflowException) { throw new FormatException("Timestamp"); }
        }
        public string ResultXml(long id,Row[] rows) => new XElement("bspec_result",Summary(id),
            rows.OrderBy(r=>r.Position).ThenBy(r=>r.Grid).Select(r=>new XElement("result",
                new XAttribute("grid",r.Grid),new XAttribute("result",r.Position),new XAttribute("car",r.Car),
                new XAttribute("driver",r.Driver),new XAttribute("user",r.User),new XAttribute("total",r.Total),
                new XAttribute("best",r.Best),new XAttribute("driver_level",r.Level)))).ToString(SaveOptions.DisableFormatting);
    }
}
