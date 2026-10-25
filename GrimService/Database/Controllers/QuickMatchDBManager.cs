using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;

namespace GrimService.Database.Controllers
{
    public sealed class QuickMatchDBManager
    {
        private readonly IDbConnection _con;
        public QuickMatchDBManager(IDbConnection con) { _con=con; }
        public void CreateTable() => _con.Execute(@"CREATE TABLE IF NOT EXISTS gt6_quickmatch_rounds(
            id UUID PRIMARY KEY,event_id BIGINT NOT NULL,entry_count INTEGER NOT NULL CHECK(entry_count BETWEEN 1 AND 16),
            started_at TIMESTAMPTZ NOT NULL DEFAULT NOW());
            CREATE INDEX IF NOT EXISTS gt6_quickmatch_rounds_event_idx ON gt6_quickmatch_rounds(event_id)");
        public Task RecordAsync(Guid round, ulong eventId, int entries) => _con.ExecuteAsync(@"
            INSERT INTO gt6_quickmatch_rounds(id,event_id,entry_count) VALUES(@round,@eventId,@entries)
            ON CONFLICT(id) DO NOTHING", new{round,eventId=(long)eventId,entries});
        public async Task<Dictionary<string,long>> GetCountersAsync(string[] keys)
        {
            var ids=keys.Select(k=>long.Parse(k.Substring(k.LastIndexOf('_')+1))).Distinct().ToArray();
            var rows=await _con.QueryAsync(@"SELECT event_id,COUNT(*) AS races,COALESCE(SUM(entry_count),0)::bigint AS entries
                FROM gt6_quickmatch_rounds WHERE event_id=ANY(@ids) GROUP BY event_id",new{ids});
            var result=keys.ToDictionary(k=>k,k=>0L);
            foreach(var row in rows)
            {
                string race="QuickMatch_RaceCount_"+row.event_id,entry="QuickMatch_EntryCount_"+row.event_id;
                if(result.ContainsKey(race))result[race]=(long)row.races;
                if(result.ContainsKey(entry))result[entry]=(long)row.entries;
            }
            return result;
        }
    }
}
