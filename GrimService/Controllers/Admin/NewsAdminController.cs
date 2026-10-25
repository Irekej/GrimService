using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using GrimService.Models.Admin;
using GrimService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using GrimService.Features.Images;

namespace GrimService.Controllers.Admin
{
    [Authorize(AuthenticationSchemes = "AdminCookie", Roles = "Administrator")]
    [Route("admin/news")]
    public sealed class NewsAdminController : Controller
    {
        private readonly NewsService news;
        private readonly WebText text;
        public NewsAdminController(NewsService news,WebText text) { this.news=news;this.text=text; }
        [HttpGet("")]
        public async Task<IActionResult> Index(string game="GT6",string server="dev",string region="us")
        {
            try { return View("~/Views/Admin/News.cshtml",new NewsIndexModel{Game=game,Server=server,Region=region,Posts=await news.List(game,server,region)}); }
            catch(ArgumentException e){return BadRequest(text[e.Message]);}
        }
        [HttpGet("edit")]
        public async Task<IActionResult> Edit(int id=0,string game="GT6",string server="dev",string region="us")
        {
            try {
                NewsService.Scope(game,server,region);
                var post=id==0?new NewsPost{Game=game,Server=server,Region="us",Category1=game=="GT5"?0:1,Category2=game=="GT5"?0:1}:await news.Get(id,game);
                post?.PrepareLanguageFields();
                return post==null?NotFound():View("~/Views/Admin/NewsEdit.cshtml",post);
            } catch(ArgumentException e){return BadRequest(text[e.Message]);}
        }
        [HttpPost("save")]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(20*1024*1024)]
        [RequestFormLimits(MultipartBodyLengthLimit=20*1024*1024)]
        public async Task<IActionResult> Save(NewsPost post,List<IFormFile> pictures,int[] removeImages,string imageCaption)
        {
            try
            {
                if(!ModelState.IsValid)throw new ArgumentException("Nieprawidłowe dane formularza.");
                var uploads=new List<NewsImage>();
                if((pictures?.Count??0)>8 || (imageCaption?.Length??0)>300)throw new ArgumentException("News może zawierać maksymalnie 8 zdjęć.");
                foreach(var file in pictures??new List<IFormFile>())
                {
                    if(file.Length<=0||file.Length>2*1024*1024)throw new ArgumentException("Zdjęcie musi być plikiem JPEG lub PNG do 2 MB.");
                    using var buffer=new MemoryStream(); await file.CopyToAsync(buffer);
                    var bytes=buffer.ToArray();
                    var jpeg=bytes.Length>=3&&bytes[0]==255&&bytes[1]==216&&bytes[2]==255;
                    var png=bytes.Length>=8&&bytes.Take(8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10});
                    if(!jpeg&&!png)throw new ArgumentException("Zdjęcie musi być plikiem JPEG lub PNG do 2 MB.");
                    var info=GameImages.Inspect(bytes);
                    if((long)info.Width*info.Height>8000000||info.Width>4096||info.Height>4096)
                        throw new ArgumentException("Zdjęcie jest zbyt duże. Maksymalnie 4096 × 4096 i 8 megapikseli.");
                    uploads.Add(new NewsImage{Data=GameImages.PrepareNewsJpeg(bytes),Caption=imageCaption??""});
                }
                var id=await news.Save(post,User.Identity.Name,uploads,removeImages);
                TempData["NewsMessage"]="News zapisany.";
                return RedirectToAction(nameof(Edit),new{id,game=post.Game,server=post.Server,region=post.Region});
            }
            catch(Exception e) when(e is ArgumentException||e is InvalidOperationException||e is XmlException)
            {
                ModelState.AddModelError("",text[e is InvalidGameImageException?"Zdjęcie musi być plikiem JPEG lub PNG do 2 MB.":e.Message]);
                if(post.Id>0&&(post.Game=="GT5"||post.Game=="GT6"))post.Images=(await news.Get(post.Id,post.Game))?.Images??new();
                post.PrepareLanguageFields();
                return View("~/Views/Admin/NewsEdit.cshtml",post);
            }
        }
        [HttpPost("delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id,string game,int version)
        {
            var post=await news.Get(id,game); if(post==null)return NotFound();
            try { await news.Delete(id,game,version,User.Identity.Name); }
            catch(InvalidOperationException e){return Conflict(text[e.Message]);}
            TempData["NewsMessage"]="News wycofany. Dane pozostają w bazie.";
            return RedirectToAction(nameof(Index),new{game,server=post.Server,region=post.Region});
        }
        [HttpGet("image/{id:int}/{imageId:int}")]
        public async Task<IActionResult> PreviewImage(int id,int imageId,string game)
        {
            var bytes=await news.Image(id,imageId,game,true);
            Response.Headers["Cache-Control"]="no-store";
            Response.Headers["X-Content-Type-Options"]="nosniff";
            return bytes==null?NotFound():File(bytes,"image/jpeg");
        }
    }
}
