using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Dapper;
using GrimService.Database.Tables;

namespace GrimService.Features.PlayerRecords
{
    public sealed class ActionHistoryStore
    {
        private readonly IDbConnection db;
        public ActionHistoryStore(IDbConnection db) => this.db = db;
        public Task<ActionLogDTO> Find(long id) => db.QueryFirstOrDefaultAsync<ActionLogDTO>("SELECT * FROM actionlogs WHERE id=@Id", new { Id = id });
        public Task<IEnumerable<ActionLogDTO>> ForUser(int userId) => db.QueryAsync<ActionLogDTO>("SELECT * FROM actionlogs WHERE user_id=@UserId", new { UserId = userId });
        public Task<long> Append(ActionLogDTO entry) => db.ExecuteScalarAsync<long>("""
            INSERT INTO actionlogs (user_id,create_time,value1,value2,value3,value4,value5)
            VALUES (@UserId,@CreateTime,@Value1,@Value2,@Value3,@Value4,@Value5) RETURNING id
            """, entry);
        public Task Replace(ActionLogDTO entry) => db.ExecuteAsync("""
            UPDATE actionlogs SET user_id=@UserId,create_time=@CreateTime,
            value1=@Value1,value2=@Value2,value3=@Value3,value4=@Value4,value5=@Value5 WHERE id=@Id
            """, entry);
        public Task Delete(long id) => db.ExecuteAsync("DELETE FROM actionlogs WHERE id=@Id", new { Id = id });
        public void EnsureSchema()
        {
            db.Execute("""
                CREATE TABLE IF NOT EXISTS actionlogs (
                    id SERIAL PRIMARY KEY, user_id INTEGER REFERENCES users(id),
                    create_time TIMESTAMP WITHOUT TIME ZONE,
                    value1 TEXT,value2 TEXT,value3 TEXT,value4 TEXT,value5 TEXT)
                """);
            db.Execute("CREATE INDEX IF NOT EXISTS actionlogs_user_id_idx ON actionlogs (user_id)");
        }
    }

    public sealed class PlayerBoardStore
    {
        private readonly IDbConnection db;
        public PlayerBoardStore(IDbConnection db) => this.db = db;
        public Task<BbsDTO> Find(long id) => db.QueryFirstOrDefaultAsync<BbsDTO>("SELECT * FROM bbs WHERE id=@Id", new { Id = id });
        public Task<IEnumerable<BbsDTO>> ForBoard(int boardId) => db.QueryAsync<BbsDTO>("SELECT * FROM bbs WHERE bbs_board_id=@BoardId", new { BoardId = boardId });
        public Task<long> Append(BbsDTO entry) => db.ExecuteScalarAsync<long>("""
            INSERT INTO bbs (bbs_board_id,author_id,comment,create_time)
            VALUES (@BbsBoardId,@AuthorId,@Comment,@CreateTime) RETURNING id
            """, entry);
        public Task Replace(BbsDTO entry) => db.ExecuteAsync("""
            UPDATE bbs SET bbs_board_id=@BbsBoardId,author_id=@AuthorId,
            comment=@Comment,create_time=@CreateTime WHERE id=@Id
            """, entry);
        public Task Delete(long id) => db.ExecuteAsync("DELETE FROM bbs WHERE id=@Id", new { Id = id });
        public Task Clear(int boardId) => db.ExecuteAsync("DELETE FROM bbs WHERE bbs_board_id=@BoardId", new { BoardId = boardId });
        public void EnsureSchema()
        {
            db.Execute("""
                CREATE TABLE IF NOT EXISTS bbs (
                    id SERIAL PRIMARY KEY,bbs_board_id INTEGER REFERENCES users(id),
                    author_id INTEGER REFERENCES users(id),comment TEXT,create_time TIMESTAMP WITHOUT TIME ZONE)
                """);
            db.Execute("CREATE INDEX IF NOT EXISTS bbs_bbsboardid_idx ON bbs (bbs_board_id)");
        }
    }

    public sealed class PlayerRewardStore
    {
        private readonly IDbConnection db;
        public PlayerRewardStore(IDbConnection db) => this.db = db;
        public Task<UserSpecialDTO> Find(long id) => db.QueryFirstOrDefaultAsync<UserSpecialDTO>("SELECT * FROM user_specials WHERE id=@Id", new { Id = id });
        public Task<IEnumerable<UserSpecialDTO>> ForUser(long userId, int type) => type == -1
            ? db.QueryAsync<UserSpecialDTO>("SELECT * FROM user_specials WHERE userid=@UserId", new { UserId = userId })
            : db.QueryAsync<UserSpecialDTO>("SELECT * FROM user_specials WHERE userid=@UserId AND type=@Type", new { UserId = userId, Type = type });
        public Task<long> Append(UserSpecialDTO entry) => db.ExecuteScalarAsync<long>("""
            INSERT INTO user_specials (userid,type,key,value) VALUES (@UserId,@Type,@Key,@Value) RETURNING id
            """, entry);
        public Task Replace(UserSpecialDTO entry) => db.ExecuteAsync("""
            UPDATE user_specials SET userid=@UserId,type=@Type,key=@Key,value=@Value WHERE id=@Id
            """, entry);
        public Task Delete(long id) => db.ExecuteAsync("DELETE FROM user_specials WHERE id=@Id", new { Id = id });
        public void EnsureSchema()
        {
            db.Execute("""
                CREATE TABLE IF NOT EXISTS user_specials (
                    id SERIAL PRIMARY KEY,userid BIGINT REFERENCES users(id),type INT,key TEXT,value TEXT)
                """);
            db.Execute("CREATE INDEX IF NOT EXISTS user_specials_userid_idx ON user_specials (userid)");
        }
    }
}
