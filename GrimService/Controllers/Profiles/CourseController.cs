using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Formatters.Xml;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

using GrimService.Config;
using GrimService.Utils;
using GrimService.Database.Controllers;
using GrimService.Models;
using GrimService.Models.Xml;
using GrimService.Filters;
using GrimService.Services;
using GrimService.Database.Tables;
using Microsoft.AspNetCore.Hosting.Server;

namespace GrimService.Controllers
{
    [ApiController]
    [PDIClient]
    [Authorize]
    [Produces("application/xml")]
    public class CourseController : GrimControllerBase
    {
        private readonly ILogger<CourseController> _logger;
        private readonly GameServerOptions _gsOptions;
        private readonly CourseDBManager _courses;
        private readonly PhotoDBManager _photos;
        private readonly CourseGT5DBManager _coursesgt5;
        private readonly UserDBManager _users;

        public CourseController(PlayerManager players,
            CourseDBManager courses,
            PhotoDBManager photos,
            CourseGT5DBManager coursesgt5,
            UserDBManager users,
            IOptions<GameServerOptions> options,
            ILogger<CourseController> logger)
            : base(players)
        {
            _logger = logger;
            _gsOptions = options.Value;
            _users = users;
            _coursesgt5 = coursesgt5;
            _photos = photos;
            _courses = courses;
        }

        [HttpPost]
        [Route("/ap/course/")]
        public async Task<ActionResult> Post()
        {
            var player = Player;
            if (player is null)
            {
                var detectedPlayer = await _users.GetByIPAddress(Request.HttpContext.Connection.RemoteIpAddress.ToString());
                _logger.LogWarning("Could not get current player. PSN: {detectedPlayer} IP: {ipaddr}", detectedPlayer, Request.HttpContext.Connection.RemoteIpAddress.ToString());
                return Unauthorized();
            }

            if (_gsOptions.GameType != GameType.GT6)
            {
                _logger.LogWarning("Got course getlist request on non GT6");
                return BadRequest();
            }

            GrimRequest requestReq = await GrimRequest.Deserialize(Request.Body);
            if (requestReq is null)
            {

                var badReq = GrimResult.FromInt(-1);
                return BadRequest(badReq);
            }

            _logger.LogDebug("[{name}] Log: <- Got course request: {command}", Player.Data.PSNUserId, requestReq.Command);

            switch (requestReq.Command)
            {
                case "course.getlist":
                    return await OnGetList(requestReq, player);
                case "course.copy":
                    return await OnCopyCourse(requestReq, player);


                case "course.update":
                    return await OnRequestUpdateCourse(requestReq, player);
                case "course.deletecourse":
                    return await OnRequestDeleteCourse(requestReq, player);
                case "course.delete":
                    return await OnRequestDeleteCourseGT6(requestReq, player);
                    /*
                    case "course.getphoto":
                        return await GetPhoto(requestReq, player); 
                    */


            }

            _logger.LogDebug("<- Got unknown course command: {command}", requestReq.Command);
            var badReqs = GrimResult.FromInt(-1);
            return BadRequest(badReqs);
        }

        private async Task<ActionResult> OnCopyCourse(GrimRequest request, Player player)
        {
            if (player.Token == null || player.Token.ExpiryDate <= DateTime.Now) return Unauthorized();
            if (player.Data.LastGame != "GT6") return BadRequest(GrimResult.FromInt(-1));
            if (!request.TryGetParameterLongByKey("src_course_id", out var sourceId) || sourceId <= 0 ||
                !request.TryGetParameterByKey("src_user_id", out var sourceUser) ||
                string.IsNullOrWhiteSpace(sourceUser.Text) || sourceUser.Text.Length > 32)
                return BadRequest(GrimResult.FromInt(-1));
            try
            {
                var copyId = await _courses.CopyAsync(GT6CourseId.ToDatabase(sourceId), sourceUser.Text, player.Data.Id,
                    player.Data.PSNUserId, _gsOptions.XmlResourcePath);
                if (copyId == null) return Ok(GrimResult.FromBool(false));
                _logger.LogInformation("[{User}] Course copied: source={Source}, copy={Copy}",
                    player.Data.PSNUserId, sourceId, copyId);
                return Ok(GrimResult.FromBool(true));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{User}] Course copy failed: source={Source}", player.Data.PSNUserId, sourceId);
                return Ok(GrimResult.FromBool(false));
            }
        }

        private async Task<ActionResult> OnRequestUpdateCourse(GrimRequest gRequest, Player player)
        {
            if (Player is null)
            {
                _logger.LogWarning("Could not get current player. IP: {RemoteIpAddress}", Request.HttpContext.Connection.RemoteIpAddress.ToString());
                return Unauthorized();
            }

            if (!gRequest.TryGetParameterLongByKey("course_id", out long course_id))
            {
                _logger.LogWarning($"Got course.update request with missing 'course_id' parameter");
                return BadRequest();
            }

            course_id = GT6CourseId.ToDatabase(course_id);
            var course = await _courses.GetByIDAsync(course_id);
            if (course is null) return NotFound();
            if (course.UserId != player.Data.Id) return Forbid();

            if (!gRequest.TryGetParameterByKey("status", out var status))
            {
                if (gRequest.TryGetParameterByKey("title", out var title))
                {
                    if (gRequest.TryGetParameterByKey("comment", out var comment))
                    {
                        await _courses.UpdateCourseTitleComment(course_id, title.Text, comment.Text);
                        return Ok(GrimResult.FromBool(true));
                    }
                    await _courses.UpdateCourseTitle(course_id, title.Text);
                    return Ok(GrimResult.FromBool(true));
                }
                if (gRequest.TryGetParameterByKey("comment", out var comment2))
                {
                    await _courses.UpdateCourseComment(course_id, comment2.Text);
                    return Ok(GrimResult.FromBool(true));
                }
                _logger.LogWarning($"Got course.update request with missing parameter");
                return BadRequest();
            }

            await _courses.UpdateCourseStatus(course_id, status.Text);

            return Ok(GrimResult.FromBool(true));
        }

        private async Task<ActionResult> OnRequestDeleteCourse(GrimRequest gRequest, Player player)
        {
            if (Player is null)
            {
                _logger.LogWarning("Could not get current player. IP: {RemoteIpAddress}", Request.HttpContext.Connection.RemoteIpAddress.ToString());
                return Unauthorized();
            }

            if (!gRequest.TryGetParameterLongByKey("course_id", out long course_id))
            {
                _logger.LogWarning($"Got photo.upload request with missing 'photo_id' parameter");
                return BadRequest();
            }

            var course = await _coursesgt5.GetByIDAsync(course_id);
            if (course is null)
                return NotFound();

            if (course.UserId != player.Data.Id)
                return Forbid();

            await _coursesgt5.RemoveAsync(course_id);

            return Ok(GrimResult.FromBool(true));
        }

        private async Task<ActionResult> OnRequestDeleteCourseGT6(GrimRequest gRequest, Player player)
        {
            if (Player is null)
            {
                _logger.LogWarning("Could not get current player. IP: {RemoteIpAddress}", Request.HttpContext.Connection.RemoteIpAddress.ToString());
                return Unauthorized();
            }

            if (!gRequest.TryGetParameterLongByKey("course_id", out long course_id))
            {
                _logger.LogWarning($"Got photo.upload request with missing 'photo_id' parameter");
                return BadRequest();
            }

            course_id = GT6CourseId.ToDatabase(course_id);
            var course = await _courses.GetByIDAsync(course_id);
            if (course is null)
                return NotFound();

            if (course.UserId != player.Data.Id)
                return Forbid();

            await _courses.RemoveAsync(course_id);

            return Ok(GrimResult.FromBool(true));
        }


        [HttpGet]
        [Route("/photo/image/{args}.png")]
        public async Task<ActionResult> Get(string args)
        {
            if (Player == null) return Unauthorized();
            string[] spl = args.Split('_');
            if (spl.Length != 2)
                return BadRequest();
            if (!long.TryParse(spl[0], out long photoId) || !int.TryParse(spl[1], out int type))
                return BadRequest();
            if (photoId <= 0 || (type != 0 && type != 1)) return BadRequest();

            string requestedImage = Path.Combine(_gsOptions.XmlResourcePath, "photo", "image", $"{photoId}_{type}.png");
            _logger.LogDebug("[{User}] Course PNG request: photo={PhotoId}, variant={Variant}, requestedFileExists={Exists}",
                Player.Data.PSNUserId, photoId, type, System.IO.File.Exists(requestedImage));
            if (System.IO.File.Exists(requestedImage))
                return File(new FileStream(requestedImage, FileMode.Open, FileAccess.Read), "image/png");

            if (System.IO.File.Exists($"{_gsOptions.XmlResourcePath}/photo/image/{photoId}_0.png"))
            {


                FileStream fs = new FileStream($"{_gsOptions.XmlResourcePath}/photo/image/{photoId}_0.png", FileMode.Open, FileAccess.Read);
                return File(fs, "image/png");
            }
            else if (System.IO.File.Exists($"{_gsOptions.XmlResourcePath}/photo/image/{photoId}_1.png"))
            {
                FileStream fs = new FileStream($"{_gsOptions.XmlResourcePath}/photo/image/{photoId}_1.png", FileMode.Open, FileAccess.Read);
                return File(fs, "image/png");
            }
            else if (System.IO.File.Exists($"{_gsOptions.XmlResourcePath}/photo/image/{photoId}.png"))
            {
                FileStream fs = new FileStream($"{_gsOptions.XmlResourcePath}/photo/image/{photoId}.png", FileMode.Open, FileAccess.Read);
                return File(fs, "image/png");
            }
            else
            {
                _logger.LogWarning($"[{Player.Data.PSNUserId}] GET: Could not find photo with id {photoId} \n Request: GET /photo/image/{args}.png");
                return NotFound();
            }



        }

        /*
        [HttpGet]
        [Route("/photo/image/1001{args}.png")]
        public async Task Get(string args)
        {
            string pngFile = $"course/image/1001{args}.png";
            await this.SendFile(_gsOptions.XmlResourcePath, pngFile);
        }
        */

        [HttpGet]
        [Route("/[controller]/data/{courseId:long}.pwy")]
        public async Task GetTrackPWY(long courseId)
        {
            string pwyFile = $"course/gt5/{courseId}.pwy";
            await this.SendFile(_gsOptions.XmlResourcePath, pwyFile);
            _logger.LogDebug("[{name}] Log: Course Get Request => {courseId}", Player.Data.PSNUserId, courseId);
        }

        [HttpGet]
        [Route("/[controller]/data/{courseId:long}.dat")]
        public async Task GetTrack(long courseId)
        {
            courseId = GT6CourseId.ToDatabase(courseId);
            if (courseId <= 0) { Response.StatusCode = 400; return; }
            string tedFile = $"course/data/{courseId}.dat";
            await this.SendFile(_gsOptions.XmlResourcePath, tedFile);
            _logger.LogDebug("[{name}] Log: Course Get Request => {courseId}", Player.Data.PSNUserId, courseId);
        }

        [HttpGet]
        [Route("/course/list/{UserId}.xml")]
        public async Task<ActionResult> GetUserCourseList(string UserId)
        {

            var currentPlayer = Player;
            if (currentPlayer is null)
            {
                _logger.LogWarning("Could not get current player. IP: {RemoteIpAddress}", Request.HttpContext.Connection.RemoteIpAddress.ToString());
                return Unauthorized();
            }

            var courses = await _coursesgt5.GetAllCoursesOfPSNUserId(UserId);

            var list = new CourseListGT5();
            list.CoursesGT5 = new List<CoursesGT5>();
            foreach (var course in courses)
            {
                var courseData = new CoursesGT5();
                courseData.CourseId = course.Id;
                courseData.UserId = course.UserId;
                courseData.Psnuserid = course.PSNUserId;
                courseData.Title = course.Title;
                courseData.Comment = course.Comment;
                courseData.Length = course.Length;
                courseData.Weather = course.Weather;
                courseData.StartDatetime = course.StartDatetime;
                courseData.Author = course.Author;
                courseData.Elevation = course.Elevation;
                courseData.Straight = course.Straight;
                courseData.Corner = course.Corner;
                courseData.Datetime = course.Datetime;
                courseData.OnlineRace = course.OnlineRace;
                courseData.WeatherValue0 = course.WeatherValue0;
                courseData.InitialRetention = course.InitialRetention;
                courseData.WeatherAccel = course.WeatherAccel;
                courseData.TimeProgressSpeed = course.TimeProgressSpeed;
                courseData.PhotoId = course.PhotoId;
                courseData.UpdateTime = course.UpdateTime;
                courseData.CreateTime = course.CreateTime;
                list.CoursesGT5.Add(courseData);
            }
            return Ok(list);
        }

        /*
        public async Task OnCourseUploadGT6()
        {

            
            CourseDTO course = new CourseDTO(Player.Data.Id, );

            using var ms = Program.StreamManager.GetStream();
            await Request.Body.CopyToAsync(ms);
            ms.Position = 0;
            long courseid = await _courses.AddAsync(course);
            Directory.CreateDirectory($"{_gsOptions.XmlResourcePath}/course/gt6");

            using (var fs = new FileStream($"{_gsOptions.XmlResourcePath}/course/gt6/{courseid}.dat", FileMode.Create))
                await ms.CopyToAsync(fs);

            _logger.LogInformation("[{name}] Log: Player uploaded a course to server (GT6)", Player.Data.PSNUserId);

            var result = GrimResult.FromLong(1);
            return Ok(result);
        }
        */

        [HttpPost]
        [Route("/ap/course/complete/")]
        public async Task<ActionResult> OnRequestComplete([FromForm] IFormFile file)
        {

            Player player = Player;
            if (player is null)
            {
                _logger.LogWarning("Could not get current player. IP: {RemoteIpAddress}", Request.HttpContext.Connection.RemoteIpAddress.ToString());
                return Unauthorized();
            }

            var value = Request.Headers["X-gt-xml"];
            if (value.Count != 1)
                return BadRequest();

            string xml = value[0];
            if (string.IsNullOrEmpty(xml))
                return BadRequest();

            GrimRequest request = GrimRequest.Deserialize(xml);
            if (request is null)
                return BadRequest();

            if (request.Command != "course.complete")
                return BadRequest();

            if (!request.TryGetParameterByKey("course_id", out var courseIdParam))
            {
                _logger.LogWarning("Got course complete without 'course_id'");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("title", out var titleParam))
            {
                _logger.LogWarning("Got course complete without 'title'");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("comment", out var commentParam))
            {
                _logger.LogWarning("Got course complete without 'comment'");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("theme", out var themeParam))
            {
                _logger.LogWarning("Got course complete without 'theme'");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("length", out var lengthParam))
            {
                _logger.LogWarning("Got course complete without 'length'");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("corners", out var cornersParam))
            {
                _logger.LogWarning("Got course complete without 'corners'");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("one_way", out var onewayParam))
            {
                _logger.LogWarning("Got course complete without 'oneway'");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("straight", out var straightParam))
            {
                _logger.LogWarning("Got course complete without 'straight'");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("height", out var heightParam))
            {
                _logger.LogWarning("Got course complete without 'height'");
                return BadRequest();
            }

            if (!long.TryParse(courseIdParam.Text, out var courseIdLong) || courseIdLong <= 0)
                return BadRequest();
            courseIdLong = GT6CourseId.ToDatabase(courseIdLong);
            var existingCourse = await _courses.GetByIDAsync(courseIdLong);
            if (existingCourse == null) return NotFound();
            if (existingCourse.UserId != player.Data.Id) return Forbid();

            using var ms = Grim.Program.StreamManager.GetStream();


            if (file != null) await file.CopyToAsync(ms);
            else await Request.Body.CopyToAsync(ms);
            ms.Position = 0;
            var signature = new byte[8];
            if (ms.Read(signature, 0, signature.Length) != signature.Length ||
                !signature.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            {
                _logger.LogWarning("[{User}] course.complete rejected: invalid PNG for course={CourseId}, bytes={Bytes}",
                    player.Data.PSNUserId, courseIdLong, ms.Length);
                return BadRequest(GrimResult.FromBool(false));
            }
            ms.Position = 0;

            Directory.CreateDirectory($"{_gsOptions.XmlResourcePath}/photo/image");

            string courseText = "course";
            string typeCourse = "15";

            PhotoDTO photo = new PhotoDTO(Player.Data.Id, Player.Data.PSNUserId, commentParam.Text, titleParam.Text, courseText, typeCourse, DateTime.Now);
            long newId = await _photos.AddAsync(photo);

            ms.Position = 0;
            using (var fs = new FileStream($"{_gsOptions.XmlResourcePath}/photo/image/{newId}_0.png", FileMode.Create))
                await ms.CopyToAsync(fs);

            string status = "1";
            string courseId = courseIdParam.Text;

            await _courses.UpdateCourseData(courseIdLong, titleParam.Text, commentParam.Text, themeParam.Text, lengthParam.Text, cornersParam.Text, onewayParam.Text, straightParam.Text, heightParam.Text, status, newId);

            _logger.LogInformation("[{name}] Log: Uploaded a course to the server ID: {courseId}", Player.Data.PSNUserId, courseId);

            return Ok(GrimResult.FromBool(true));
        }

        private async Task<ActionResult> OnGetList(GrimRequest request, Player player)
        {
            if (!request.TryGetParameterByKey("user_id", out var userIdParam) || userIdParam.Text.Length > 32)
            {
                _logger.LogWarning("Got course getlist without 'user_id'");
                return BadRequest();
            }


            var courses = await _courses.GetAllCoursesOfUser(userIdParam.Text);

            var courseList = new CourseList();
            courseList.Courses = new List<Course>();
            foreach (var course in courses)
            {
                var courseData = new Course();
                courseData.CourseId = GT6CourseId.ToClient(course.Id);
                courseData.Comment = course.Comment;
                courseData.Height = course.Height;
                courseData.OneWay = course.OneWay;
                courseData.Status = course.Status;
                courseData.Straight = course.Straight;
                courseData.Title = course.Title;
                courseData.Theme = course.Theme;
                courseData.Length = course.Length;
                courseData.OwnerId = course.OwnerId;
                courseData.Corners = course.Corners;
                courseData.SourceUserId = course.SourceUserId;
                courseData.PhotoId = course.PhotoId;
                courseData.PhotoHidden = course.PhotoHidden;
                courseData.CommentHidden = course.CommentHidden;
                courseData.TitleHidden = course.TitleHidden;
                courseData.UpdateTime = course.UpdateTime;
                courseData.CreateTime = course.CreateTime;
                courseData.TransferTime = course.TransferTime;
                _logger.LogDebug("[{User}] Course list item: course={CourseId}, status={Status}, photo={PhotoId}, created={Created}, updated={Updated}",
                    player.Data.PSNUserId, course.Id, course.Status, course.PhotoId,
                    courseData.CreateTimeString, courseData.UpdateTimeString);
                courseList.Courses.Add(courseData);
            }
            return Ok(courseList);

            /*
            string eventFilePath;
            eventFilePath = $"Resources/course/list/public_courses.xml";

            if (System.IO.File.Exists(eventFilePath))
            {
                // Pobranie zawartości pliku
                string xmlContent = await System.IO.File.ReadAllTextAsync(eventFilePath);

                // Określenie odpowiedniego MIME Type dla XML
                var contentType = "application/xml";

                // Zwrócenie zawartości pliku XML jako ContentResult
                var contentResult = new ContentResult
                {
                    Content = xmlContent,
                    ContentType = contentType,
                    StatusCode = 200
                };

                return contentResult;
            }
            else
            {
                return NotFound();
            }
            */
        }
            

        [HttpPost]
        [Route("/ap/course/upload")]
        public async Task<ActionResult> OnRequestUploadCourse()
        {

            var player = Player;
            if (player?.Token == null || player.Token.ExpiryDate <= DateTime.Now) return Unauthorized();
            if (player.Data.LastGame != "GT5") return BadRequest();

            if (player is null)
            {
                _logger.LogWarning("Could not get current player. IP: {RemoteIpAddress}", Request.HttpContext.Connection.RemoteIpAddress.ToString());
                return Unauthorized();
            }

            var value = Request.Headers["X-gt-xml"];
            if (value.Count != 1)
                return BadRequest();

            string xml = value[0];
            if (string.IsNullOrEmpty(xml))
                return BadRequest();

            GrimRequest gRequest = GrimRequest.Deserialize(xml);
            if (gRequest is null)
                return BadRequest();

            if (gRequest.Command != "course.upload")
                return BadRequest();

            if (!gRequest.TryGetParameterByKey("title", out var titleParam))
            {
                _logger.LogWarning($"Got course.upload without title.");
                return BadRequest();
            }
            if (!gRequest.TryGetParameterByKey("comment", out var commentParam))
            {
                _logger.LogWarning($"Got course.upload without comment.");
                return BadRequest();
            }
            if (!gRequest.TryGetParameterByKey("genre", out var genreParam))
            {
                _logger.LogWarning($"Got course.upload without genre.");
                return BadRequest();
            }
            if (!gRequest.TryGetParameterByKey("length", out var lengthParam))
            {
                _logger.LogWarning($"Got course.upload without comment.");
                return BadRequest();
            }
            if (!gRequest.TryGetParameterByKey("weather", out var weatherParam))
            {
                _logger.LogWarning("Got course.upload without weather.");
                return BadRequest();
            }
            if (!gRequest.TryGetParameterByKey("start_datetime", out var startDatetimeParam))
            {
                _logger.LogWarning("Got course.upload without start_datetime.");
                return BadRequest();
            }
            if (!gRequest.TryGetParameterByKey("author", out var authorParam))
            {
                _logger.LogWarning("Got course.upload without author.");
                return BadRequest();
            }
            if (!gRequest.TryGetParameterByKey("elevation", out var elevationParam))
            {
                _logger.LogWarning("Got course.upload without elevation.");
                return BadRequest();
            }
            if (!gRequest.TryGetParameterByKey("straight", out var straightParam))
            {
                _logger.LogWarning("Got course.upload without straight.");
                return BadRequest();
            }
            if (!gRequest.TryGetParameterByKey("corner", out var cornerParam))
            {
                _logger.LogWarning("Got course.upload without corner.");
                return BadRequest();
            }
            if (!gRequest.TryGetParameterByKey("datetime", out var datetimeParam))
            {
                _logger.LogWarning("Got course.upload without datetime.");
                return BadRequest();
            }
            if (!gRequest.TryGetParameterByKey("online_race", out var onlineRaceParam))
            {
                _logger.LogWarning("Got course.upload without online_race.");
                return BadRequest();
            }
            if (!gRequest.TryGetParameterByKey("weather_value0", out var weatherValue0Param))
            {
                _logger.LogWarning("Got course.upload without weather_value0.");
                return BadRequest();
            }
            if (!gRequest.TryGetParameterByKey("initial_retention", out var initialRetentionParam))
            {
                _logger.LogWarning("Got course.upload without initial_retention.");
                return BadRequest();
            }
            if (!gRequest.TryGetParameterByKey("weather_accel", out var weatherAccelParam))
            {
                _logger.LogWarning("Got course.upload without weather_accel.");
                return BadRequest();
            }
            if (!gRequest.TryGetParameterByKey("time_progress_speed", out var timeProgressSpeedParam))
            {
                _logger.LogWarning("Got course.upload without time_progress_speed.");
                return BadRequest();
            }

            CourseGT5DTO course = new CourseGT5DTO
            (
                Player.Data.Id,
                Player.Data.PSNUserId,
                titleParam.Text,
                commentParam.Text,
                genreParam.Text,
                lengthParam.Text,
                weatherParam.Text,
                startDatetimeParam.Text,
                authorParam.Text,
                elevationParam.Text,
                straightParam.Text,
                cornerParam.Text,
                datetimeParam.Text,
                onlineRaceParam.Text,
                weatherValue0Param.Text,
                initialRetentionParam.Text,
                weatherAccelParam.Text,
                timeProgressSpeedParam.Text,
                DateTime.Now,
                DateTime.Now
            );


            using var ms = Grim.Program.StreamManager.GetStream();
            await Request.Body.CopyToAsync(ms);
            ms.Position = 0;
            long courseid = await _coursesgt5.AddAsync(course);
            Directory.CreateDirectory($"{_gsOptions.XmlResourcePath}/course/gt5");

            using (var fs = new FileStream($"{_gsOptions.XmlResourcePath}/course/gt5/{courseid}.pwy", FileMode.Create))
                await ms.CopyToAsync(fs);

            _logger.LogInformation("[{name}] Log: Player uploaded a course to server (GT5)", Player.Data.PSNUserId);


            var result = GrimResult.FromLong(courseid);
            return Ok(result);

        }

        [HttpPost]
        [Route("/ap/course/thumbnail_upload")]
        [RequestSizeLimit(2 * 1024 * 1024)]
        public async Task<ActionResult> OnRequestUploadThumbnail()
        {

            var player = Player;

            if (player is null)
            {
                _logger.LogWarning("Could not get current player. IP: {RemoteIpAddress}", Request.HttpContext.Connection.RemoteIpAddress.ToString());
                return Unauthorized();
            }

            if (player.Data.LastGame != "GT5") return BadRequest();
            if (Request.HasFormContentType) return StatusCode(415);
            var header = Request.Headers["X-gt-xml"];
            if (header.Count != 1 || string.IsNullOrWhiteSpace(header[0])) return BadRequest();
            GrimRequest metadata;
            try { metadata = GrimRequest.Deserialize(header[0]); }
            catch (InvalidOperationException) { return BadRequest(); }
            if (metadata?.Params?.ParamList == null || metadata.Command != "thumbnail.upload" ||
                !metadata.TryGetParameterLongByKey("course_id", out var courseId) || courseId <= 0) return BadRequest();
            var course = await _coursesgt5.GetByIDAsync(courseId);
            if (course == null) return NotFound();
            if (course.UserId != player.Data.Id) return Forbid();
            using var ms = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await Request.Body.ReadAsync(buffer, HttpContext.RequestAborted)) > 0)
            {
                if (ms.Length + read > 2 * 1024 * 1024) return StatusCode(413);
                await ms.WriteAsync(buffer.AsMemory(0,read), HttpContext.RequestAborted);
            }
            if (ms.Length == 0 || (Request.ContentLength.HasValue && ms.Length != Request.ContentLength)) return BadRequest();

            long photoId = await _photos.AddAsync(new PhotoDTO(player.Data.Id,player.Data.PSNUserId,"","","","gt5_course",DateTime.UtcNow));
            var directory = Path.Combine(_gsOptions.XmlResourcePath,"photo","image");
            var path = Path.Combine(directory,photoId+"_0.png");
            bool published = false, created = false;
            try
            {
                Directory.CreateDirectory(directory);
                using (var output = new FileStream(path,FileMode.CreateNew,FileAccess.Write))
                { created=true; ms.Position=0; await ms.CopyToAsync(output); }
                published = await _coursesgt5.SetPhotoAsync(courseId,player.Data.Id,photoId);
                return published ? Ok(GrimResult.FromLong(photoId)) : Conflict();
            }
            finally
            {
                if (!published)
                {
                    if (created && System.IO.File.Exists(path)) System.IO.File.Delete(path);
                    await _photos.RemoveAsync(photoId);
                }
            }

        }
    }
}



/*

var user = await _users.GetByPSNUserIdAsync(player.Data.PSNUserId);
var courses = await _courses.GetAllCoursesOfUser(user.Id);

var courseList = new CourseList();
courseList.Courses = new List<Course>();

var course1 = new Course()

{
    Comment = "[Glitched] Coaster Of Speed",
    CourseId = 1001001,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "[Glitched] Coaster Of Speed",
    Theme = "scenery_andalusia",
    Length = 6000,
    OwnerId = player.Data.PSNUserId,
    Corners = 2,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001001,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course2 = new Course()

{
    Comment = "[Glitched] Special Stage Route VMAX",
    CourseId = 1001002,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "[Glitched] Special Stage Route VMAX",
    Theme = "scenery_andalusia",
    Length = 7440,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001002,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course3 = new Course()

{
    Comment = "[Glitched] Wild Ride",
    CourseId = 1001003,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "[Glitched] Wild Ride",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 2,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001003,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course4 = new Course()

{
    Comment = "[Glitched] Keep on Turnin",
    CourseId = 1001004,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "[Glitched] Keep on Turnin",
    Theme = "scenery_andalusia",
    Length = 2078,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001004,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course5 = new Course()

{
    Comment = "[Fun] 6 High Speed Flying Jumps",
    CourseId = 1001005,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "[Fun] 6 High Speed Flying Jumps",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 14,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001005,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course6 = new Course()

{
    Comment = "[Fun] Hoops in the Hills(PRO)",
    CourseId = 1001006,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "[Fun] Hoops in the Hills(PRO)",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001006,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course7 = new Course()

{
    Comment = "[Fun] Red Devil Canyon v1.3",
    CourseId = 1001007,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "[Fun] Red Devil Canyon v1.3",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001007,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course8 = new Course()

{
    Comment = "[Fun] THE-WORLD",
    CourseId = 1001008,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "[Fun] THE-WORLD",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001008,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course9 = new Course()

{
    Comment = "[Fun] Fat Death",
    CourseId = 1001009,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "[Fun] Fat Death",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001009,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course10 = new Course()

{
    Comment = "[Fun] Thin Death",
    CourseId = 1001010,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "[Fun] Thin Death",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001010,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course11 = new Course()

{
    Comment = "[Fun] Eifel Xtreme Offroad Challenge",
    CourseId = 1001011,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "[Fun] Eifel Xtreme Offroad Challenge",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001011,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course12 = new Course()

{
    Comment = "[Fun] Ski Jump",
    CourseId = 1001012,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "[Fun] Ski Jump",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001012,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course13 = new Course()

{
    Comment = "[Fun] A Very Mountainous Journey",
    CourseId = 1001013,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "[Fun] A Very Mountainous Journey",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001013,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course14 = new Course()

{
    Comment = "[Fun] ウ○コ・インターナショナルサーキット",
    CourseId = 1001014,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "[Fun] ウ○コ・インターナショナルサーキット",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001014,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course15 = new Course()

{
    Comment = "AymericFR Track_1",
    CourseId = 1001015,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "AymericFR Track_1",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001015,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course16 = new Course()

{
    Comment = "Street Drag Drift Test",
    CourseId = 1001016,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "Street Drag Drift Test",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001016,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course17 = new Course()

{
    Comment = "High Speed Street",
    CourseId = 1001017,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "High Speed Street",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001017,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course18 = new Course()

{
    Comment = "High Speed Street Death Valley",
    CourseId = 1001018,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "High Speed Street Death Valley",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001018,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course19 = new Course()

{
    Comment = "Street Drag Drift Death Valley",
    CourseId = 1001019,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "Street Drag Drift Death Valley",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001019,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course20 = new Course()

{
    Comment = "Drift X Drag Park V1",
    CourseId = 1001020,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "Drift X Drag Park V1",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001020,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course21 = new Course()

{
    Comment = "Drift X Drag Park V2",
    CourseId = 1001021,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "Drift X Drag Park V2",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001021,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course22 = new Course()

{
    Comment = "Oran Park",
    CourseId = 1001022,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "Oran Park",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001022,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course23 = new Course()

{
    Comment = "Highway south Ibiza",
    CourseId = 1001023,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "Highway south Ibiza",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001023,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course24 = new Course()

{
    Comment = "Interlagos",
    CourseId = 1001024,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "Interlagos",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001024,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course25 = new Course()

{
    Comment = "Austin Track",
    CourseId = 1001025,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "Austin Track",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001025,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course26 = new Course()

{
    Comment = "60m Home Straight",
    CourseId = 1001026,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "60m Homw Straight",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001026,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course27 = new Course()

{
    Comment = "Autodromo Oscar y Juan Galvez",
    CourseId = 1001027,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "Autodromo Oscar y Juan Galvez",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001027,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course28 = new Course()

{
    Comment = "Autodromo Parque Provincia del Neuquen",
    CourseId = 1001028,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "Autodromo Parque Provincia del Neuquen",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001028,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course29 = new Course()

{
    Comment = "Barcelona No Chicane",
    CourseId = 1001029,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "Barcelona No Chicane",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001029,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course30 = new Course()

{
    Comment = "China track",
    CourseId = 1001030,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "China track",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001030,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course31 = new Course()

{
    Comment = "Greece GP",
    CourseId = 1001031,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "Greece GP",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001031,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course32 = new Course()

{
    Comment = "Jeddah",
    CourseId = 1001032,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "Jeddah",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001032,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course33 = new Course()

{
    Comment = "Monte Haruna",
    CourseId = 1001033,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "Monte Haruna",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001033,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

var course34 = new Course()

{
    Comment = "Canyon Di Amalfi",
    CourseId = 1001034,
    Height = 200,
    OneWay = 0,
    Status = 2,
    Straight = 200,
    Title = "Canyon Di Amalfi",
    Theme = "scenery_andalusia",
    Length = 7878,
    OwnerId = player.Data.PSNUserId,
    Corners = 69,
    OriginalCreator = player.Data.PSNUserId,
    PhotoId = 01001034,
    UpdateTime = DateTime.Now,
    PhotoHidden = 0,
};

courseList.Courses.Add(course1);
courseList.Courses.Add(course2);
courseList.Courses.Add(course3);
courseList.Courses.Add(course4);
courseList.Courses.Add(course5);
courseList.Courses.Add(course6);
courseList.Courses.Add(course7);
courseList.Courses.Add(course8);
courseList.Courses.Add(course9);
courseList.Courses.Add(course10);
courseList.Courses.Add(course11);
courseList.Courses.Add(course12);
courseList.Courses.Add(course13);
courseList.Courses.Add(course14);
courseList.Courses.Add(course15);
courseList.Courses.Add(course16);
courseList.Courses.Add(course17);
courseList.Courses.Add(course18);
courseList.Courses.Add(course19);
courseList.Courses.Add(course20);
courseList.Courses.Add(course21);
courseList.Courses.Add(course22);
courseList.Courses.Add(course23);
courseList.Courses.Add(course24);
courseList.Courses.Add(course25);
courseList.Courses.Add(course26);
courseList.Courses.Add(course27);
courseList.Courses.Add(course28);
courseList.Courses.Add(course29);
courseList.Courses.Add(course30);
courseList.Courses.Add(course31);
courseList.Courses.Add(course32);
courseList.Courses.Add(course33);
courseList.Courses.Add(course34);
return Ok(courseList);
*/
