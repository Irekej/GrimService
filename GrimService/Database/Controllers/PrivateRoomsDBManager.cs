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
    public class PrivateRoomsDBManager : IDBManager<PrivateRoomsDTO>
    {
        private ILogger<PrivateRoomsDBManager> _logger;
        protected IDbConnection _con;
        private readonly string _connectionString;

        public PrivateRoomsDBManager(ILogger<PrivateRoomsDBManager> logger, IDbConnection con)
        {
            _logger = logger;
            _con = con;
            _connectionString = con.ConnectionString;
        }

        public async Task<PrivateRoomsDTO> GetByIDAsync(long id)
            => await _con.QueryFirstOrDefaultAsync<PrivateRoomsDTO>(@"SELECT * FROM privaterooms WHERE id = @Id", new { Id = id });

        public async Task<IEnumerable<PrivateRoomsDTO>> GetPrivateRoom(string username)
        {
            using var con = new NpgsqlConnection(_connectionString);
            return await con.QueryAsync<PrivateRoomsDTO>(@"SELECT p.id,p.user_id AS UserId,u.psn_user_id AS Username,p.stats,p.create_time AS CreateTime
                FROM privaterooms p JOIN users u ON u.id=p.user_id
                WHERE u.psn_user_id=@UserName ORDER BY p.id DESC LIMIT 1", new { UserName = username });
        }

        public async Task<IEnumerable<PrivateRoomsDTO>> GetPrivateRoomById(long userId)
            => await _con.QueryAsync<PrivateRoomsDTO>(@"SELECT * FROM privaterooms WHERE user_id=@UserId ORDER BY id DESC LIMIT 1", new { UserId = userId });

        public async Task UpdatePrivateRoom(PrivateRoomsDTO privaterooms)
        {
            await SaveAsync(privaterooms);
        }

        public async Task UpdateAsync(PrivateRoomsDTO pData)
            => await SaveAsync(pData);

        public async Task<long> AddAsync(PrivateRoomsDTO pData)
            => await SaveAsync(pData);

        public async Task<long> SaveAsync(PrivateRoomsDTO pData)
        {


            using var con = new NpgsqlConnection(_connectionString);
            await con.OpenAsync();
            using var tx = con.BeginTransaction();
            var owner = await con.QuerySingleOrDefaultAsync<string>(
                "SELECT psn_user_id FROM users WHERE id=@UserId FOR UPDATE", pData, tx);
            if (owner == null) throw new InvalidOperationException("Private room owner does not exist.");
            var id = await con.QueryFirstOrDefaultAsync<long?>(
                "SELECT id FROM privaterooms WHERE user_id=@UserId ORDER BY id DESC LIMIT 1", pData, tx);
            var args = new { pData.UserId, Username = owner, pData.Stats, pData.CreateTime, Id = id };
            if (id.HasValue)
                await con.ExecuteAsync("UPDATE privaterooms SET username=@Username, stats=@Stats WHERE id=@Id", args, tx);
            else
                id = await con.ExecuteScalarAsync<long>(@"INSERT INTO privaterooms(user_id,username,stats,create_time)
                    VALUES(@UserId,@Username,@Stats,@CreateTime) RETURNING id", args, tx);

            tx.Commit();
            return id.Value;
        }

        public async Task RemoveAsync(long id)
            => await _con.ExecuteAsync(@"DELETE FROM privaterooms WHERE id=@id", new { Id = id });







        public async Task RemoveCourseAsync(long userId, long courseId)
           => await _con.ExecuteAsync(@"DELETE FROM privaterooms WHERE user_id=@UserId AND id=@Id", new { UserId = userId, Id = courseId });

        public void CreateTable()
        {
            string query =
            @"CREATE TABLE IF NOT EXISTS privaterooms (
                id SERIAL PRIMARY KEY,
				user_id BIGINT REFERENCES users(id),
                username TEXT,
                stats TEXT,
                create_time TIMESTAMP WITHOUT TIME ZONE
			);";

            _con.Execute(query);

            string query2 = @"CREATE INDEX IF NOT EXISTS privaterooms_user_id_idx ON privaterooms (user_id)";
            _con.Execute(query2);
        }
    }
}
