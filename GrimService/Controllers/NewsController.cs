using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;

using GrimService.Config;
using GrimService.Utils;
using GrimService.Filters;
using GrimService.Services;
using GrimService.Models;
using GrimService.Infrastructure.Sessions;

namespace GrimService.Controllers
{



    [ApiController]
    [PDIClient]
    [Authorize]
    [Route("/[controller]")]
    [Produces("application/xml")]
    public class NewsController : GrimControllerBase
    {
        private readonly ILogger<NewsController> _logger;
        private readonly GameServerOptions _gameServerOptions;
        private readonly PlayerManager _players;
        private readonly NewsService _news;
        private readonly GameRequestSession _sessions;

        public NewsController(PlayerManager players, IOptions<GameServerOptions> options, ILogger<NewsController> logger, NewsService news, GameRequestSession sessions = null) : base (players)
        {
            _logger = logger;
            _gameServerOptions = options.Value;
            _players = players;
            _news = news;
            _sessions = sessions;
        }

        private async Task<Player> ResolveNewsPlayer()
        {
            var player = Player;
            if (player == null && _sessions != null)
            {
                var session = await _sessions.Resolve(Request.Cookies["X-gt-token"]);
                player = session.Player;
            }
            if (player == null)
            {
                _logger.LogWarning("News request has no game session: path={Path}", Request.Path);
                Response.StatusCode = 401;
                return null;
            }
            _players.CheckIfNotConnected(player);
            _players.UpdatePlayerActivity(player);
            return player;
        }

        [HttpGet]
        [Route("{serverId}/{region}/{fileId:int}.xml")]
        public async Task GetNews(string serverId, string region, int fileId)
        {
            var player = await ResolveNewsPlayer();
            if (player == null) return;
            try {
                var xml=await _news.Article(player.Data.LastGame,serverId,region,fileId);
                if(xml==null){Response.StatusCode=404;return;}
                Response.ContentType="application/xml";await Response.WriteAsync(xml);
            } catch(ArgumentException){Response.StatusCode=400;}
        }

        [HttpGet]
        [Route("news/{serverId}/{region}/{fileId:int}.xml")]
        public async Task GetNewsNew(string serverId, string region, int fileId)
        {
            await GetNews(serverId,region,fileId);
        }

        [HttpGet]
        [Route("{serverId}/{region}/l{category_id1:int}_{category_id2:int}.xml")]
        [Route("news/{serverId}/{region}/l{category_id1:int}_{category_id2:int}.xml")]
        public async Task GetNewsList(string serverId, string region, int category_id1, int category_id2)
        {
            var player = await ResolveNewsPlayer();
            if (player == null) return;
            try {
                Response.ContentType="application/xml";
                await Response.WriteAsync(await _news.Feed(player.Data.LastGame,serverId,region,category_id1,category_id2));
            } catch(ArgumentException){Response.StatusCode=400;}
        }







        [HttpGet]
        [Route("{serverId}/{region}/root.xml")]
        [Route("news/{serverId}/{region}/root.xml")]
        public async Task GetCategoryRoot(string serverId, string region)
        {
            var player = await ResolveNewsPlayer();
            if (player == null) return;
            try {
                Response.ContentType="application/xml";
                await Response.WriteAsync(await _news.Categories(player.Data.LastGame,serverId,region));
            } catch(ArgumentException){Response.StatusCode=400;}
        }







        [HttpGet]
        [Route("common/{newsId}/{imageId}.jpg")]
        public async Task GetNewsImage(int newsId, int imageId)
        {
            var player = await ResolveNewsPlayer();
            if (player == null) return;
            Response.ContentType="image/jpeg";
            if(newsId>=1000000)
            {
                var bytes=await _news.Image(newsId,imageId,player.Data.LastGame);
                if(bytes==null){Response.StatusCode=404;return;}
                Response.Headers["X-Content-Type-Options"]="nosniff";
                await Response.Body.WriteAsync(bytes);return;
            }
            string newsFile = $"news/common/{newsId}/{imageId}.jpg";
            await this.SendFile(_gameServerOptions.XmlResourcePath, newsFile);
        }
    }
}
