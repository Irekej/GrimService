using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

using System;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Security.Claims;

using GrimService.Results;
using GrimService.Services;
using GrimService.Database.Controllers;
using GrimService.Database.Tables;
using GrimService.Filters;
using GrimService.Config;
using GrimService.Models;
using GrimService.Models.Xml;
using GrimService.Sony;

using System.Linq;
using System.IO;
using System.Net.Sockets;
using System.Xml;
using System.Net;
using System.Net.Http;
using System.Net.Mail;

namespace GrimService.Controllers.Profiles
{
    [ApiController]
    [PDIClient]
    [Route("/ap/ticket/login/{pfsVersion}")]
    [Produces("application/xml")]
    public class AuthenticationController : ControllerBase
    {
        private readonly ILogger<AuthenticationController> _logger;
        private readonly IConfiguration _config;
        private readonly PlayerManager _players;
        private readonly GameServerOptions _gsOptions;

        private readonly UserDBManager _users;
        private readonly GrimService.Services.Rpcn.RpcnBridge _rpcnBridge;
        private readonly GrimService.Services.Rpcn.RpcnCustomTicket _customTickets;
        private readonly GrimService.Services.Rpcn.RpcnTicketVerifier _rpcnTickets;

        public AuthenticationController(IConfiguration config, 
            ILogger<AuthenticationController> logger,
            PlayerManager players,
            UserDBManager users,
            GrimService.Services.Rpcn.RpcnBridge rpcnBridge = null,
            GrimService.Services.Rpcn.RpcnCustomTicket customTickets = null,
            GrimService.Services.Rpcn.RpcnTicketVerifier rpcnTickets = null)
        {
            _logger = logger;
            _config = config;
            _players = players;
            _gsOptions = _config.GetSection(GameServerOptions.GameServer).Get<GameServerOptions>();
            _users = users;
            _rpcnBridge = rpcnBridge;
            _customTickets = customTickets;
            _rpcnTickets = rpcnTickets;
        }

        [HttpPost]
        public async Task<ActionResult> Post(ulong pfsVersion, [FromQuery(Name = "r")] string GameServerRegion, [FromQuery(Name = "m")] string PlayerMacAddress = null)
        {
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var result = await LoginCore(pfsVersion, GameServerRegion, PlayerMacAddress);
                _logger.LogDebug("Game login completed: pfs={Pfs}, http={Status}, elapsedMs={ElapsedMs}",
                    pfsVersion, (result as IStatusCodeActionResult)?.StatusCode ?? 200, elapsed.ElapsedMilliseconds);
                return result;
            }
            catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
            {
                _logger.LogWarning("Game login cancelled by client: pfs={Pfs}, elapsedMs={ElapsedMs}", pfsVersion, elapsed.ElapsedMilliseconds);
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Game login failed: pfs={Pfs}, elapsedMs={ElapsedMs}", pfsVersion, elapsed.ElapsedMilliseconds);
                throw;
            }
        }

        private async Task<ActionResult> LoginCore(ulong pfsVersion, string GameServerRegion, string PlayerMacAddress)
        {
            PFSType pfsType = (PFSType)pfsVersion;
            _logger.LogDebug("Login request from IP: {RemoteIpAddress} with PFS: {pfsType} ({pfsNum})", Request.HttpContext.Connection.RemoteIpAddress.ToString(), pfsType, (long)pfsType);
            if (Request.ContentLength >= 0x300)
            {
                _logger.LogWarning("Received a ticket too big - {size} from {RemoteIpAddress}", Request.ContentLength, Request.HttpContext.Connection.RemoteIpAddress.ToString());
                return BadRequest();
            }
            string hexNpTicket = "";
            NPTicket ticket;
            bool verifiedRpcn = false;
            bool pairedPrivate = false;
            string rpcnClientPlatform = "RPCS3";
            try
            {
                using var body = new MemoryStream();
                var chunk = new byte[0x300];
                int read;
                while ((read = await Request.Body.ReadAsync(chunk.AsMemory(), HttpContext.RequestAborted)) > 0)
                {
                    if (body.Length + read >= 0x300) return BadRequest();
                    body.Write(chunk, 0, read);
                }
                byte[] buf = body.ToArray();
                if (GrimService.Services.Rpcn.RpcnCustomTicket.Matches(buf))
                {
                    if (PlayerMacAddress != null || _rpcnBridge == null || _customTickets == null ||
                        !_customTickets.TryConsume(buf, _rpcnBridge.Settings, DateTimeOffset.UtcNow, out var device))
                    {
                        _logger.LogWarning("Rejected private GT5 device ticket");
                        return Unauthorized();
                    }
                    ticket = new NPTicket { OnlineId = device.Username, Region = "PL", Domain = "RPCN",
                        Language = NPLanguage.Polish, ConsoleID = new byte[16] };
                    pairedPrivate = true;
                    _logger.LogInformation("Private GT5 device ticket accepted: user={User}, device={Device}", device.Username, device.Id);
                }



                else if (GrimService.Services.Rpcn.RpcnTicketVerifier.IsRpcn(buf) ||
                    GrimService.Services.Rpcn.RpcnTicketVerifier.IsEnvelope(buf)) {
                    string reason = "verifier-unavailable";
                    string signedServiceId = null;
                    if (_rpcnTickets == null || !_rpcnTickets.TryConsumeWithService(buf, _rpcnBridge?.Settings ?? new GrimService.Config.RpcnBridgeOptions(),
                        DateTimeOffset.UtcNow, out ticket, out rpcnClientPlatform, out reason, out signedServiceId)) {
                        _logger.LogWarning("Rejected game RPCN ticket: pfs={Pfs}, reason={Reason}, serviceId={ServiceId}", pfsVersion, reason, signedServiceId ?? "<unverified>");
                        return Unauthorized();
                    }
                    verifiedRpcn = true;
                    _logger.LogInformation("Signed RPCN game ticket accepted: pfs={Pfs}, user={User}, platform={Platform}, serviceId={ServiceId}",
                        pfsVersion, ticket.OnlineId, rpcnClientPlatform, signedServiceId);
                }
                else ticket = NPTicket.FromBuffer(buf);
                hexNpTicket = BitConverter.ToString(buf).Replace("-", "");
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Could not read NP ticket provided by client");
                return BadRequest();
            }
            if (!VerifyTicket(ticket))
                return BadRequest();

            string hexToFind1 = "5053335F43";
            string hexToFind2 = "5250434E";
            bool isRPCN = verifiedRpcn;
            string consoleIdBase64 = "";
            string platformName = verifiedRpcn ? rpcnClientPlatform : pairedPrivate ? "PS3" : "";


            var now = DateTime.UtcNow;
            if (hexNpTicket.Contains(hexToFind2) && hexNpTicket.StartsWith("21010000"))
            {
                platformName = verifiedRpcn ? rpcnClientPlatform : "RPCS3";
                isRPCN = true;
                var userData14241 = await _users.GetByPSNUserIdAsync(ticket.OnlineId);
                if (userData14241 is null)
                {

                }
                if (userData14241 is not null)
                {
                    if (userData14241.Platform != "ps3" && userData14241.Platform == "RPCS3")
                    {

                    }
                    else
                    {

                    }
                }
            }
            if (hexNpTicket.Contains(hexToFind1) && hexNpTicket.StartsWith("4100000000"))
            {
                platformName = "PS3";
                isRPCN = false;
                consoleIdBase64 = Convert.ToBase64String(ticket.ConsoleID);
                var userData14241 = await _users.GetByPSNUserIdAsync(ticket.OnlineId);
                if (userData14241 is null)
                {

                }
                if (userData14241 is not null)
                {
                    if (userData14241.Platform == "ps3" && userData14241.Platform != "RPCS3")
                    {

                    }
                    else
                    {

                    }
                }
            }


            if (ticket.ConsoleID == null && !isRPCN && PlayerMacAddress is not null)
            {
                _logger.LogError($"[:] PS3: Invalid ConsoleID for {ticket.OnlineId}.");
                return NotFound();
            }






            bool bindTicketIdentity = verifiedRpcn || PlayerMacAddress == null;
            string authProvider = verifiedRpcn ? "rpcn" : PlayerMacAddress == null ? (pairedPrivate ? "private" : "psn") : null;
            long? rpcnSubject = verifiedRpcn ? (long?)ticket.UserId : null;
            UserDTO user = await _users.GetByPSNUserIdAsync(ticket.OnlineId);
            if (bindTicketIdentity && user != null && !GrimService.Services.Rpcn.AuthIdentityPolicy.Allows(
                user.AuthProvider, user.Platform, user.RpcnUserId, authProvider, rpcnSubject)) {
                _logger.LogWarning("Game auth identity collision: user={User}, provider={Provider}", ticket.OnlineId, authProvider);

            }
            user ??= await CreateUser(ticket, PlayerMacAddress, authProvider, rpcnSubject, platformName);

            if (user is null)
            {
                _logger.LogError("Failed to get or create user from db: Name: {name}", ticket.OnlineId);
                return Problem();    
            }
            if (bindTicketIdentity && !await _users.BindAuthIdentityAsync(user.Id, authProvider, rpcnSubject)) return StatusCode(403);
            if (bindTicketIdentity) { user.AuthProvider = authProvider; user.RpcnUserId = rpcnSubject; }


            _logger.LogInformation("Checking if this player is banned: {OnlineId}", ticket.OnlineId);
            if (IsPlayerBanned(ticket.OnlineId))
            {
                _logger.LogInformation("Player is banned!: {OnlineId}", ticket.OnlineId);
                return new BanResult();
            }
            _logger.LogInformation("Player is not banned: {OnlineId}", ticket.OnlineId);
            if (user.BanStatus == "1")
            {
                _logger.LogInformation("Player is banned!: {OnlineId}", ticket.OnlineId);
                return new BanResult();
            }
            if (!pairedPrivate && await _users.UpdateCountryFromTicketAsync(user, ticket.Region))
                _logger.LogInformation("Player country updated from login ticket: user={User}, country={Country}", user.PSNUserId, user.Country);
            CheckAlreadyConnected(ticket.OnlineId);
            var resp = new TicketResult()
            {
                Result = "1",
                Nickname = ticket.OnlineId,
                UserId = ticket.OnlineId,
                UserNumber = user.Id,
                ServerTime = now,
            };
            var expiryTime = now.AddDays(12);
            var sToken = new SessionToken(GenerateToken(expiryTime), expiryTime);
            var cookieOptions = new CookieOptions() { Expires = sToken.ExpiryDate, Path = "/" };
            Response.Cookies.Append("X-gt-token", sToken.Token, cookieOptions);
            var player = new Player(user, sToken);
            await _users.UpdatePlayerTicket(sToken.Token, sToken.ExpiryDate, player.Data.Id);
            _logger.LogDebug("Game login stage: user={User}, stage=ticket-persisted", ticket.OnlineId);
            player.LastUpdate = now;
            _players.AddUser(player);
            _players.AddUserTemp(player);
            var data2 = player.Data;
            if (PlayerMacAddress is null)
            {
                if (!verifiedRpcn && !pairedPrivate) platformName = "PS3";
                string LastGame = "GT5";
                data2.LastGame = LastGame;
                data2.Id = player.Data.Id;
                await _users.UpdateGame(data2);
                await _users.UpdatePlatform(platformName, user.Id);
                user.Platform = platformName;
            }
            else
            {
                string LastGame = "GT6";
                data2.LastGame = LastGame;
                data2.Id = player.Data.Id;
                await _users.UpdateGame(data2);
            }
            var data = player.Data;
            data.Id = player.Data.Id;
            data.IPAddress = Request.HttpContext.Connection.RemoteIpAddress.ToString();
            if (PlayerMacAddress != null)
            {
                data.MacAddress = PlayerMacAddress;
            }
            var Loggins = await _users.GetByPSNUserIdAsync(player.Data.PSNUserId);
            if (Loggins == null)
            {
                long timesoflogin = 0;
                long finaltimes = timesoflogin + 1;
                await _users.UpdateLoggins(finaltimes, player.Data.PSNUserId);
            }
            if (Loggins != null)
            {
                long timesoflogin = player.Data.NumbersOfLogin;
                long finaltimes = timesoflogin + 1;
                await _users.UpdateLoggins(finaltimes, player.Data.PSNUserId);
            }
            await _users.UpdateLastActivity(DateTime.Now, player.Data.PSNUserId);
            await _users.UpdateIpAndMac(data);
            _logger.LogDebug("Game login stage: user={User}, stage=account-updated", ticket.OnlineId);
            using var notificationBudget = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(HttpContext.RequestAborted);
            notificationBudget.CancelAfter(TimeSpan.FromSeconds(2));
            if (data.MacAddress != null)
            {
                var usersFromDB = await _users.GetByMACList(data.MacAddress);
                if (usersFromDB != null && player.Data.PSNUserId != "IrekejO")
                {
                    foreach (var userper in usersFromDB)
                    {
                        string usersDiscord2 = await _users.GetPlayersDiscordID(user.PSNUserId);
                        string apiUrl2 = _config["Discord:MultiAccountWebhookUrl"];
                        if (userper.DiscordId is not null && userper.PSNUserId != user.PSNUserId)
                        {
                            _logger.LogInformation($"[:] Player {user.PSNUserId} has multiaccount: {userper.PSNUserId}");
                            var data234 = new
                            {
                                content = $"Player ``{user.PSNUserId}`` ( <@{usersDiscord2}> ) has multiaccount: ``{userper.PSNUserId}`` BanStatus:{userper.BanStatus} ( <@{userper.DiscordId}> )"
                            };
                            string jsonData2 = Newtonsoft.Json.JsonConvert.SerializeObject(data234);
                            await GrimService.Infrastructure.Security.GameLoginNotification.SendAsync(apiUrl2, jsonData2, notificationBudget.Token, _logger);
                        }
                        else if (userper.PSNUserId != user.PSNUserId)
                        {
                            _logger.LogInformation($"[:] Player {user.PSNUserId} has multiaccount: {userper.PSNUserId}");
                            var data234 = new
                            {
                                content = $"Player ``{user.PSNUserId}`` ( <@{usersDiscord2}> ) has multiaccount: ``{userper.PSNUserId}`` ( No discord detected )"
                            };
                            string jsonData2 = Newtonsoft.Json.JsonConvert.SerializeObject(data234);
                            await GrimService.Infrastructure.Security.GameLoginNotification.SendAsync(apiUrl2, jsonData2, notificationBudget.Token, _logger);
                        }
                    }
                }
            }
            _logger.LogDebug("Auth Request: NP_Ticket -> PFS: {pfsVersion} | OnlineID: {OnlineId} | Region: {Region} | IP: {RemoteIpAddress} | Mac: {PlayerMacAddress} | GameMode: {GameServerRegion}", pfsVersion, ticket.OnlineId, ticket.Region, Request.HttpContext.Connection.RemoteIpAddress.ToString(), PlayerMacAddress, GameServerRegion);
            try
            {
                if (string.IsNullOrEmpty(user.DiscordId))
                {
                    string apiUrl = _config["Discord:LoginWebhookUrl"];
                    string usersDiscord = await _users.GetPlayersDiscordID(player.Data.PSNUserId);
                    var data23 = new
                    {
                        content = $"Auth Request: -> PSN: ``{player.Data.PSNUserId}`` | Platform: {platformName} | Region: :flag_{player.Data.Country}: | Game: {user.LastGame} PFS: {pfsVersion} | Discord: Not linked."
                    };
                    string jsonData = Newtonsoft.Json.JsonConvert.SerializeObject(data23);
                    await GrimService.Infrastructure.Security.GameLoginNotification.SendAsync(apiUrl, jsonData, notificationBudget.Token, _logger);
                }
                else
                {
                    string apiUrl = _config["Discord:LoginWebhookUrl"];
                    string usersDiscord = await _users.GetPlayersDiscordID(player.Data.PSNUserId);
                    var data23 = new
                    {
                        content = $"Auth Request: -> PSN: ``{player.Data.PSNUserId}`` | Platform: {platformName} | Region: :flag_{player.Data.Country}: | Game: {user.LastGame} PFS: {pfsVersion} | Discord: <@{usersDiscord}>"
                    };
                    string jsonData = Newtonsoft.Json.JsonConvert.SerializeObject(data23);
                    await GrimService.Infrastructure.Security.GameLoginNotification.SendAsync(apiUrl, jsonData, notificationBudget.Token, _logger);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Game login notification preparation failed: reason={Reason}; authentication continues", ex.GetType().Name);
            }

            if (!string.IsNullOrEmpty(consoleIdBase64) && PlayerMacAddress != null)
                await _users.UpdateCIDandPlatform(consoleIdBase64, platformName, player.Data.Id);
            if (string.IsNullOrEmpty(consoleIdBase64) && PlayerMacAddress != null)
                await _users.UpdatePlatform(platformName, player.Data.Id);

            return Ok(resp);
        }

        [NonAction]
        private bool VerifyTicket(NPTicket ticket)
        {
            if (ticket.OnlineId.Length > 32)
                return false;

            if (ticket.Region.Length > 2)
                return false;

            if (ticket.Domain.Length > 4)
                return false;

            return true;
        }

        [NonAction]
        public async Task<UserDTO> CreateUser(NPTicket ticket, string formattedMacAddress,
            string authProvider = null, long? rpcnUserId = null, string platform = null)
        {
            var user = new UserDTO()
            {
                PSNUserId = ticket.OnlineId,
                IPAddress = Request.HttpContext.Connection.RemoteIpAddress.ToString(),
                Nickname = ticket.OnlineId,
                Country = ticket.Region.ToString().TrimEnd('\0').Substring(0, 2),
                MacAddress = formattedMacAddress,
                AuthProvider = authProvider, RpcnUserId = rpcnUserId, Platform = platform,
            };

            int id;
            try
            {
                id = (int)await _users.AddAsync(user);
            }
            catch (Exception e)
            {
                _logger.LogError("Failed to create new user for PSN User: {userName}", ticket.OnlineId);
                return null;
            }

            user.Id = id;

            _logger.LogInformation("Created user: {psnUserId} (DB: {id})", ticket.OnlineId, id);
            return user;
        }

        [NonAction]
        public string GenerateToken(DateTime expiryTime)
        {
            var secKey = GrimService.Infrastructure.Security.GameSigningKey.Read(_config, _logger);
            var creds = new SigningCredentials(secKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(_config["Jwt:Issuer"], _config["Jwt:Audience"], null,
                expires: expiryTime, signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [NonAction]
        public bool CheckAlreadyConnected(string psnOnlineName)
        {
            var potentialOnlinePlayer = _players.GetPlayerByName(psnOnlineName);
            if (potentialOnlinePlayer != null)
            {
                _players.RemoveByToken(potentialOnlinePlayer.Token);
                _players.RemoveByTokenTemp(potentialOnlinePlayer.Token);
                return true;
            }
            return false;
        }
        public bool IsPlayerBanned(string psnOnlineName)
        {
            string filePath = $"Resources/banned_people.txt";
            if (System.IO.File.Exists(filePath))
            {
                string fileContent = System.IO.File.ReadAllText(filePath);
                string[] bannedPlayers = fileContent.Split(',');
                return bannedPlayers.Contains(psnOnlineName);
            }
            return false;
        }
    }
}
