using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System;
using GrimService.Models;
using GrimService.Services;
using GrimService.Controllers.Profiles;
using GrimService.Database.Controllers;
using System.Threading.Tasks;
using System.Globalization;
using System.Collections;
using GrimService.Models.Xml;

namespace GrimService.Controllers.Data
{
    [ApiController]

    public class PlayersForecastController : ControllerBase
    {
        private readonly PlayerManager _players;
        public readonly AuthenticationController authcont;
        private readonly ILogger<PlayersForecastController> _logger;
        private readonly RankingsDBManager _rankings;
        private readonly UserDBManager _userdb;

        public PlayersForecastController(ILogger<PlayersForecastController> logger, PlayerManager players, RankingsDBManager rankings, UserDBManager userdb)
        {
            _logger = logger;
            _players = players;
            _rankings = rankings;
            _userdb = userdb;
        }

        [Route("playersforecast")]
        [HttpGet]
        public IEnumerable<object> Get()
        {

            var connectedPlayers = _players.GetConnectedPlayers();

            var selectedPlayers = connectedPlayers.Select(player => new
            {
                player.Data.PSNUserId,
                player.Data.Platform,
                player.Data.Country,
                player.Data.LastGame
            });

            int PlayersNumber = selectedPlayers.Count();


            return new List<object>
            {
                new
                {
                    PlayersNumber,
                    ConnectedPlayers = selectedPlayers.ToList()
                }
            };
        }

        [Route("rankingsforecast/{board_id}")]
        [HttpGet]
        public async Task<IEnumerable<object>> Get2(string board_id)
        {
            var rankingsList = await _rankings.GetAllRankingsForecast(board_id);

            var sortedRankings = rankingsList.OrderBy(ranking => int.Parse(ranking.Score));

            var selectedRankings = sortedRankings.Select(ranking => new
            {
                ranking.Username,
                ranking.Country,
                FormattedScore = FormatMilliseconds(ranking.Score)
            }).ToList();

            return new List<object>
            {
                new
                {
                    Rankings = selectedRankings
                }
            };
        }

        [Route("playerinfoforecast/{user_name}")]
        [HttpGet]
        public async Task<ActionResult> GetForecast(string user_name)
        {
            var playerData = await _userdb.GetByPSNUserIdAsync(user_name);

            if (playerData == null)
            {
                return NotFound($"Player with PSNUserId {user_name} not found");
            }

            var forecast = new
            {
                playerData.Id,
                playerData.DiscordId,
                playerData.PSNUserId,
                playerData.Platform,
                playerData.Country,
                playerData.LastGame,
                playerData.NumbersOfLogin,
                playerData.LastActivity
            };

            return Ok(new { Forecast = forecast });
        }

        [Route("rankingsdriftforecast/{board_id}")]
        [HttpGet]
        public async Task<IEnumerable<object>> Get4(string board_id)
        {
            var rankingsList = await _rankings.GetAllRankingsForecast(board_id);

            var sortedRankings = rankingsList.OrderBy(ranking => int.Parse(ranking.Score));

            var selectedRankings = sortedRankings.Select(ranking => new
            {
                ranking.Username,
                ranking.Country,
                ranking.Score
            }).ToList();

            return new List<object>
            {
                new
                {
                    Rankings = selectedRankings
                }
            };
        }

        [Route("playerrankingforecast/{user_id}")]
        [HttpGet]
        public async Task<IEnumerable<object>> Get3(string user_id)
        {
            var rankingsList = await _rankings.GetAllPlayerRankingForecast(user_id);

            var sortedRankings = rankingsList.OrderBy(ranking => int.Parse(ranking.Score));

            var selectedRankings = sortedRankings.Select(ranking => new
            {
                ranking.BoardId,
                ranking.Username,
                ranking.Country,
                FormattedScore = FormatMilliseconds(ranking.Score)
            }).ToList();

            return new List<object>
            {
                new
                {
                    Rankings = selectedRankings
                }
            };
        }

        private string FormatMilliseconds(string millisecondsString)
        {
            if (long.TryParse(millisecondsString, out long milliseconds))
            {
                TimeSpan timeSpan = TimeSpan.FromMilliseconds(milliseconds);
                return $"{timeSpan.Minutes}:{timeSpan.Seconds}.{timeSpan.Milliseconds}";
            }
            else
            {

                return "Invalid Time";
            }
        }
    }
}
