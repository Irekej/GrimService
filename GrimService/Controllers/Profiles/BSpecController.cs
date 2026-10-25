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
using GrimService.Services;
using GrimService.Models;
using GrimService.Models.Xml;
using GrimService.Filters;
using GrimService.Database.Tables;
using GrimService.Database.Controllers;
using Microsoft.AspNetCore.SignalR;
using System.Xml.Serialization;
using System.Xml.Linq;
using GrimService.Controllers.Services;
using GrimService.Infrastructure.Sessions;
using GrimService.Infrastructure.Protocol;

namespace GrimService.Controllers
{



    [ApiController]
    [PDIClient]
    [Authorize]
    [Produces("application/xml")]
    public class BSpecController : GrimControllerBase
    {
        private readonly ILogger<BSpecController> _logger;
        private readonly GameServerOptions _gsOptions;
        private readonly UserDBManager _userDB;
        private readonly BSpecDriverDBManager _bspecDriverDB;
        private readonly BSpecCarDBManager _bspecCarDB;
        private readonly PlayerManager _players;
        private readonly PhotoDBManager _photoDb;
        private readonly BSpecRaceDBManager _races;
        private readonly GameRequestSession _sessions;

        public BSpecController(PlayerManager players, IOptions<GameServerOptions> options, ILogger<BSpecController> logger, UserDBManager UserDb, BSpecCarDBManager BSpecCarDb, BSpecDriverDBManager BSpecDriverDb, PhotoDBManager photoDb, BSpecRaceDBManager races = null, GameRequestSession sessions = null)
            : base(players)
        {
            _logger = logger;
            _players = players;
            _gsOptions = options.Value;
            _userDB = UserDb;
            _bspecCarDB = BSpecCarDb;
            _bspecDriverDB = BSpecDriverDb;
            _photoDb = photoDb;
            _races = races;
            _sessions = sessions ?? new GameRequestSession(players, new DatabaseGameRequestRecovery(UserDb, players));
        }

        [HttpPost]
        [Route("/ap/bspec/")]
        [Route("/ap/bspecmonitor/")]
        [RequestSizeLimit(65536)]
        public async Task<ActionResult> Post()
        {
            var session = await _sessions.Resolve(Request.Cookies["X-gt-token"]);
            if (session.Status == GrimService.Infrastructure.Sessions.GameSessionStatus.MissingToken) return NotFound();
            if (session.Status != GrimService.Infrastructure.Sessions.GameSessionStatus.Authenticated) return Unauthorized();
            var player = session.Player;
            if (player?.Token == null || player.Token.ExpiryDate <= DateTime.Now) return Unauthorized();
            if (player.Data.LastGame != "GT5") return BadRequest(GrimResult.FromInt(-1));

            /*
            if (_gsOptions.GameType != GameType.GT5)
            {
                _logger.LogWarning("Got a bspec remote request on a GT6 server from host: {host}", Request.Host);
                return Unauthorized();
            }
            */

            GrimRequest gRequest;
            try { gRequest = await GrimRequest.Deserialize(Request.Body); }
            catch (InvalidOperationException) { return BadRequest(); }
            if ((gRequest?.Command is "bspec.beginremotemode" or "bspec.endremotemode" or "bspec.clear") && gRequest.Params?.ParamList == null)
                gRequest.Params = new GrimRequestParams { ParamList = new List<GrimRequestParam>() };
            if (gRequest?.Params?.ParamList is null)
            {

                var badReq = GrimResult.FromInt(-1);
                return BadRequest(badReq);
            }

            _logger.LogDebug("[{name}] Log: <- {command}", player.Data.PSNUserId, gRequest.Command);
            _players.CheckIfNotConnected(player);
            _players.UpdatePlayerActivity(player);

            switch (gRequest.Command)
            {
                case "bspec.beginremotemode":
                case "bspec.endremotemode":
                    if (_races == null) return StatusCode(503);
                    await _races.RemoteMode(player.Data.Id,gRequest.Command == "bspec.beginremotemode");
                    return Ok(GrimResult.FromInt(1));
                case "bspecmonitor.putBSpecMonitor":
                case "bspec.beginrace":
                case "bspec.endrace":
                case "bspec.updateresult":
                    if (_races == null) return StatusCode(503);
                    try
                    {
                        var data = new BSpecRaceProtocol(gRequest);
                        bool success = gRequest.Command == "bspec.beginrace" ? await _races.Begin(player.Data.Id,data) :
                            gRequest.Command == "bspec.endrace" ? await _races.End(player.Data.Id,data) :
                            gRequest.Command == "bspecmonitor.putBSpecMonitor" ? await _races.Monitor(player.Data.Id,data) :
                            await _races.Save(player.Data.Id,data);
                        if (gRequest.Command != "bspecmonitor.putBSpecMonitor")
                            _logger.LogInformation("[{User}] BSpec response: command={Command}, result={Result}",
                                player.Data.PSNUserId,gRequest.Command,success?1:0);
                        return Ok(GrimResult.FromInt(success ? 1 : 0));
                    }
                    catch (FormatException ex)
                    {
                        string actual = gRequest.TryGetParameterByKey(ex.Message,out var invalid) ? invalid.Text : "<missing>";
                        actual=(actual??"").Replace("\r","\\r").Replace("\n","\\n");
                        if(actual.Length>160)actual=actual.Substring(0,160)+"...";
                        _logger.LogWarning("[{User}] Invalid {Command}: {Field}, value={Value}",player.Data.PSNUserId,gRequest.Command,ex.Message,actual);
                        return BadRequest(GrimResult.FromInt(0));
                    }
                case "bspec.beginentry":
                case "bspec.endentry":
                    if (!gRequest.TryGetParameterLongByKey("driver_id", out long entryDriverId) || entryDriverId <= 0)
                        return BadRequest(GrimResult.FromInt(0));
                    bool accepted = gRequest.Command == "bspec.beginentry"
                        ? await _bspecDriverDB.BeginEntryAsync(entryDriverId, player.Data.Id)
                        : await _bspecDriverDB.EndEntryAsync(entryDriverId, player.Data.Id);
                    return Ok(GrimResult.FromInt(accepted ? 1 : 0));
                case "bspec.carlist":
                    return await OnRequestCarList(player, gRequest);
                case "bspec.driverlist":
                    return await OnRequestDriverList(player, gRequest);
                case "bspec.driver":
                    return await OnRequestDriver(player, gRequest);
                case "bspec.popdriver":
                    return await OnRequestPopDriver(player, gRequest);
                case "bspec.pushcar":
                    return await OnRequestPushCar(player, gRequest);
                case "bspec.pushdriver":
                    return await OnRequestPushDriver(player, gRequest);


                case "bspec.popcar":
                    return await OnRequestPopCar(player, gRequest);
                case "bspec.clear":
                    return await OnRequestClear(player, gRequest);
                case "bspec.resultsummarylist":
                    return await OnRequestSummaryList(player, gRequest);
            }

            _logger.LogDebug("Received unimplemented bspec call: {command}", gRequest.Command);
            var res = GrimResult.FromInt(-1);
            return BadRequest(res);
        }

        [HttpGet("/bspec/result/{id:long}.xml")]
        [HttpGet("/ap/bspec/result/{id:long}.xml")]
        public async Task<ActionResult> RaceResult(long id)
        {
            if (Player == null && !await new SessionRecover(_userDB,_players).CheckIfPlayerExist(Request.Cookies["X-gt-token"])) return Unauthorized();
            if (Player?.Token == null || Player.Token.ExpiryDate <= DateTime.Now) return Unauthorized();
            if (Player.Data.LastGame != "GT5") return BadRequest();
            if (_races == null) return StatusCode(503);
            string xml = await _races.Result(id,Player.Data.Id);
            return xml == null ? NotFound() : Content(xml,"application/xml");
        }

        private async Task<ActionResult> OnRequestCarList(Player player, GrimRequest request)
        {
            if (!request.TryGetParameterByKey("user_id", out var bspecuseridParam))
            {
                _logger.LogDebug("Received wrong bspec data: user_id");
                return BadRequest();
            }

            string userId = bspecuseridParam.Text;

            var targetId = await _userDB.GetInternalIdOfUserAsync(userId);
            if (targetId is null)
                return NotFound();

            var cars = await _bspecCarDB.GetAllCarsOfUser(targetId.Value);

            var list = new BSpecCarList();
            list.Cars = new List<BSpecCar>();

            foreach (var car in cars)
            {
                var carModel = new BSpecCar();
                carModel.UserId = userId;
                carModel.CarId = car.Id;
                carModel.CarLabel = car.CarLabel;
                carModel.CarParameter = car.CarParameter;
                carModel.CarThumbnail = car.CarThumbnail;
                carModel.LockUserId = userId;
                carModel.CreateTime = car.CreateTime;
                list.Cars.Add(carModel);
            }
            return Ok(list);
        }

        private async Task<ActionResult> OnRequestPopCar(Player player, GrimRequest request)
        {
            if (player is null)
            {
                _logger.LogWarning("Could not get current player. IP: {RemoteIpAddress}", Request.HttpContext.Connection.RemoteIpAddress.ToString());
                return Unauthorized();
            }

            if (!request.TryGetParameterByKey("car_id", out var bspeccaridParam))
            {
                _logger.LogDebug("Received wrong bspec data: car_id");
                return BadRequest();
            }
            try
            {
                string carId = bspeccaridParam.Text;
                long.TryParse(carId, out long carIdValue);
                await _bspecCarDB.RemoveCarAsync(Player.Data.Id, carIdValue);

                var result = GrimResult.FromLong(1);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing bspec.popcar request for player {playerId}", player.Data.Id);
                var result = GrimResult.FromLong(0);
                return Ok(result);
            }

            /*
            if (!System.IO.File.Exists($"{_gsOptions.XmlResourcePath}/photo/thumbnail/1.png"))
                return NotFound();

            FileStream fs = new FileStream($"{_gsOptions.XmlResourcePath}/photo/thumbnail/1.png", FileMode.Open, FileAccess.Read);
            return File(fs, "image/png"); */

            /*string carId = bspeccaridParam.Text;

            string UserId = player.Data.PSNUserId;

            long.TryParse(carId, out long carIdValue);

            var cars = await _bspecCarDB.GetAllCarsOfId(carIdValue);

            var list = new BSpecCarList();
            list.Cars = new List<BSpecCar>();

            foreach (var car in cars)
            {
                var carModel = new BSpecCar();
                carModel.UserId = UserId;
                carModel.CarId = car.Id;
                carModel.CarLabel = car.CarLabel;
                carModel.CarParameter = car.CarParameter;
                carModel.LockUserId = UserId;
                carModel.CarThumbnail = car.CarThumbnail;
                carModel.CreateTime = car.CreateTime;
                list.Cars.Add(carModel);
            }

            _logger.LogDebug("[{name}] Log: BSpec car list request", UserId);

            return Ok(list);*/
        }

        private async Task<ActionResult> OnRequestDriverList(Player player, GrimRequest request)
        {
            if (!request.TryGetParameterByKey("user_id", out var bspecuseridParam))
            {
                _logger.LogDebug("Received wrong bspec data: user_id");
                return BadRequest();
            }

            string userId = bspecuseridParam.Text;

            var targetId = await _userDB.GetInternalIdOfUserAsync(userId);
            if (targetId is null)
                return NotFound();

            var drivers = await _bspecDriverDB.GetAllDriversOfUser(targetId.Value);

            var list = new BSpecDriverList();
            list.Drivers = new List<BSpecDriver>();

            foreach (var driver in drivers)
            {
                var driverModel = new BSpecDriver();
                driverModel.UserId = userId;
                driverModel.DriverId = driver.Id;
                driverModel.DriverName = driver.DriverName;
                driverModel.DriverLevel = driver.DriverLevel;
                driverModel.DriverHelmet = driver.DriverHelmet;
                driverModel.DriverWear = driver.DriverWear;
                driverModel.HelmetColor = driver.HelmetColor;
                driverModel.WearColor = driver.WearColor;
                driverModel.Experimental = driver.Experimental;
                driverModel.DriverSpeed = driver.DriverSpeed;
                driverModel.DriverPhysical = driver.DriverPhysical;
                driverModel.DriverMental = driver.DriverMental;
                driverModel.DriverHeart = driver.DriverHeart;
                driverModel.DriverCareer = driver.DriverCareer;
                driverModel.WinCount = driver.WinCount;
                driverModel.RaceCount = driver.RaceCount;
                driverModel.Condition = driver.Condition;
                driverModel.OnlineCredit = driver.OnlineCredit;
                driverModel.OnlineExperience = driver.OnlineExperience;
                driverModel.OnlineWinCount = driver.OnlineWinCount;
                driverModel.OnlineRaceCount = driver.OnlineRaceCount;
                driverModel.DriverParameter = driver.DriverParameter;
                driverModel.LockUserId = driver.EntryLockUser ?? string.Empty;
                driverModel.Status = driver.EntryLocked ? 0 : 1;
                driverModel.CreateTime = driver.CreateTime;
                list.Drivers.Add(driverModel);
            }
            return Ok(list);
        }
        private async Task<ActionResult> OnRequestDriver(Player player, GrimRequest request)
        {
            if (!request.TryGetParameterByKey("driver_id", out var bspecdriverIdParam))
            {
                _logger.LogDebug("Received wrong bspec data: driver_id");
                return BadRequest();
            }

            if (!long.TryParse(bspecdriverIdParam.Text, out long DriverId) || DriverId <= 0)
                return BadRequest();

            var drivers = await _bspecDriverDB.GetAllDriversOfId(DriverId);

            var list = new BSpecDriverList();
            list.Drivers = new List<BSpecDriver>();

            foreach (var driver in drivers)
            {
                var driverModel = new BSpecDriver();
                var owner = await _userDB.GetByIDAsync(driver.UserId);
                if (owner == null) return NotFound();
                driverModel.UserId = owner.PSNUserId;
                driverModel.DriverId = driver.Id;
                driverModel.DriverName = driver.DriverName;
                driverModel.DriverLevel = driver.DriverLevel;
                driverModel.DriverHelmet = driver.DriverHelmet;
                driverModel.DriverWear = driver.DriverWear;
                driverModel.HelmetColor = driver.HelmetColor;
                driverModel.WearColor = driver.WearColor;
                driverModel.Experimental = driver.Experimental;
                driverModel.DriverSpeed = driver.DriverSpeed;
                driverModel.DriverPhysical = driver.DriverPhysical;
                driverModel.DriverMental = driver.DriverMental;
                driverModel.DriverHeart = driver.DriverHeart;
                driverModel.DriverCareer = driver.DriverCareer;
                driverModel.WinCount = driver.WinCount;
                driverModel.RaceCount = driver.RaceCount;
                driverModel.Condition = driver.Condition;
                driverModel.OnlineCredit = driver.OnlineCredit;
                driverModel.OnlineExperience = driver.OnlineExperience;
                driverModel.OnlineWinCount = driver.OnlineWinCount;
                driverModel.OnlineRaceCount = driver.OnlineRaceCount;
                driverModel.DriverParameter = driver.DriverParameter;
                driverModel.LockUserId = driver.EntryLockUser ?? string.Empty;
                driverModel.Status = driver.EntryLocked ? 0 : 1;
                driverModel.CreateTime = driver.CreateTime;
                list.Drivers.Add(driverModel);
            }
            return Ok(list);
        }

        private async Task<ActionResult> OnRequestPopDriver(Player player, GrimRequest request)
        {
            if (!request.TryGetParameterByKey("driver_id", out var bspecdriverIdParam))
            {
                _logger.LogDebug("Received wrong bspec data: driver_id");
                return BadRequest();
            }

            if (!long.TryParse(bspecdriverIdParam.Text, out long DriverId) || DriverId <= 0)
                return BadRequest();
            var existing = await _bspecDriverDB.GetByIDAsync(DriverId);
            if (existing != null && existing.UserId != player.Data.Id) return Forbid();

            try
            {
                bool removed = await _bspecDriverDB.RemoveDriverAsync(player.Data.Id, DriverId);
                if (!removed && await _bspecDriverDB.GetByIDAsync(DriverId) != null)
                    return Ok(GrimResult.FromLong(0));

                var result = GrimResult.FromLong(1);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while processing bspec.popdriver request for player {playerId}", player.Data.Id);
                var result = GrimResult.FromLong(0);
                return Ok(result);
            }

            /*var drivers = await _bspecDriverDB.GetAllDriversOfId(DriverId);

            var list = new BSpecDriverList();
            list.Drivers = new List<BSpecDriver>();

            foreach (var driver in drivers)
            {
                var driverModel = new BSpecDriver();
                driverModel.UserId = driver.UserId;
                driverModel.DriverId = driver.Id;
                driverModel.DriverName = driver.DriverName;
                driverModel.DriverLevel = driver.DriverLevel;
                driverModel.DriverHelmet = driver.DriverHelmet;
                driverModel.DriverWear = driver.DriverWear;
                driverModel.HelmetColor = driver.HelmetColor;
                driverModel.WearColor = driver.WearColor;
                driverModel.Experimental = driver.Experimental;
                driverModel.DriverSpeed = driver.DriverSpeed;
                driverModel.DriverPhysical = driver.DriverPhysical;
                driverModel.DriverMental = driver.DriverMental;
                driverModel.DriverHeart = driver.DriverHeart;
                driverModel.DriverCareer = driver.DriverCareer;
                driverModel.WinCount = driver.WinCount;
                driverModel.RaceCount = driver.RaceCount;
                driverModel.Condition = driver.Condition;
                driverModel.DriverParameter = driver.DriverParameter;
                driverModel.CreateTime = driver.CreateTime;
                list.Drivers.Add(driverModel);
            }
            return Ok(list);*/
        }

        [HttpPost]
        [Route("/ap/bspec/car_thumbnail_upload/")]
        public async Task<ActionResult> OnRequestThumbnailUploadCar()
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

            GrimRequest gRequest = GameRequestPayload.ReadXml(xml);
            if (gRequest is null)
                return BadRequest();

            if (gRequest.Command != "car.thumbnail.upload")
                return BadRequest();

            if (!gRequest.TryGetParameterLongByKey("car_id", out long carId))
            {
                _logger.LogWarning($"Got car.thumbnail.upload request with missing 'car_id' parameter");
                return BadRequest();
            }

            using var ms = Grim.Program.StreamManager.GetStream();
            await Request.Body.CopyToAsync(ms);
            ms.Position = 0;

            PhotoDTO photo = new PhotoDTO(Player.Data.Id, Player.Data.PSNUserId, "gt5car", "gt5car", "gt5car", "6", DateTime.Now);
            long thumbnailid = await _photoDb.AddAsync(photo);

            Directory.CreateDirectory($"{_gsOptions.XmlResourcePath}/photo/image");

            using (var fs = new FileStream($"{_gsOptions.XmlResourcePath}/photo/image/{thumbnailid}_0.png", FileMode.Create))
                await ms.CopyToAsync(fs);

            await _bspecCarDB.UpdateCarData(carId, thumbnailid);


            _logger.LogDebug("[{name}] Log: BSpec car_thumbnail_upload request", Player.Data.PSNUserId);

            var result = GrimResult.FromLong(1);
            return Ok(result);
        }

        private async Task<ActionResult> OnRequestPushCar(Player player, GrimRequest request)
        {
            if (!request.TryGetParameterByKey("car_label", out var bspeccarlabelParam))
            {
                _logger.LogDebug("Received wrong bspec data: car_label");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("cp", out var bspeccpParam))
            {
                _logger.LogDebug("Received wrong bspec data: cp");
                return BadRequest();
            }

            string carPhoto = "1";

            BSpecCarDTO car = new BSpecCarDTO(Player.Data.Id, bspeccarlabelParam.Text, bspeccpParam.Text, carPhoto, DateTime.Now);
            long newId = await _bspecCarDB.AddAsync(car);
            if (newId <= 0)
            {
                _logger.LogWarning("[{name}] BSpec car limit reached (6)", player.Data.PSNUserId);
                return BadRequest(GrimResult.FromInt(-1));
            }

            _logger.LogDebug("[{name}] Log: BSpec car push request", player.Data.PSNUserId);

            var result = GrimResult.FromLong(newId);
            return Ok(result);
        }
        private async Task<ActionResult> OnRequestClear(Player player, GrimRequest request)
        {
            await _bspecDriverDB.ClearEntriesAsync(player.Data.Id);
            var result = GrimResult.FromLong(1);
            return Ok(result);
        }
        private async Task<ActionResult> OnRequestSummaryList(Player player, GrimRequest request)
        {
            if (!request.TryGetParameterByKey("user_id", out var bspecuserIdParam))
            {
                _logger.LogDebug("Received wrong bspec data: user_id");
                return BadRequest();
            }
            if (_races == null) return StatusCode(503);
            var user = await _userDB.GetInternalIdOfUserAsync(bspecuserIdParam.Text);
            if (!user.HasValue) return NotFound();
            if (!await _races.CanReadHistory(player.Data.Id,user.Value)) return Forbid();
            return Content(await _races.Summaries(user.Value), "application/xml");
        }
        private async Task<ActionResult> OnRequestPushDriver(Player player, GrimRequest request)
        {
            if (!request.TryGetParameterByKey("name", out var bspecnameParam))
            {
                _logger.LogDebug("Received wrong bspec data: name");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("level", out var bspeclevelParam))
            {
                _logger.LogDebug("Received wrong bspec data: level");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("helmet", out var bspechelmetParam))
            {
                _logger.LogDebug("Received wrong bspec data: helmet");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("wear", out var bspecwearParam))
            {
                _logger.LogDebug("Received wrong bspec data: wear");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("helmet_color", out var bspechelmetcolorParam))
            {
                _logger.LogDebug("Received wrong bspec data: helmet_color");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("wear_color", out var bspecwearcolorParam))
            {
                _logger.LogDebug("Received wrong bspec data: wear_color");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("experimental", out var bspecexperimentalParam))
            {
                _logger.LogDebug("Received wrong bspec data: experimental");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("speed", out var bspecspeedParam))
            {
                _logger.LogDebug("Received wrong bspec data: speed");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("physical", out var bspecphysicalParam))
            {
                _logger.LogDebug("Received wrong bspec data: physical");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("mental", out var bspecmentalParam))
            {
                _logger.LogDebug("Received wrong bspec data: mental");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("heart", out var bspecheartParam))
            {
                _logger.LogDebug("Received wrong bspec data: heart");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("career", out var bspeccareerParam))
            {
                _logger.LogDebug("Received wrong bspec data: career");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("win_count", out var bspecwincountParam))
            {
                _logger.LogDebug("Received wrong bspec data: win_count");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("race_count", out var bspecracecountParam))
            {
                _logger.LogDebug("Received wrong bspec data: race_count");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("condition", out var bspecconditionParam))
            {
                _logger.LogDebug("Received wrong bspec data: condition");
                return BadRequest();
            }

            if (!request.TryGetParameterByKey("dp", out var bspecdpParam))
            {
                _logger.LogDebug("Received wrong bspec data: dp");
                return BadRequest();
            }

            BSpecDriverDTO driver = new BSpecDriverDTO(Player.Data.Id, bspecnameParam.Text, bspeclevelParam.Text,
                bspechelmetParam.Text, bspecwearParam.Text, bspechelmetcolorParam.Text, bspecwearcolorParam.Text, bspecexperimentalParam.Text,
                bspecspeedParam.Text, bspecphysicalParam.Text, bspecmentalParam.Text, bspecheartParam.Text, bspeccareerParam.Text, bspecwincountParam.Text,
                bspecracecountParam.Text, bspecconditionParam.Text, bspecdpParam.Text, DateTime.Now);


            long driverId = await _bspecDriverDB.AddAsync(driver);
            if (driverId <= 0)
            {
                _logger.LogWarning("[{name}] BSpec driver limit reached (6)", player.Data.PSNUserId);
                return BadRequest(GrimResult.FromInt(-1));
            }

            _logger.LogInformation("[{name}] BSpec driver published: driverId={driverId}", player.Data.PSNUserId, driverId);

            var result = GrimResult.FromLong(driverId);
            return Ok(result);
        }
    }
}
