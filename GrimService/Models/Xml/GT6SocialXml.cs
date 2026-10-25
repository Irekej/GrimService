using System;
using System.Globalization;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using GrimService.Database.Tables;
namespace GrimService.Models.Xml
{
    public static class GT6SocialXml
    {
        public static string Time(DateTime t)=>t.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'",CultureInfo.InvariantCulture);
        public static XElement Event(ClubEventDTO e)=>new XElement("community_event",
            new XElement("community_id",e.ClubId+1000000),new XElement("community_event_id",e.Id+1000000),
            new XElement("create_time",Time(e.CreateTime)),new XElement("update_time",Time(e.UpdateTime)),
            new XElement("open",e.Open),new XElement("title",e.Title),new XElement("description",e.Description),
            new XElement("image_id",0),new XElement("photo_id",e.PhotoId??""),new XElement("bbs_thread_id",e.ThreadId),
            new XElement("user_count",e.UserCount),new XElement("friend_count",e.FriendCount),new XElement("status",1),
            new XElement("regulation",e.Regulation),new XElement("mode",e.Mode),new XElement("ranking_board_id",e.RankingBoard),
            new XElement("start_time",Time(e.StartTime)),new XElement("end_time",Time(e.EndTime)),
            new XElement("title_hidden",0),new XElement("description_hidden",0));
        public static XElement Template(ClubTemplateDTO t)=>new XElement("community_event_template",
            new XElement("community_id",t.ClubId+1000000),new XElement("community_event_template_id",t.Id),
            new XElement("description",t.Description),new XElement("regulation",t.Regulation));
        public static XElement Result(ClubResultDTO r)=>new XElement("community_event_result",
            new XAttribute("community_id",r.ClubId+1000000),new XAttribute("community_event_id",r.EventId+1000000),
            new XAttribute("community_event_result_id",r.Id),new XAttribute("user_id",r.Username),
            new XAttribute("create_time",Time(r.CreateTime)),new XAttribute("result",r.Result),new XAttribute("title_hidden",0));
        public static string Activity(IEnumerable<GT6ActivityDTO> rows)=>new XElement("activity_list",rows.Select(a=>
            new XElement("activity",new XAttribute("user_id",a.Username),new XAttribute("community_id",a.ClubId.HasValue?a.ClubId.Value+1000000:0),
                new XAttribute("create_time",Time(a.CreateTime)),new XAttribute("values",a.Values.Length),
                a.Values.Select((v,i)=>new XAttribute("value"+i,v??""))))).ToString(SaveOptions.DisableFormatting);
    }
}
