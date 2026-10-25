using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using System.Data;
using System.Net;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;
using Dapper;
using Npgsql;

using Microsoft.Extensions.DependencyInjection;
using GrimService.Database.Tables;

namespace GrimService.Database.Controllers
{
    public class BSpecDriverDBManager : IDBManager<BSpecDriverDTO>
    {
        private ILogger<BSpecDriverDBManager> _logger;
        protected IDbConnection _con;

        private const string DriverSelect = @"SELECT d.*,
            (d.entry_until > CURRENT_TIMESTAMP AND d.entry_user_id IS NOT NULL) AS EntryLocked,
            COALESCE((SELECT u.psn_user_id FROM users u WHERE u.id=d.entry_user_id
                AND d.entry_until > CURRENT_TIMESTAMP), '') AS EntryLockUser FROM bspecdriver d";

        public BSpecDriverDBManager(ILogger<BSpecDriverDBManager> logger, IDbConnection con)
        {
            _logger = logger;
            _con = con;
        }

        public async Task<BSpecDriverDTO> GetByIDAsync(long id)
            => await _con.QueryFirstOrDefaultAsync<BSpecDriverDTO>(DriverSelect + " WHERE d.id = @Id", new { Id = id });






        public async Task<int?> GetAuthorIdOfPhotoAsync(long id)
            => await _con.QueryFirstOrDefaultAsync<int?>(@"SELECT user_id FROM bspecdriver WHERE id = @Id", new { Id = id });






        public async Task<int?> GetPhotoCountOfUserAsync(long id)
            => await _con.QueryFirstOrDefaultAsync<int?>(@"SELECT COUNT(id) FROM bspecdriver WHERE user_id = @UserId", new { UserId = id });






        public async Task<IEnumerable<BSpecDriverDTO>> GetAllDriversOfUser(long userId)
            => await _con.QueryAsync<BSpecDriverDTO>(DriverSelect + " WHERE d.user_id=@UserId ORDER BY d.id", new { UserId = userId });

        public async Task<IEnumerable<BSpecDriverDTO>> GetAllDriversOfId(long driverId)
            => await _con.QueryAsync<BSpecDriverDTO>(DriverSelect + " WHERE d.id=@DriverId", new { DriverId = driverId });


        public async Task<bool> BeginEntryAsync(long driverId, long userId)
            => await _con.ExecuteAsync(@"UPDATE bspecdriver d
                SET entry_user_id=@UserId, entry_until=CURRENT_TIMESTAMP + INTERVAL '30 minutes',race_id=NULL
                WHERE d.id=@DriverId
                AND (d.race_id IS NULL OR d.entry_until <= CURRENT_TIMESTAMP)
                AND (d.user_id=@UserId OR EXISTS(SELECT 1 FROM friends f
                    WHERE f.userid=@UserId AND f.friendid=d.user_id))
                AND (d.entry_user_id IS NULL OR d.entry_until IS NULL
                    OR d.entry_until <= CURRENT_TIMESTAMP OR d.entry_user_id=@UserId)",
                new { DriverId = driverId, UserId = userId }) == 1;

        public async Task<bool> EndEntryAsync(long driverId, long userId)
            => await _con.ExecuteAsync(@"UPDATE bspecdriver d
                SET entry_user_id=NULL, entry_until=NULL WHERE d.id=@DriverId
                AND (d.race_id IS NULL OR d.entry_until <= CURRENT_TIMESTAMP)
                AND (d.entry_user_id=@UserId OR
                    ((d.entry_user_id IS NULL OR d.entry_until <= CURRENT_TIMESTAMP)
                    AND (d.user_id=@UserId OR EXISTS(SELECT 1 FROM friends f
                        WHERE f.userid=@UserId AND f.friendid=d.user_id))))",
                new { DriverId = driverId, UserId = userId }) == 1;

        public async Task<IEnumerable<BSpecDriverDTO>> GetAllPhotosOfUsers(string typeId)
            => await _con.QueryAsync<BSpecDriverDTO>(@"SELECT * FROM bspecdriver WHERE type=@Type", new { Type = typeId });

        public Task ClearEntriesAsync(long userId) => _con.ExecuteAsync(@"UPDATE bspecdriver
            SET entry_user_id=NULL,entry_until=NULL,race_id=NULL WHERE entry_user_id=@userId
            AND (race_id IS NULL OR entry_until<=CURRENT_TIMESTAMP)",new{userId});

        public async Task<IEnumerable<BSpecDriverDTO>> GetAllCarPhotosOfUser(long userId, string typeId)
            => await _con.QueryAsync<BSpecDriverDTO>(@"SELECT * FROM bspecdriver WHERE user_id=@UserId AND type=@Type", new { UserId = userId, Type = typeId });

        public async Task UpdateAsync(BSpecDriverDTO pData)
            => await _con.ExecuteAsync(@"UPDATE bspecdriver SET driver_name=@DriverName, driver_level=@DriverLevel, driver_helmet=@DriverHelmet, driver_wear=@DriverWear, helmet_color=@HelmetColor, wear_color=@WearColor, experimental=@Experimental, driver_speed=@DriverSpeed, driver_physical=@DriverPhysical, driver_mental=@DriverMental, driver_heart=@DriverHeart, driver_career=@DriverCareer, win_count=@WinCount, race_count=@RaceCount, condition=@Condition, driver_parameter=@DriverParameter, create_time=@CreateTime WHERE id=@Id AND user_id=@UserId", pData);

        public async Task<long> AddAsync(BSpecDriverDTO pData)
        {
            var query =
@"INSERT INTO bspecdriver (user_id, driver_name, driver_level, driver_helmet, driver_wear, helmet_color, wear_color, experimental, driver_speed, driver_physical, driver_mental, driver_heart, driver_career, win_count, race_count, condition, driver_parameter, create_time)
  VALUES
(@UserId, @DriverName, @DriverLevel, @DriverHelmet, @DriverWear, @HelmetColor, @WearColor, @Experimental, @DriverSpeed, @DriverPhysical, @DriverMental, @DriverHeart, @DriverCareer, @WinCount, @RaceCount, @Condition, @DriverParameter, @CreateTime)
  returning id";

            return await BSpecSlotLimit.InsertAsync(_con, pData.UserId, true, query, pData);
        }

        public async Task RemoveAsync(long id)
            => await _con.ExecuteAsync(@"DELETE FROM bspecdriver WHERE id=@Id", new { Id = id });







        public async Task<bool> RemoveDriverAsync(long userId, long carId)
           => await _con.ExecuteAsync(@"DELETE FROM bspecdriver WHERE user_id=@UserId AND id=@Id
               AND (entry_user_id IS NULL OR entry_until IS NULL OR entry_until <= CURRENT_TIMESTAMP)",
               new { UserId = userId, Id = carId }) == 1;

        public void CreateTable()
        {
            string query =
            @"CREATE TABLE IF NOT EXISTS bspecdriver (
                id BIGSERIAL PRIMARY KEY,
				user_id BIGINT REFERENCES users(id),
                driver_name TEXT,
                driver_level TEXT,
                driver_helmet TEXT,
                driver_wear TEXT,
                helmet_color TEXT,
                wear_color TEXT,
                experimental TEXT,
                driver_speed TEXT,
                driver_physical TEXT,
                driver_mental TEXT,
                driver_heart TEXT,
                driver_career TEXT,
                win_count TEXT,
                race_count TEXT,
                condition TEXT,
                driver_parameter TEXT,
                create_time TIMESTAMP WITHOUT TIME ZONE
			);";

            _con.Execute(query);
            _con.Execute(@"ALTER TABLE bspecdriver ADD COLUMN IF NOT EXISTS entry_user_id BIGINT REFERENCES users(id);
                ALTER TABLE bspecdriver ADD COLUMN IF NOT EXISTS entry_until TIMESTAMP WITH TIME ZONE;
                ALTER TABLE bspecdriver ADD COLUMN IF NOT EXISTS race_id BIGINT;");

            string query2 = @"CREATE INDEX IF NOT EXISTS bspecdriver_user_id_idx ON bspecdriver (user_id)";
            _con.Execute(query2);
        }
    }
}
