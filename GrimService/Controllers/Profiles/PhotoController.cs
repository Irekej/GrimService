using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using GrimService.Features.Images;

using GrimService.Config;
using GrimService.Services;
using GrimService.Models;
using GrimService.Models.Xml;
using GrimService.Filters;
using GrimService.Utils;
using GrimService.Database.Controllers;
using GrimService.Database.Tables;
using GrimService.Controllers.Services;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace GrimService.Controllers
{



    [ApiController]
    [PDIClient]
    [Authorize]
    [Route("/ap/[controller]/")]
    [Produces("application/xml")]
    public class PhotoController : GrimControllerBase
    {
        private readonly ILogger<PhotoController> _logger;
        private readonly PlayerManager _players;
        private readonly GameServerOptions _gsOptions;
        private readonly UserDBManager _userDb;
        private readonly FriendDBManager _friendsDb;
        private readonly PhotoDBManager _photoDb;
        private readonly GT6SocialDBManager _social;
        private readonly string _photoType3WebhookUrl;
        private readonly string _photoType9WebhookUrl;
        private readonly GrimService.Infrastructure.Sessions.GameRequestSession _sessions;
        private readonly GrimService.Features.Photos.PhotoLibraryService _library;

        public PhotoController(PlayerManager players,
            IOptions<GameServerOptions> options, 
            ILogger<PhotoController> logger,
            UserDBManager userDb,
            FriendDBManager friendsDb,
            PhotoDBManager photoDb, GT6SocialDBManager social = null, GrimService.Infrastructure.Sessions.GameRequestSession sessions = null,
            GrimService.Features.Photos.PhotoLibraryService library = null,
            Microsoft.Extensions.Configuration.IConfiguration configuration = null)
            : base(players)
        {
            _players = players;
            _logger = logger;
            _gsOptions = options.Value;
            _userDb = userDb;
            _friendsDb = friendsDb;
            _photoDb = photoDb;
            _social = social;
            _photoType3WebhookUrl = configuration?["Discord:PhotoType3WebhookUrl"];
            _photoType9WebhookUrl = configuration?["Discord:PhotoType9WebhookUrl"];
            _library = library ?? new(new GrimService.Features.Photos.DatabasePhotoLibrary(userDb, photoDb));
            _sessions = sessions ?? new GrimService.Infrastructure.Sessions.GameRequestSession(players,
                new GrimService.Infrastructure.Sessions.DatabaseGameRequestRecovery(userDb, players));
        }

        [HttpPost]
        [Route("/ap/photo")]
        public async Task<ActionResult> Post(string server)
        {
            var session = await _sessions.Resolve(Request.Cookies["X-gt-token"]);
            if (session.Status == GrimService.Infrastructure.Sessions.GameSessionStatus.MissingToken) return NotFound();
            if (session.Status != GrimService.Infrastructure.Sessions.GameSessionStatus.Authenticated) return Unauthorized();
            var player = session.Player;

            GrimRequest gRequest = await GrimService.Infrastructure.Protocol.GameRequestPayload.ReadAsync(Request);
            if (gRequest is null)
                return BadRequest();

            _logger.LogDebug("[{name}] Log: <- {command}", player.Data.PSNUserId, gRequest.Command);

            switch (gRequest.Command)
            {
                case "photo.getlist":
                    return await OnRequestGetList(player, gRequest);
                case "photo.getlistfriend":
                    return await OnRequestGetListFriend(player, gRequest);
                case "photo.deleteimage":
                    return await OnRequestDeleteImage(player, gRequest);
            }

            _logger.LogDebug("Got unimplemented photo call: {command}", gRequest.Command);


            return BadRequest();
        }

        private async Task<ActionResult> OnRequestGetList(Player player, GrimRequest request)
        {
            if (!request.TryGetParameterByKey("user_id", out var user) || user.Text.Length > 32 ||
                !request.TryGetParameterByKey("type", out var type)) return BadRequest();
            switch (type.Text)
            {

                case "1":
                case "3": return await GetUserPhotoList(user.Text);
                case "9": return await GetUserCarPhotoList(user.Text);
                default: return BadRequest();
            }
        }

        private Task<ActionResult> OnRequestGetListFriend(Player player, GrimRequest gRequest)
            => GetUserPhotoList(player.Data.PSNUserId);

        [HttpGet]
        [Route("/photo/list/{userId}.xml")]
        public async Task<ActionResult> GetUserPhotoList(string userId)
        {
            if (Player == null) return Unauthorized();
            var list = await _library.Owned(userId, "3");
            return list == null ? NotFound() : Ok(list);
        }

        [HttpGet]
        [Route("/photo/photo_stream.xml")]
        public async Task<ActionResult> GetPhotoStream()
        {
            if (Player == null) return Unauthorized();
            return Ok(await _library.Stream());
        }

        public async Task<ActionResult> GetUserCarPhotoList(string userId)
        {
            if (Player == null) return Unauthorized();
            var list = await _library.Owned(userId, "9");
            return list == null ? NotFound() : Ok(list);
        }

        [HttpGet]
        [Route("/photo/image/0_0.jpg")]
        public async Task<ActionResult> GetPhotoProfile()
        {
            Player player = Player;
            if (player is null)
            {
                _logger.LogWarning("Could not get current player. IP: {RemoteIpAddress}", Request.HttpContext.Connection.RemoteIpAddress.ToString());
                return Unauthorized();
            }

            if (!System.IO.File.Exists($"{_gsOptions.XmlResourcePath}/photo/image/0_0.jpg"))
                return NotFound();

            FileStream fs = new FileStream($"{_gsOptions.XmlResourcePath}/photo/image/0_0.jpg", FileMode.Open, FileAccess.Read);
                return File(fs, "image/jpeg");

        }

        [HttpGet]
        [Route("/photo/image/{args}.jpg")]
        public async Task<ActionResult> GetPhotoJPG(string args, [FromServices] ClubContentDBManager clubContent)
        {
            Player player = Player;
            if (player is null)
            {
                _logger.LogWarning("Could not get current player. IP: {RemoteIpAddress}", Request.HttpContext.Connection.RemoteIpAddress.ToString());
                return Unauthorized();
            }

            string[] spl = args.Split('_');
            if (spl.Length != 2)
                return BadRequest();

            if (!long.TryParse(spl[0], out long photoId) || !int.TryParse(spl[1], out int type))
                return BadRequest();

            if (type != 0 && type != 1) return BadRequest();
            var photo = await _photoDb.GetByIDAsync(photoId);
            if (photo?.Type == "gt6_club" || photo?.Type == "gt6_forum")
            {
                if (player.Data.LastGame != "GT6") return Forbid();
                var image = await clubContent.GetImageAsync(photoId, player.Data.Id);
                return image == null ? NotFound() : File(image.Data, "image/jpeg");
            }

            if (photo == null) return NotFound();





            bool sharedPhoto = photo.Type == "1" || photo.Type == "3" || photo.Type == "9";
            if (!sharedPhoto && photo.UserId != player.Data.Id &&
                !await _friendsDb.IsFriendedToUser(player.Data.Id, photo.UserId))
            {
                _logger.LogDebug("Photo access denied: photo={PhotoId}, user={User}, type={PhotoType}", photoId, player.Data.PSNUserId, photo.Type);
                return Forbid();
            }


            string path = Path.Combine(_gsOptions.XmlResourcePath, "photo", "image", $"{photoId}_1.jpg");
            if (!System.IO.File.Exists(path))
            {
                _logger.LogDebug("Photo file missing: photo={PhotoId}, variant={Variant}", photoId, type);
                return NotFound();
            }
            return File(new FileStream(path, FileMode.Open, FileAccess.Read), "image/jpeg");
        }

        [HttpPost]
        [RequestSizeLimit(GTConstants.MaxPhotoSize)]
        [Route("/ap/photo/upload")]
        public async Task<ActionResult> OnRequestUploadImage()
        {
            var session = await _sessions.Resolve(Request.Cookies["X-gt-token"]);
            if (session.Status == GrimService.Infrastructure.Sessions.GameSessionStatus.MissingToken) return NotFound();
            if (session.Status != GrimService.Infrastructure.Sessions.GameSessionStatus.Authenticated) return Unauthorized();
            var player = session.Player;

            var value = Request.Headers["X-gt-xml"];
            if (value.Count != 1)
                return BadRequest();

            string xml = value[0];
            if (string.IsNullOrEmpty(xml))
                return BadRequest();

            GrimRequest gRequest = GrimService.Infrastructure.Protocol.GameRequestPayload.ReadXml(xml);
            if (gRequest is null)
                return BadRequest();

            if (gRequest.Command != "photo.upload")
                return BadRequest();

            if (!gRequest.TryGetParameterByKey("place", out var place))
            {
                _logger.LogWarning($"Got photo.upload request with missing 'place' parameter");
                return BadRequest();
            }
            if (!gRequest.TryGetParameterByKey("car_name", out var car_name))
            {
                _logger.LogWarning($"Got photo.upload request with missing 'car_name' parameter");
                return BadRequest();
            }
            if (!gRequest.TryGetParameterByKey("comment", out var comment))
            {
                _logger.LogWarning($"Got photo.upload request with missing 'comment' parameter");
                return BadRequest();
            }

            if (comment.Text.Length > 140)
            {
                _logger.LogWarning("Received photo.upload request with too long 'comment' parameter (len {comment.Text.Length} > 140)", comment.Text.Length);
                return BadRequest();
            }

            /*
            if (!gRequest.TryGetParameterByKey("position_x", out var position_x))
            {
                _logger.LogWarning("Got photo.upload request with missing 'position_x' parameter");
                return Ok();
            }

            if (!gRequest.TryGetParameterByKey("position_y", out var position_y))
            {
                _logger.LogWarning("Got photo.upload request with missing 'position_y' parameter");
                return Ok();
            }

            if (!gRequest.TryGetParameterByKey("position_z", out var position_z))
            {
                _logger.LogWarning("Got photo.upload request with missing 'position_z' parameter");
                return Ok();
            }
            */



            if (!gRequest.TryGetParameterByKey("type", out var type))
            {
                _logger.LogWarning($"Got photo.upload request with missing 'type' parameter");
                return BadRequest();
            }




            using var ms = Grim.Program.StreamManager.GetStream();
            await Request.Body.CopyToAsync(ms);
            ms.Position = 0;


            if (!await VerifyImage(ms, GTConstants.MaxPhotoWidth, GTConstants.MaxPhotoHeight))
            return BadRequest();

            PhotoDTO photo = new PhotoDTO(Player.Data.Id, Player.Data.PSNUserId, comment.Text, car_name.Text, place.Text, type.Text, DateTime.Now);
            long newId = await _photoDb.AddAsync(photo);

            Directory.CreateDirectory($"{_gsOptions.XmlResourcePath}/photo/image");

            ms.Position = 0;
            using (var fs = new FileStream($"{_gsOptions.XmlResourcePath}/photo/image/{newId}_1.jpg", FileMode.Create))
            await ms.CopyToAsync(fs);

            if (type.Text == "3" && !string.IsNullOrWhiteSpace(_photoType3WebhookUrl))
            {
                string webhookUrl = _photoType3WebhookUrl;
                string imageUrl = $"http://54.37.232.59/photo/public/images/{newId}_0.jpg";
                string imagePath = $"{_gsOptions.XmlResourcePath}/photo/image/{newId}_1.jpg";

                var jsonContent = new
                {
                    content = $"Player ``{player.Data.PSNUserId}`` uploaded this photo!"
                };

                using (var client = new HttpClient())
                {
                    var formData = new MultipartFormDataContent();
                    var fileContent = new ByteArrayContent(System.IO.File.ReadAllBytes(imagePath));
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                    formData.Add(fileContent, "file", Path.GetFileName(imagePath));
                    var contentJson = JsonConvert.SerializeObject(jsonContent);
                    var jsonContent3 = new StringContent(contentJson, Encoding.UTF8, "application/json");
                    formData.Add(jsonContent3, "payload_json");
                    var response = await client.PostAsync(webhookUrl, formData);
                    if (response.IsSuccessStatusCode)
                    {

                    }
                    else
                    {

                    }
                }
            }
            if (type.Text == "9" && !string.IsNullOrWhiteSpace(_photoType9WebhookUrl))
            {
                string webhookUrl = _photoType9WebhookUrl;
                string imageUrl = $"http://54.37.232.59/photo/public/images/{newId}_0.jpg";
                string imagePath = $"{_gsOptions.XmlResourcePath}/photo/image/{newId}_1.jpg";

                var jsonContent = new
                {
                    content = $"Player ``{player.Data.PSNUserId}`` uploaded his favorite car!"
                };

                using (var client = new HttpClient())
                {
                    var formData = new MultipartFormDataContent();
                    var fileContent = new ByteArrayContent(System.IO.File.ReadAllBytes(imagePath));
                    fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                    formData.Add(fileContent, "file", Path.GetFileName(imagePath));
                    var contentJson = JsonConvert.SerializeObject(jsonContent);
                    var jsonContent3 = new StringContent(contentJson, Encoding.UTF8, "application/json");
                    formData.Add(jsonContent3, "payload_json");
                    var response = await client.PostAsync(webhookUrl, formData);
                    if (response.IsSuccessStatusCode)
                    {

                    }
                    else
                    {

                    }
                }
            }

            _logger.LogInformation("[{name}] Log: Player uploaded a photo to server. ID: {newId}", Player.Data.PSNUserId, newId);

            if (type.Text == "1")
            {
                string avatarId = newId.ToString();




                await _userDb.UpdatePlayerAvatar(avatarId, player.Data.Id);

            }


            if(player.Data.LastGame=="GT6" && _social!=null)
            {
                string[] values=type.Text=="1"?new[]{"PPU",newId.ToString()}:
                    type.Text=="9"?new[]{"CP",newId.ToString(),car_name.Text,place.Text,comment.Text}:
                    type.Text=="3"?new[]{"PP",newId.ToString(),comment.Text}:null;
                if(values!=null)await _social.AddActivityAsync(player.Data.Id,null,values);
            }
            var result = GrimResult.FromLong(newId);
            return Ok(result);
        }








        private async Task<ActionResult> OnRequestDeleteImage(Player player, GrimRequest gRequest)
        {
            if (Player is null)
            {
                _logger.LogWarning("Could not get current player. IP: {RemoteIpAddress}", Request.HttpContext.Connection.RemoteIpAddress.ToString());
                return Unauthorized();
            }

            if (!gRequest.TryGetParameterLongByKey("photo_id", out long photo_id))
            {
                _logger.LogWarning($"Got photo.upload request with missing 'photo_id' parameter");
                return BadRequest();
            }
                var photo = await _photoDb.GetByIDAsync(photo_id);
                if (photo is null)
                    return NotFound();

                if (photo.UserId != player.Data.Id)
                    return Forbid();

                await _photoDb.RemoveAsync(photo_id);

                return Ok(GrimResult.FromBool(true));
            
        }








        private async Task<bool> VerifyImage(Stream stream, int maxWidth, int maxHeight)
        {
            try
            {
                var image = await GameImages.InspectAsync(stream, HttpContext.RequestAborted);
                return image.Kind == ImageFileKind.Jpeg && image.Width <= maxWidth && image.Height <= maxHeight;
            }
            catch (InvalidGameImageException) { return false; }
        }
    }
}
