using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Dapper;
using GrimService.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GrimService.Database.Controllers
{
    public sealed class BSpecRaceDBManager
    {
        private readonly IDbConnection _con;
        private readonly ILogger<BSpecRaceDBManager> _logger;
        public BSpecRaceDBManager(IDbConnection con,ILogger<BSpecRaceDBManager> logger=null)
        { _con=con;_logger=logger??NullLogger<BSpecRaceDBManager>.Instance; }
        private bool Reject(int host,string reason)
        { _logger.LogWarning("BSpec result rejected: host={Host}, reason={Reason}",host,reason);return false; }
        public void CreateTable() => _con.Execute(@"
            CREATE TABLE IF NOT EXISTS gt5_bspec_races(
                id BIGSERIAL PRIMARY KEY, host_id BIGINT NOT NULL REFERENCES users(id),
                drivers BIGINT[] NOT NULL, owners BIGINT[] NOT NULL,
                created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(), expires_at TIMESTAMPTZ NOT NULL,
                ended BOOLEAN NOT NULL DEFAULT FALSE, fingerprint TEXT, result_start BIGINT,
                summary_xml TEXT, result_xml TEXT);
            CREATE INDEX IF NOT EXISTS gt5_bspec_races_host_idx ON gt5_bspec_races(host_id,id);
            ALTER TABLE gt5_bspec_races ADD COLUMN IF NOT EXISTS result_start BIGINT;
            CREATE UNIQUE INDEX IF NOT EXISTS gt5_bspec_races_retry_idx ON gt5_bspec_races(host_id,fingerprint)
                WHERE fingerprint IS NOT NULL;
            CREATE TABLE IF NOT EXISTS gt5_bspec_monitor(
                race_id BIGINT NOT NULL REFERENCES gt5_bspec_races(id) ON DELETE CASCADE,
                slot_id INTEGER NOT NULL,type INTEGER NOT NULL,payload TEXT NOT NULL,
                updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),PRIMARY KEY(race_id,slot_id,type));
            CREATE TABLE IF NOT EXISTS gt5_bspec_remote_mode(
                user_id BIGINT PRIMARY KEY REFERENCES users(id),expires_at TIMESTAMPTZ NOT NULL);
            ALTER TABLE bspecdriver ADD COLUMN IF NOT EXISTS race_id BIGINT;
            ALTER TABLE bspecdriver ADD COLUMN IF NOT EXISTS online_credit BIGINT NOT NULL DEFAULT 0;
            ALTER TABLE bspecdriver ADD COLUMN IF NOT EXISTS online_experience BIGINT NOT NULL DEFAULT 0;
            ALTER TABLE bspecdriver ADD COLUMN IF NOT EXISTS online_win_count BIGINT NOT NULL DEFAULT 0;
            ALTER TABLE bspecdriver ADD COLUMN IF NOT EXISTS online_race_count BIGINT NOT NULL DEFAULT 0;");
        private sealed class Race
        {
            public long Id {get;set;}
            public long[] Drivers {get;set;}
            public long[] Owners {get;set;}
            public bool Ended {get;set;}
            public string Fingerprint {get;set;}
        }
        private sealed class Driver
        {
            public long Id {get;set;}
            public long UserId {get;set;}
            public string Name {get;set;}
            public string DriverName {get;set;}
        }
        private async Task<bool> Transaction(int host,Func<IDbTransaction,Task<bool>> action)
        {
            bool open=_con.State!=ConnectionState.Open;
            if(open)_con.Open();
            try
            {
                using var tx=_con.BeginTransaction();
                if(!await _con.ExecuteScalarAsync<bool>("SELECT EXISTS(SELECT id FROM users WHERE id=@host FOR UPDATE)",new{host},tx))return false;
                if(!await action(tx))return false;
                tx.Commit();return true;
            }
            finally { if(open)_con.Close(); }
        }
        public Task<bool> Begin(int host,BSpecRaceProtocol data)
        {
            var ids=data.Drivers();int minutes=(int)data.Number("lock_minutes",1,1440);
            var owners=ids.Select((_,i)=>data.Number("user_number"+i.ToString("D2"),1)).ToArray();
            return Transaction(host,async tx=>
            {
                var existing=await _con.QuerySingleOrDefaultAsync<Race>(@"SELECT * FROM gt5_bspec_races
                    WHERE host_id=@host AND NOT ended AND fingerprint IS NULL AND expires_at>NOW() ORDER BY id DESC LIMIT 1 FOR UPDATE",new{host},tx);
                if(existing!=null)return existing.Drivers.SequenceEqual(ids)&&existing.Owners.SequenceEqual(owners);

                var rows=(await _con.QueryAsync<Driver>(@"SELECT d.id,d.user_id FROM bspecdriver d
                    WHERE d.id=ANY(@ids) ORDER BY d.id FOR UPDATE",new{ids},tx)).ToArray();
                if(rows.Length!=ids.Length||ids.Where((id,i)=>!rows.Any(d=>d.Id==id&&d.UserId==owners[i])).Any())return false;
                long race=await _con.ExecuteScalarAsync<long>(@"INSERT INTO gt5_bspec_races(host_id,drivers,owners,expires_at)
                    VALUES(@host,@ids,@owners,NOW()+(@minutes+10)*INTERVAL '1 minute') RETURNING id",new{host,ids,owners,minutes},tx);
                int changed=await _con.ExecuteAsync(@"UPDATE bspecdriver d SET race_id=@race,entry_user_id=@host,
                    entry_until=NOW()+(@minutes+10)*INTERVAL '1 minute'
                    WHERE d.id=ANY(@ids) AND d.entry_user_id=@host AND d.entry_until>NOW()
                    AND (d.race_id IS NULL OR NOT EXISTS(SELECT 1 FROM gt5_bspec_races r
                        WHERE r.id=d.race_id AND NOT r.ended AND r.fingerprint IS NULL AND r.expires_at>NOW()))
                    AND (d.user_id=@host OR EXISTS(SELECT 1 FROM friends f WHERE f.userid=@host AND f.friendid=d.user_id))",
                    new{race,host,ids,minutes},tx);
                return changed==ids.Length;
            });
        }
        public Task<bool> End(int host,BSpecRaceProtocol data)
        {
            var ids=data.Drivers();
            return Transaction(host,async tx=>
            {
                var race=await _con.QuerySingleOrDefaultAsync<Race>(@"SELECT * FROM gt5_bspec_races WHERE host_id=@host
                    ORDER BY id DESC LIMIT 1 FOR UPDATE",new{host},tx);
                if(race==null||!race.Drivers.OrderBy(x=>x).SequenceEqual(ids.OrderBy(x=>x)))return false;
                await _con.ExecuteAsync(@"UPDATE gt5_bspec_races SET ended=TRUE WHERE id=@id;
                    UPDATE bspecdriver SET race_id=NULL,entry_user_id=NULL,entry_until=NULL
                    WHERE race_id=@id AND entry_user_id=@host",new{id=race.Id,host},tx);
                return true;
            });
        }
        public Task<bool> Save(int host,BSpecRaceProtocol data)
        {
            var rows=data.Results();var ids=rows.Select(r=>r.DriverId).ToArray();
            string fingerprint=data.Fingerprint;
            long start=data.Number("start_time");

            data.Summary(0);
            return Transaction(host,async tx=>
            {
                if(await _con.ExecuteScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM gt5_bspec_races WHERE host_id=@host AND fingerprint=@fingerprint)",new{host,fingerprint},tx))return true;
                if(await _con.ExecuteScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM gt5_bspec_races WHERE host_id=@host AND result_start=@start)",new{host,start},tx))return Reject(host,"start_time already saved with different data");
                var race=await _con.QuerySingleOrDefaultAsync<Race>(@"SELECT * FROM gt5_bspec_races WHERE host_id=@host
                    AND fingerprint IS NULL AND expires_at>NOW() ORDER BY id DESC LIMIT 1 FOR UPDATE",new{host},tx);
                if(race==null)return Reject(host,"no pending unexpired race; check beginrace response");
                if(!race.Drivers.OrderBy(x=>x).SequenceEqual(ids.OrderBy(x=>x)))return Reject(host,"result driver IDs differ from reserved race");
                var drivers=(await _con.QueryAsync<Driver>(@"SELECT d.id,d.user_id,d.driver_name AS DriverName,u.psn_user_id AS name
                    FROM bspecdriver d JOIN users u ON u.id=d.user_id WHERE d.id=ANY(@ids) ORDER BY d.id FOR UPDATE OF d",new{ids},tx)).ToArray();
                if(drivers.Length!=ids.Length)return Reject(host,"reserved driver or owner no longer exists");
                if(race.Owners==null||race.Owners.Length!=race.Drivers.Length)
                    return Reject(host,"invalid reserved owner list");




                var resolved=rows.Select(row=>drivers.Where(d=>
                    string.Equals(d.Name,row.User,StringComparison.OrdinalIgnoreCase)&&
                    string.Equals(d.DriverName,row.Driver,StringComparison.Ordinal)).ToArray()).ToArray();
                if(resolved.Any(matches=>matches.Length!=1)||resolved.Select(matches=>matches[0].Id).Distinct().Count()!=rows.Length)
                {
                    _logger.LogWarning("BSpec result participant mapping ambiguous: host={Host}, race={Race}, rows={Rows}",
                        host,race.Id,System.Text.Json.JsonSerializer.Serialize(rows));
                    return Reject(host,"cannot match result names uniquely to reserved participants; no results or rewards changed");
                }
                for(int i=0;i<rows.Length;i++)
                {
                    var row=rows[i];
                    var driver=resolved[i][0];
                    var reservedOwner=race.Owners[Array.IndexOf(race.Drivers,driver.Id)];
                    if(driver.UserId!=reservedOwner)
                    {
                        _logger.LogWarning("BSpec result owner mismatch: host={Host}, race={Race}, driver={Driver}, submittedOwner={SubmittedOwner}, reservedOwner={ReservedOwner}, databaseOwner={DatabaseOwner}",
                            host,race.Id,row.DriverId,row.Owner,reservedOwner,driver.UserId);
                        return Reject(host,"driver owner differs from reserved race");
                    }





                    if(row.Owner!=reservedOwner)
                        _logger.LogDebug("BSpec result user number normalized: host={Host}, race={Race}, driver={Driver}, submittedUserNumber={SubmittedUserNumber}, owner={Owner}",
                            host,race.Id,row.DriverId,row.Owner,reservedOwner);
                    if(!string.Equals(row.User,driver.Name,StringComparison.Ordinal))
                        _logger.LogDebug("BSpec result user label normalized from database: host={Host}, race={Race}, driver={Driver}, owner={Owner}",
                            host,race.Id,row.DriverId,reservedOwner);



                    if(string.IsNullOrWhiteSpace(driver.DriverName))
                        return Reject(host,"reserved driver has no database name");
                    if(row.DriverId!=driver.Id)
                        _logger.LogInformation("BSpec result row remapped: host={Host}, race={Race}, submittedDriver={SubmittedDriver}, driver={Driver}, owner={Owner}, position={Position}, total={Total}, best={Best}",
                            host,race.Id,row.DriverId,driver.Id,reservedOwner,row.Position,row.Total,row.Best);
                    rows[i]=row with { DriverId=driver.Id, Owner=reservedOwner, User=driver.Name, Driver=driver.DriverName };
                }
                await _con.ExecuteAsync(@"UPDATE gt5_bspec_races SET fingerprint=@fingerprint,result_start=@start,summary_xml=@summary,result_xml=@xml,ended=TRUE WHERE id=@id",
                    new{id=race.Id,fingerprint,start,summary=data.Summary(race.Id).ToString(SaveOptions.DisableFormatting),xml=data.ResultXml(race.Id,rows)},tx);

                var winningPosition = BSpecRaceProtocol.WinningPosition(rows);
                if (!winningPosition.HasValue)
                    _logger.LogWarning("BSpec winner not counted: race={Race}, ambiguous positions={Positions}", race.Id, rows.Select(r=>r.Position).ToArray());
                foreach(var row in rows)
                    await _con.ExecuteAsync(@"UPDATE bspecdriver SET online_credit=LEAST(2147483647,online_credit+@Credit),
                        online_experience=LEAST(2147483647,online_experience+@Experience),
                        online_race_count=LEAST(2147483647,online_race_count+1),
                        online_win_count=LEAST(2147483647,online_win_count+CASE WHEN @Won THEN 1 ELSE 0 END)
                        WHERE id=@DriverId AND user_id=@Owner",new {row.Credit,row.Experience,row.DriverId,row.Owner,
                            Won=winningPosition.HasValue && row.Position==winningPosition.Value},tx);
                await _con.ExecuteAsync(@"UPDATE bspecdriver SET race_id=NULL,entry_user_id=NULL,entry_until=NULL
                    WHERE race_id=@id AND entry_user_id=@host",new{id=race.Id,host},tx);
                return true;
            });
        }
        public async Task<string> Summaries(long user)
        {
            var xml=await _con.QueryAsync<string>(@"SELECT summary_xml FROM gt5_bspec_races
                WHERE summary_xml IS NOT NULL AND (host_id=@user OR @user=ANY(owners)) ORDER BY id DESC LIMIT 100",new{user});
            return new XElement("bspec_result_summary_list",xml.Select(XElement.Parse)).ToString(SaveOptions.DisableFormatting);
        }
        public Task<bool> CanReadHistory(long viewer,long owner) => _con.ExecuteScalarAsync<bool>(
            "SELECT @viewer=@owner OR EXISTS(SELECT 1 FROM friends WHERE userid=@viewer AND friendid=@owner)",new{viewer,owner});
        public Task<string> Result(long id,long user) => _con.QuerySingleOrDefaultAsync<string>(@"SELECT result_xml FROM gt5_bspec_races
            WHERE id=@id AND (host_id=@user OR @user=ANY(owners) OR EXISTS(SELECT 1 FROM friends f
                WHERE f.userid=@user AND (f.friendid=host_id OR f.friendid=ANY(owners))))",new{id,user});
        public async Task<bool> Monitor(int host,BSpecRaceProtocol data)
        {
            var m=data.Monitor();
            return await _con.ExecuteAsync(@"INSERT INTO gt5_bspec_monitor(race_id,slot_id,type,payload)
                SELECT id,-1,@type,@xml FROM gt5_bspec_races WHERE host_id=@host AND NOT ended
                    AND expires_at>NOW() ORDER BY id DESC LIMIT 1
                ON CONFLICT(race_id,slot_id,type) DO UPDATE SET payload=EXCLUDED.payload,updated_at=NOW()",
                new{host,type=m.Type,xml=m.Xml})==1;
        }
        public Task RemoteMode(int user,bool enabled) => enabled
            ? _con.ExecuteAsync(@"INSERT INTO gt5_bspec_remote_mode(user_id,expires_at) VALUES(@user,NOW()+INTERVAL '2 minutes')
                ON CONFLICT(user_id) DO UPDATE SET expires_at=EXCLUDED.expires_at",new{user})
            : _con.ExecuteAsync("DELETE FROM gt5_bspec_remote_mode WHERE user_id=@user",new{user});
    }
}
