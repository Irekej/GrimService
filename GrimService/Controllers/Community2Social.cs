using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using GrimService.Models;
using GrimService.Models.Xml;
using GrimService.Database.Tables;
using GrimService.Features.Clubs;
using GrimService.Controllers.Clubs;

namespace GrimService.Controllers
{
    public partial class Community2Controller
    {
        private static string Param(GrimRequest r,string key)=>r.TryGetParameterByKey(key,out var p)?p.Text:null;
        private static bool ValidText(string text,int max,bool required=false)=>ClubText.IsValid(text,max,required);
        private async Task<ActionResult> OnSocialRequest(Player player,GrimRequest r)
        {
            if(player.Data.LastGame!="GT6"||_social==null)return Forbid();
            if(r.Params?.ParamList==null)return BadRequest();
            r.TryGetParameterLongByKey("community_id",out var publicClub);
            r.TryGetParameterLongByKey("community_event_id",out var publicEvent);
            long clubId=ToDatabaseClubId(publicClub), eventId=ToDatabaseClubId(publicEvent);
            if(clubId<0||eventId<0)return BadRequest();
            if(r.Command=="event.getListUser" || r.Command=="event.getIdListUser")
            {
                var username=Param(r,"user_id")??player.Data.PSNUserId;
                if(!ValidText(username,32,true))return BadRequest();
                var events=await _eventReads.EventsAsync(clubId,player.Data.Id,eventId,username);
                return r.Command=="event.getListUser" ? ClubEventReadResponses.Events(events) : ClubEventReadResponses.Memberships(events,username);
            }
            ClubEventDTO current=null;
            if(eventId>0)
            {
                current=await _eventReads.FindAsync(clubId,player.Data.Id,eventId);
                if(current==null)return NotFound(GrimResult.FromInt(-1));
                clubId=current.ClubId;
            }
            if(ClubOperationResponses.TryGetOperation(r.Command,out var operation))
                return ClubOperationResponses.ToResponse(await _clubOperations.ExecuteAsync(operation,clubId,
                    new ClubActor(player.Data.LastGame,player.Data.Id,player.Data.PSNUserId),Param(r,"user_id")),CommunityIdOffset);
            var access=await _clubAccess.ReadAsync(clubId,player.Data.Id,player.Data.PSNUserId);
            if(access==null)return NotFound(GrimResult.FromInt(-1));
            bool owner=access.IsOwner,member=access.IsMember;
            if(!access.CanRead)return Forbid();
            var participant=new ClubEventParticipant(player.Data.Id,player.Data.LastGame,member,owner);
            switch(r.Command)
            {
                case "event.getFriendList":
                    if(r.Command=="event.getFriendList"&&current==null)return BadRequest();
                    return ClubEventReadResponses.Friends(await _eventReads.FriendsAsync(clubId,player.Data.Id,eventId));
                case "event.getList":return ClubEventReadResponses.Events(await _eventReads.EventsAsync(clubId,player.Data.Id));
                case "event.get":return current==null?BadRequest():ClubEventReadResponses.Events(new[]{current});
                case "event.create":
                case "event.update":
                    int? mode = r.TryGetParameterIntByKey("mode",out var suppliedMode) ? suppliedMode : null;
                    long? ranking = r.TryGetParameterLongByKey("ranking_board",out var suppliedRanking) ? suppliedRanking : null;
                    return ClubEventWriteResponses.ToResponse(await _eventWrites.SaveAsync(clubId,current,
                        new ClubEventEditor(player.Data.Id,player.Data.LastGame,owner),
                        new ClubEventDraft(Param(r,"title"),Param(r,"description"),Param(r,"regulation"),Param(r,"open"),
                            Param(r,"start_time"),Param(r,"end_time"),mode,ranking,Param(r,"activity")=="1",Param(r,"bbs_comment")),
                        r.Command=="event.create"));
                case "event.delete":
                    return ClubEventWriteResponses.ToResponse(await _eventWrites.DeleteAsync(clubId,current,
                        new ClubEventEditor(player.Data.Id,player.Data.LastGame,owner)));
                case "event.entry":
                case "event.cancel":
                    return ClubEventWriteResponses.ToResponse(await _eventActions.MembershipAsync(clubId,current?.Id,participant,r.Command=="event.entry"));
                case "event.invite":
                    return ClubEventWriteResponses.ToResponse(await _eventActions.InviteAsync(current?.Id,participant,Param(r,"user_id")));
                case "event_template.getList":
                    return ClubEventReadResponses.Templates(await _eventReads.TemplatesAsync(clubId));
                case "event_template.get":
                case "event_template.create":
                case "event_template.update":
                case "event_template.delete":
                    r.TryGetParameterLongByKey("community_event_template_id",out var templateId);
                    if(r.Command!="event_template.create"&&templateId<=0)return BadRequest();
                    if(r.Command=="event_template.get")return ClubEventReadResponses.Templates(await _eventReads.TemplatesAsync(clubId,templateId));
                    var templateOperation=r.Command=="event_template.create"?ClubTemplateOperation.Create:
                        r.Command=="event_template.delete"?ClubTemplateOperation.Delete:ClubTemplateOperation.Update;
                    return ClubEventWriteResponses.ToResponse(await _eventTemplates.SaveAsync(clubId,templateId,
                        new ClubEventEditor(player.Data.Id,player.Data.LastGame,owner),templateOperation,Param(r,"description"),Param(r,"regulation")));
                case "event_result.getList":
                    return current==null?BadRequest():ClubEventReadResponses.Results(await _eventReads.ResultsAsync(clubId,eventId));
                case "event_result.create":
                    return ClubEventWriteResponses.ToResponse(await _eventActions.AddResultAsync(clubId,current?.Id,participant,Param(r,"result")));
                case "event_result.delete":
                    long? resultId=r.TryGetParameterLongByKey("community_event_result_id",out var suppliedResultId)?suppliedResultId:null;
                    return ClubEventWriteResponses.ToResponse(await _eventActions.DeleteResultAsync(clubId,current?.Id,participant,resultId));
                default:return BadRequest(GrimResult.FromInt(-1));
            }
        }
    }
}
