using System;
using System.Linq;
using System.Threading.Tasks;
using GrimService.Database.Controllers;
using GrimService.Models.Admin;
using GrimService.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GrimService.Controllers.Admin
{
    [Authorize(AuthenticationSchemes = "AdminCookie")]
    [Route("admin")]
    public class AdminController : Controller
    {
        private readonly AdminUserDBManager _adminUsers;
        private readonly UserDBManager _users;
        private readonly CourseDBManager _courses;
        private readonly PhotoDBManager _photos;
        private readonly ReplayDBManager _replays;
        private readonly PlayerManager _players;
        private readonly ILogger<AdminController> _logger;

        public AdminController(AdminUserDBManager adminUsers, UserDBManager users, CourseDBManager courses,
            PhotoDBManager photos, ReplayDBManager replays, PlayerManager players, ILogger<AdminController> logger)
        {
            _adminUsers = adminUsers;
            _users = users;
            _courses = courses;
            _photos = photos;
            _replays = replays;
            _players = players;
            _logger = logger;
        }

        [AllowAnonymous]
        [HttpGet("login")]
        public IActionResult Login(string returnUrl = "/admin") => View(model: returnUrl);

        [AllowAnonymous]
        [HttpPost("login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password, string returnUrl = "/admin")
        {
            if (!await _adminUsers.VerifyAsync(username, password))
            {
                _logger.LogWarning("Nieudana próba logowania administratora z {Ip}", HttpContext.Connection.RemoteIpAddress);
                ModelState.AddModelError(string.Empty, "Nieprawidłowy login lub hasło.");
                return View(model: returnUrl);
            }

            var identity = new System.Security.Claims.ClaimsIdentity("AdminCookie");
            identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, username));
            var account = await _adminUsers.FindAsync(username);
            identity.AddClaim(new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, account.IsAdmin ? "Administrator" : "EventEditor"));
            await HttpContext.SignInAsync("AdminCookie", new System.Security.Claims.ClaimsPrincipal(identity));
            _logger.LogInformation("Administrator {Admin} zalogował się z {Ip}", username, HttpContext.Connection.RemoteIpAddress);
            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl : "/admin");
        }

        [HttpPost("logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("AdminCookie");
            return RedirectToAction(nameof(Login));
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string q, string game)
        {
            if (game != "GT5" && game != "GT6") game = null;
            var model = new AdminDashboardViewModel
            {
                Query = q,
                Game = game,
                Players = (await _users.SearchAsync(q, game)).ToList(),
                TotalPlayers = await _users.CountAsync(),
                BannedPlayers = await _users.CountBannedAsync(),
                ConnectedPlayers = _players.GetConnectedPlayers().Count,
                Administrators = User.IsInRole("Administrator") ? (await _adminUsers.GetAllAsync()).ToList() : new()
            };
            return View(model);
        }

        [HttpGet("players/{id:int}")]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Player(int id)
        {
            var user = await _users.GetByIDAsync(id);
            if (user == null) return NotFound();
            return View(new AdminPlayerViewModel
            {
                Player = user,
                Courses = (await _courses.GetAllForAdminAsync(id)).ToList(),
                Photos = (await _photos.GetAllForAdminAsync(id)).ToList(),
                Replays = (await _replays.GetAllForAdminAsync(id)).ToList(),
                IsConnected = _players.GetPlayerByToken(user.PlayerToken) != null,
                BanReason = await _users.GetBanReasonAsync(id)
            });
        }

        [HttpPost("players/{id:int}/ban")]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ban(int id, string reason)
        {
            await _users.SetBanAsync(id, true, reason, User.Identity?.Name);
            await DropSessionInternal(id);
            _logger.LogWarning("Administrator {Admin} zbanował gracza {PlayerId}: {Reason}", User.Identity?.Name, id, reason);
            return RedirectToAction(nameof(Player), new { id });
        }

        [HttpPost("players/{id:int}/unban")]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unban(int id)
        {
            await _users.SetBanAsync(id, false, null, User.Identity?.Name);
            _logger.LogInformation("Administrator {Admin} odbanował gracza {PlayerId}", User.Identity?.Name, id);
            return RedirectToAction(nameof(Player), new { id });
        }

        [HttpPost("players/{id:int}/drop-session")]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DropSession(int id)
        {
            await DropSessionInternal(id);
            _logger.LogInformation("Administrator {Admin} usunął sesję gracza {PlayerId}", User.Identity?.Name, id);
            return RedirectToAction(nameof(Player), new { id });
        }

        [HttpPost("players/{id:int}/edit")]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string nickname, string comment, string credit, string welcomeMessage)
        {
            await _users.UpdateAdminFieldsAsync(id, nickname, comment, credit, welcomeMessage);
            _logger.LogInformation("Administrator {Admin} zmienił dane gracza {PlayerId}", User.Identity?.Name, id);
            return RedirectToAction(nameof(Player), new { id });
        }

        [HttpPost("administrators")]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAdministrator(string username, string password, bool isAdmin = false)
        {
            try
            {
                await _adminUsers.CreateAsync(username, password, User.Identity?.Name, isAdmin);
                _logger.LogWarning("Administrator {Admin} utworzył konto administratora {NewAdmin}", User.Identity?.Name, username);
            }
            catch (ArgumentException exception)
            {
                TempData["AdminError"] = exception.Message;
            }
            catch (Exception)
            {
                TempData["AdminError"] = "Nie udało się dodać administratora. Login może być już zajęty.";
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("players/{playerId:int}/courses/{courseId:long}/delete")]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteCourse(int playerId, long courseId)
        {
            await _courses.RemoveForAdminAsync(playerId, courseId);
            return RedirectToAction(nameof(Player), new { id = playerId });
        }

        [HttpPost("players/{playerId:int}/photos/{photoId:long}/delete")]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePhoto(int playerId, long photoId)
        {
            await _photos.RemoveForAdminAsync(playerId, photoId);
            return RedirectToAction(nameof(Player), new { id = playerId });
        }

        [HttpPost("players/{playerId:int}/replays/{replayId:long}/delete")]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReplay(int playerId, long replayId)
        {
            await _replays.RemoveForAdminAsync(playerId, replayId);
            return RedirectToAction(nameof(Player), new { id = playerId });
        }

        [HttpPost("accounts/{id:long}/role")]
        [Authorize(Roles = "Administrator")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetRole(long id, bool isAdmin)
        {
            var target = (await _adminUsers.GetAllAsync()).FirstOrDefault(a => a.Id == id);
            if (target == null) return NotFound();

            if (target.Username == User.Identity.Name && !isAdmin) return BadRequest("Nie możesz odebrać sobie uprawnień.");
            await _adminUsers.SetRoleAsync(id, isAdmin);
            return RedirectToAction(nameof(Index));
        }

        private async Task DropSessionInternal(int id)
        {
            var user = await _users.GetByIDAsync(id);
            if (user == null) return;
            if (!string.IsNullOrWhiteSpace(user.PlayerToken))
            {
                _players.RemoveByToken(new GrimService.Models.SessionToken(user.PlayerToken, DateTime.UtcNow));
                _players.RemoveByTokenTemp(new GrimService.Models.SessionToken(user.PlayerToken, DateTime.UtcNow));
            }
            await _users.ClearSessionAsync(id);
        }
    }
}
