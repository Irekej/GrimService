using GrimService.Database.Controllers;
using GrimService.Database.Tables;
using GrimService.Models;
using GrimService.Services;
using System;
using System.Threading.Tasks;

namespace GrimService.Controllers.Services
{
    public class SessionRecover
    {
        private UserDBManager _users;
        private PlayerManager _players;

        public SessionRecover(UserDBManager users, PlayerManager players)
        {
            _users = users;
            _players = players;
        }

        public async Task<bool> CheckIfPlayerExist(string sessionTicket)
        {
            if(string.IsNullOrWhiteSpace(sessionTicket))return false;
            try
            {
                UserDTO user = await _users.GetBySessionTicketAsync(sessionTicket);
                if (user == null || user.TokenExpiry <= DateTime.Now || user.BanStatus == "1")
                {
                    return false;
                }
                var sToken = new SessionToken(user.PlayerToken, user.TokenExpiry);
                var existing = _players.GetPlayerByToken(sessionTicket);
                if(existing != null)
                {

                    if(existing.Data.Id != user.Id)return false;
                    existing.Token=sToken;
                    existing.Data.TokenExpiry=user.TokenExpiry;
                    existing.LastUpdate=DateTime.Now;
                    return true;
                }
                var player = new Player(user, sToken);
                player.LastUpdate = DateTime.Now;
                _players.AddUser(player);
                _players.AddUserTemp(player);

                return true;
            }
            catch (Exception ex)
            {
                Console.Write(ex);
                return false;
            }
        }
    }
}
