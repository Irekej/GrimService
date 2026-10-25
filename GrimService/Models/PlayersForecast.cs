using GrimService.Models;
using System;
using System.Collections.Generic;

namespace GrimService.Models
{
    public class PlayersForecast
    {


        public int PlayersNumber { get; set; }

        public List<Player> ConnectedPlayers { get; set; }

    }
}
