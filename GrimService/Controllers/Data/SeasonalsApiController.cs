using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using GrimService.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace GrimService.Controllers
{
    [ApiController, Route("api/seasonals")]
    public sealed class SeasonalsApiController : ControllerBase
    {
        private readonly SeasonalEventService events;
        private readonly string connectionString;
        public SeasonalsApiController(SeasonalEventService events, IDbConnection db)
        { this.events=events; connectionString=db.ConnectionString; }
        [HttpGet("{game}")]
        [ResponseCache(Duration=30)]
        public IActionResult List(string game)
        {
            if (game != "GT5" && game != "GT6") return BadRequest();
            return Ok(events.PublicEvents(game));
        }
        [HttpGet("{game}/{folder:int}/{eventId}/ranking")]
        [ResponseCache(Duration=15)]
        public async Task<IActionResult> Ranking(string game, int folder, string eventId, int page = 1)
        {
            if ((game != "GT5" && game != "GT6") || page < 1 || page > 100000) return BadRequest();
            var entry=events.PublicEvents(game).FirstOrDefault(e=>e.Folder==folder && e.Id==eventId);
            if (entry == null || !long.TryParse(entry.BoardId,out var board) || board<=0) return NotFound();
            using var db=new NpgsqlConnection(connectionString);

            var rows=await db.QueryAsync(@"SELECT username,country,score FROM rankings WHERE board_id=@board
                AND score ~ '^[0-9]+$' ORDER BY score::numeric ASC,id ASC LIMIT 20 OFFSET @offset",new{board=entry.BoardId,offset=(page-1)*20});
            return Ok(rows.Select((r,i)=>new {position=(page-1)*20+i+1,username=(string)r.username,country=(string)r.country,
                score=(string)r.score,formattedScore=FormatScore((string)r.score)}));
        }
        [HttpGet("{game}/rankings")]
        [ResponseCache(Duration=30)]
        public IActionResult RankingCatalog(string game)
        {
            if (game != "GT5" && game != "GT6") return BadRequest();
            return Ok(events.PublicEvents(game)
                .Where(e=>long.TryParse(e.BoardId,out var id) && id>0)
                .OrderBy(e=>e.Folder).GroupBy(e=>e.BoardId).Select(group=>group.First()));
        }
        private static string FormatScore(string score)
        {
            if (!long.TryParse(score, out var ms) || ms < 0) return "Invalid Time";
            return FormattableString.Invariant($"{ms / 60000}:{ms / 1000 % 60:00}.{ms % 1000:000}");
        }
    }
}
