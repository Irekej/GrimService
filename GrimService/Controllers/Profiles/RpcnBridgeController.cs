using System;
using System.IO;
using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using GrimService.Database.Controllers;
using GrimService.Models;
using GrimService.Models.Xml;
using GrimService.Services;
using GrimService.Services.Rpcn;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace GrimService.Controllers.Profiles
{
    [ApiController, AllowAnonymous, Route("ap/rpcn"), EnableRateLimiting("rpcn-bridge")]
    public sealed class RpcnBridgeController : ControllerBase
    {
        private readonly RpcnBridge bridge;
        private readonly UserDBManager users;
        private readonly PlayerManager players;
        private readonly IConfiguration config;
        private readonly ILogger<RpcnBridgeController> logger;
        public RpcnBridgeController(RpcnBridge bridge, UserDBManager users, PlayerManager players,
            IConfiguration config, ILogger<RpcnBridgeController> logger)
        { this.bridge = bridge; this.users = users; this.players = players; this.config = config; this.logger = logger; }

        private bool Ready()
        {
            Response.Headers.CacheControl = "no-store";
            if (!bridge.Settings.Enabled) { Response.StatusCode = 404; return false; }
            if (!Request.IsHttps) { Response.StatusCode = 426; return false; }
            return true;
        }

        private GrimService.Config.RpcnBridgeDevice Device()
        {
            var header = Request.Headers["X-Grim-Device-Key"];
            return header.Count == 1 ? bridge.Authenticate(header[0]) : null;
        }

        [HttpPost("bootstrap")]
        public IActionResult Bootstrap()
        {
            if (!Ready()) return new EmptyResult();
            var device = Device();
            if (device == null) return Unauthorized();
            return Ok(new {
                version = 1, game = "GT5", username = device.Username,
                loginPath = "/ap/rpcn/login", tunnelPath = "/ap/rpcn/tunnel",
                upstreamHost = bridge.Settings.Host, upstreamPort = bridge.Settings.Port,
                rpcnProtocol = bridge.Settings.ProtocolVersion, signalingPort = 3657,
                requiresPs3NpShim = true, crossplayVerified = false
            });
        }


        [HttpPost("login"), Produces("application/xml")]
        public async Task<IActionResult> Login()
        {
            if (!Ready()) return new EmptyResult();
            var device = Device();
            if (device == null) return Unauthorized();
            if (!bridge.TryAcquire(device)) return Conflict();
            try
            {
                var user = await users.GetByPSNUserIdAsync(device.Username);
                if (user == null) return Conflict(GrimResult.FromInt(-1));
                if (!AuthIdentityPolicy.Allows(user.AuthProvider, user.Platform, user.RpcnUserId, "private", null))
                    return StatusCode(403);
                if (await Banned(user.BanStatus, user.PSNUserId)) return StatusCode(403);
                var now = DateTime.UtcNow;
                var expiry = now.AddMinutes(Math.Clamp(bridge.Settings.SessionMinutes, 1, 240));
                var credentials = new SigningCredentials(
                    GrimService.Infrastructure.Security.GameSigningKey.Read(config, logger), SecurityAlgorithms.HmacSha256);
                var jwt = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"],
                    new[] { new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")) },
                    expires: expiry, signingCredentials: credentials);
                var token = new SessionToken(new JwtSecurityTokenHandler().WriteToken(jwt), expiry);
                user.LastGame = "GT5";
                await users.UpdateGame(user);
                await users.UpdatePlayerTicket(token.Token, expiry, user.Id);
                foreach (var previous in players.Players.Values.Where(p => p.Data.Id == user.Id).ToArray())
                { players.RemoveByToken(previous.Token); players.RemoveByTokenTemp(previous.Token); }
                var player = new Player(user, token) { LastUpdate = DateTime.Now };
                players.AddUser(player); players.AddUserTemp(player);

                Response.Cookies.Append("X-gt-token", token.Token, new CookieOptions {
                    Expires = expiry, HttpOnly = true, Path = "/"
                });
                logger.LogInformation("RPCN bridge Grim login: device={Device}, user={User}", device.Id, device.Username);
                return Ok(new TicketResult { Result = "1", UserId = user.PSNUserId,
                    Nickname = user.Nickname ?? user.PSNUserId, UserNumber = user.Id, ServerTime = now });
            }
            finally { bridge.Release(device); }
        }

        private static async Task<bool> Banned(string status, string username)
        {
            if (status == "1") return true;
            const string path = "Resources/banned_people.txt";
            return System.IO.File.Exists(path) && (await System.IO.File.ReadAllTextAsync(path))
                .Split(',').Any(s => s.Trim() == username);
        }

        [HttpGet("tunnel")]
        public async Task Tunnel()
        {
            if (!Ready()) return;
            var device = Device();
            if (device == null) { Response.StatusCode = 401; return; }
            if (!HttpContext.WebSockets.IsWebSocketRequest) { Response.StatusCode = 400; return; }
            var user = await users.GetByPSNUserIdAsync(device.Username);
            if (user == null || await Banned(user.BanStatus, user.PSNUserId)) { Response.StatusCode = 403; return; }
            if (!bridge.TryAcquire(device)) { Response.StatusCode = 409; return; }
            try
            {
                var upstream = await bridge.ConnectAsync(HttpContext.RequestAborted);
                using var tcp = upstream.Client;
                using var tls = upstream.Stream;
                using var socket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                logger.LogInformation("RPCN tunnel opened: device={Device}, user={User}, protocol={Protocol}",
                    device.Id, device.Username, bridge.Settings.ProtocolVersion);
                try { await bridge.RelayAsync(socket, tls, upstream.Greeting, device, HttpContext.RequestAborted); }
                finally { socket.Abort(); }
            }
            catch (Exception ex)
            {

                logger.LogWarning("RPCN tunnel stopped: device={Device}, failure={Failure}", device.Id, ex.GetType().Name);
                if (!Response.HasStarted) Response.StatusCode = 502;
            }
            finally { bridge.Release(device); }
        }
    }
}
