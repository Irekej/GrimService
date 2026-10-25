using Microsoft.AspNetCore.Mvc;
using GrimService.Models;
using GrimService.Services;

namespace GrimService.Controllers
{
    public class GrimControllerBase : ControllerBase
    {
        protected readonly PlayerManager Players;

        public GrimControllerBase(PlayerManager players)
        {
            Players = players;
        }

        public Player Player =>
            Request.Cookies.TryGetValue("X-gt-token", out var token) && token != null
                ? Players.GetPlayerByToken(token)
                : null;
    }
}
