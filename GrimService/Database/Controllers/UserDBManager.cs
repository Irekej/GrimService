using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

using System.Data;

using Microsoft.Extensions.Logging;
using Dapper;
using Npgsql;

using Microsoft.Extensions.DependencyInjection;
using GrimService.Database.Tables;
using GrimService.Models;

namespace GrimService.Database.Controllers
{
    public class UserDBManager : IDBManager<UserDTO>
    {
        private ILogger<UserDBManager> _logger;
        protected IDbConnection _con;

        public UserDBManager(ILogger<UserDBManager> logger, IDbConnection con)
        {
            _logger = logger;
            _con = con;
        }

        public async Task<UserDTO> GetByIDAsync(long id)
            => await _con.QueryFirstOrDefaultAsync<UserDTO>(@"SELECT * FROM users WHERE id = @id", new { Id = id });

        public async Task<IEnumerable<UserDTO>> SearchAsync(string query, string game = null)
            => await _con.QueryAsync<UserDTO>(@"SELECT * FROM users
                WHERE (@Query IS NULL OR @Query = '' OR psn_user_id ILIKE '%' || @Query || '%' OR nickname ILIKE '%' || @Query || '%')
                AND (@Game IS NULL OR lastgame=@Game)
                ORDER BY last_activity DESC NULLS LAST, id DESC LIMIT 100", new { Query = query, Game = game });

        public async Task<int> CountAsync()
            => await _con.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM users");

        public async Task<int> CountBannedAsync()
            => await _con.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM users WHERE banstatus = '1'");

        public async Task SetBanAsync(long id, bool isBanned, string reason, string changedBy)
        {
            await _con.ExecuteAsync(@"UPDATE users SET banstatus = @BanStatus WHERE id = @Id", new { Id = id, BanStatus = isBanned ? "1" : "0" });
            if (isBanned)
            {
                await _con.ExecuteAsync(@"INSERT INTO player_bans (user_id, reason, created_by, created_at, revoked_at)
                    VALUES (@UserId, @Reason, @CreatedBy, NOW(), NULL)", new { UserId = id, Reason = reason, CreatedBy = changedBy });
            }
            else
            {
                await _con.ExecuteAsync(@"UPDATE player_bans SET revoked_at = NOW() WHERE user_id = @UserId AND revoked_at IS NULL", new { UserId = id });
            }
        }

        public async Task<string> GetBanReasonAsync(long id)
            => await _con.QueryFirstOrDefaultAsync<string>(@"SELECT reason FROM player_bans WHERE user_id = @UserId AND revoked_at IS NULL ORDER BY created_at DESC LIMIT 1", new { UserId = id });

        public async Task ClearSessionAsync(long id)
            => await _con.ExecuteAsync(@"UPDATE users SET player_token = NULL, ticket_expiry = NULL WHERE id = @Id", new { Id = id });

        public async Task UpdateAdminFieldsAsync(long id, string nickname, string comment, string credit, string welcomeMessage)
            => await _con.ExecuteAsync(@"UPDATE users SET nickname = @Nickname, comment = @Comment, credit = @Credit, welcomemessage = @WelcomeMessage WHERE id = @Id",
                new { Id = id, Nickname = nickname, Comment = comment, Credit = credit, WelcomeMessage = welcomeMessage });

        public async Task<UserDTO> GetUserByDiscordId(string discordId)
            => await _con.QueryFirstOrDefaultAsync<UserDTO>(@"SELECT * FROM users WHERE discordid = @DiscordId", new { DiscordId = discordId });






        public async Task<UserDTO> GetByPSNUserIdAsync(string psnId)
            => await _con.QueryFirstOrDefaultAsync<UserDTO>(@"SELECT * FROM users WHERE psn_user_id = @UserId", new { UserId = psnId });

        public async Task<List<UserDTO>> GetByMACList(string macAddress)
        {
            var users = await _con.QueryAsync<UserDTO>(@"SELECT * FROM users WHERE mac = @MacADdress", new { MacADdress = macAddress });
            return users.ToList();
        }

        public async Task<string> GetByIPAddress(string ipaddr)
            => await _con.QueryFirstOrDefaultAsync<string>(@"SELECT psn_user_id FROM users WHERE ipaddress = @IPAddress", new { IPAddress = ipaddr });

        public async Task<string> GetPlayersDiscordID(string psnId)
            => await _con.QueryFirstOrDefaultAsync<string>(@"SELECT discordid FROM users WHERE psn_user_id = @UserId", new { UserId = psnId });

        public async Task<UserDTO> GetBySessionTicketAsync(string sessionTicket)
        {
            try
            {
                UserDTO userData = await _con.QueryFirstOrDefaultAsync<UserDTO>(@"SELECT *, ticket_expiry AS ""TokenExpiry"" FROM users WHERE player_token = @SessTick", new { SessTick = sessionTicket });
                return userData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error GetBySessionTicketAsync");
                throw;
            }
        }






        public async Task<int?> GetInternalIdOfUserAsync(string psnId)
            => await _con.QueryFirstOrDefaultAsync<int?>(@"SELECT id FROM users WHERE psn_user_id = @UserId", new { UserId = psnId });






        public async Task<string> GetPSNNameByIdAsync(long id)
            => await _con.QueryFirstOrDefaultAsync<string>(@"SELECT psn_user_id FROM users WHERE id = @id", new { Id = id });

        public async Task UpdateAsync(UserDTO pData)
            => await _con.ExecuteAsync(@"UPDATE users", pData);






        public async Task UpdateNewNickname(UserDTO pData)
            => await _con.ExecuteAsync(@"UPDATE users SET nickname=@Nickname, nickname_changes=@NicknameChanges WHERE id = @Id", pData);

        public async Task UpdateCIDandPlatform(string consoleId, string platform, long userId)
            => await _con.ExecuteAsync(@"UPDATE users SET console_id=@ConsoleID, platform=@Platform WHERE id = @Id", new { ConsoleID = consoleId, Platform = platform, Id = userId });

        public async Task UpdatePlatform(string platform, long userId)
            => await _con.ExecuteAsync(@"UPDATE users SET platform=@Platform WHERE id = @Id", new { Platform = platform, Id = userId });

        public async Task<bool> UpdateCountryFromTicketAsync(UserDTO user, string country)
        {
            if (user == null || country == null || country.Length != 2) return false;
            foreach (char letter in country)
                if (!(letter >= 'A' && letter <= 'Z') && !(letter >= 'a' && letter <= 'z')) return false;
            string normalized = country.ToLowerInvariant();
            if (string.Equals(user.Country, normalized, StringComparison.Ordinal)) return false;
            int updated = await _con.ExecuteAsync("UPDATE users SET country=@Country WHERE id=@Id",
                new { Country = normalized, user.Id });
            if (updated != 1) return false;
            user.Country = normalized;
            return true;
        }

        public async Task<bool> BindAuthIdentityAsync(long id, string provider, long? rpcnUserId)
            => await _con.ExecuteAsync(@"UPDATE users SET auth_provider=@Provider,
                rpcn_user_id=COALESCE(rpcn_user_id,@RpcnUserId)
                WHERE id=@Id AND (auth_provider IS NULL OR auth_provider=@Provider)
                AND (rpcn_user_id IS NULL OR rpcn_user_id=@RpcnUserId)",
                new { Id = id, Provider = provider, RpcnUserId = rpcnUserId }) == 1;

        public async Task UpdatePlayerTicket(string token, DateTime ExpiryDate, long id)
            => await _con.ExecuteAsync(@"UPDATE users SET player_token=@PlayerTicket, ticket_expiry=@TokenExpiry WHERE id = @Id", new { PlayerTicket = token, TokenExpiry = ExpiryDate, Id = id });






        public async Task UpdateDiscordId(string discordid, long id)
            => await _con.ExecuteAsync(@"UPDATE users SET discordid=@DiscordId WHERE id = @Id", new { DiscordId = discordid, Id = id });

        public async Task UpdateLoggins(long loggins, string psn)
            => await _con.ExecuteAsync(@"UPDATE users SET numbers_of_login=@NumbersOfLogin WHERE psn_user_id = @PSNUserId", new { NumbersOfLogin = loggins, PSNUserId = psn });

        public async Task UpdateLastActivity(DateTime lastactivity, string psn)
            => await _con.ExecuteAsync(@"UPDATE users SET last_activity=@LastActivity WHERE psn_user_id = @PSNUserId", new { LastActivity = lastactivity, PSNUserId = psn });






        public async Task UpdateWelcomeMessage(UserDTO pData)
            => await _con.ExecuteAsync(@"UPDATE users SET welcomemessage=@WelcomeMessage WHERE id = @Id", pData);

        public async Task UpdateGame(UserDTO pData)
            => await _con.ExecuteAsync(@"UPDATE users SET lastgame=@LastGame WHERE id = @Id", pData);

        public async Task UpdateIpAndMac(UserDTO pData)
            => await _con.ExecuteAsync(@"UPDATE users SET ipaddress=@IPAddress, " +
                "mac = CASE WHEN @MacAddress IS NOT NULL THEN @MacAddress ELSE mac END WHERE id = @Id", pData);






        public async Task UpdateHelmet(long id, int helmetid, int helmetcolor)
            => await _con.ExecuteAsync(@"UPDATE users SET helmet=@HelmetId, helmet_color=@HelmetColorId WHERE id = @Id", new { HelmetId = helmetid, HelmetColorId = helmetcolor, Id = id });

        public async Task UpdateHelmetGT5(long id, int helmetid, int helmetcolor)
            => await _con.ExecuteAsync(@"UPDATE users SET menu_helmet=@HelmetId, menu_helmet_color=@HelmetColorId WHERE id = @Id", new { HelmetId = helmetid, HelmetColorId = helmetcolor, Id = id });






        public async Task UpdateWear(long id, int wearid, int wearcolor)
            => await _con.ExecuteAsync(@"UPDATE users SET wear=@WearId, wear_color=@WearColorId WHERE id = @Id", new { WearId = wearid, WearColorId = wearcolor, Id = id });
        
        public async Task UpdateWearGT5(long id, int wearid, int wearcolor)
            => await _con.ExecuteAsync(@"UPDATE users SET menu_wear=@WearId, menu_wear_color=@WearColorId WHERE id = @Id", new { WearId = wearid, WearColorId = wearcolor, Id = id });






        public async Task UpdateHomeDesign(UserDTO pData)
            => await _con.ExecuteAsync(@"UPDATE users SET menu_color=@MenuColor, menu_matiere=@MenuMatiere WHERE id = @Id", pData);






        public async Task UpdateGameStats(UserDTO pData)
            => await _con.ExecuteAsync("UPDATE users SET license_level=@LicenseLevel, achievement=@Achievement, trophy=@Trophy, " +
                "car_count=@CarCount, license_gold=@LicenseGold, odometer=@Odometer WHERE id = @Id", pData);

        public async Task UpdateGameSettings(UserDTO pData)
            => await _con.ExecuteAsync("UPDATE users SET profile_level=@ProfileLevel, comment=@Comment, tag_id_csv=@TagIdCSV WHERE id = @Id", pData);

        public async Task UpdateTagSettings(UserDTO pData)
            => await _con.ExecuteAsync("UPDATE users SET tag_id_csv=@TagIdCSV WHERE id = @Id", pData);

        public async Task UpdateCommentSettings(UserDTO pData)
            => await _con.ExecuteAsync("UPDATE users SET profile_level=@ProfileLevel, comment=@Comment WHERE id = @Id", pData);

        public async Task UpdateProfileLevelSettings(UserDTO pData)
            => await _con.ExecuteAsync("UPDATE users SET profile_level=@ProfileLevel WHERE id = @Id", pData);

        public async Task UpdateGameRecent(UserDTO pData)
            => await _con.ExecuteAsync("UPDATE users SET recent=@Recent, recent_update_time=@RecentUpdateTime WHERE id = @Id", pData);

        public async Task UpdatePlayerAvatar(string AvatarPhotoId, long Id)
            => await _con.ExecuteAsync("UPDATE users SET photo_id_avatar=@AvatarPhotoId WHERE id=@Id", new { AvatarPhotoId = AvatarPhotoId, Id = Id });






        public async Task UpdateOnlineProfile(UserDTO pData)
            => await _con.ExecuteAsync("UPDATE users SET profile_level=@ProfileLevel, playtime_level=@PlaytimeLevel, comment_level=@CommentLevel, " +
                "playtime=@Playtime, comment=@Comment WHERE id = @Id", pData);






        public async Task UpdateMyHomeProfile(UserDTO pData)
            => await _con.ExecuteAsync("UPDATE users SET license_level=@LicenseLevel, achievement=@Achievement, trophy=@Trophy, " +
                "car_count=@CarCount, license_gold=@LicenseGold, odometer=@Odometer, win_count=@WinCount, license_silver=@LicenseSilver, " +
                "license_bronze=@LicenseBronze, aspec_level=@ASpecLevel, bspec_level=@BSpecLevel, aspec_exp=@ASpecExp, bspec_exp=@BSpecExp, " +
                "credit=@Credit WHERE id = @Id", pData);

        public async Task<long> AddAsync(UserDTO pData)
        {
            var query =
@"INSERT INTO users (psn_user_id, ipaddress, mac, country, nickname, platform, auth_provider, rpcn_user_id)
  VALUES(@PSNUserId, @IPAddress, @MacAddress, @Country, @Nickname, @Platform, @AuthProvider, @RpcnUserId)
  returning id";
            
            return await _con.ExecuteScalarAsync<long>(query, new { pData.PSNUserId, pData.IPAddress, 
                pData.MacAddress, pData.Country, pData.Nickname, pData.Platform, pData.AuthProvider, pData.RpcnUserId });
        }

        public async Task RemoveAsync(long id)
            => await _con.ExecuteAsync(@"DELETE FROM users WHERE id=@Id", new { Id = id });

        public async Task RemoveByPSNUserIdAsync(string psnUserId)
            => await _con.ExecuteAsync(@"DELETE FROM users WHERE psn_user_id=@Id", new { Id = psnUserId });

        public void CreateTable()
        {
            string query =
            @"CREATE TABLE IF NOT EXISTS users (
                id SERIAL PRIMARY KEY,
                psn_user_id TEXT UNIQUE,
                ipaddress TEXT,
                country TEXT,
                mac TEXT,

                nickname_changes INTEGER DEFAULT 3,
                license_level TEXT DEFAULT 0,
                achievement TEXT DEFAULT 0,
                trophy TEXT DEFAULT 0,
                car_count TEXT DEFAULT 0,
                license_gold TEXT DEFAULT 0,
                odometer TEXT DEFAULT 0,
                aspec_level TEXT DEFAULT 0,
                aspec_exp TEXT DEFAULT 0,
                bspec_level TEXT DEFAULT 0,
                bspec_exp TEXT DEFAULT 0,
                win_count TEXT DEFAULT 0,
                license_silver TEXT DEFAULT 0,
                license_bronze TEXT DEFAULT 0,
                credit TEXT DEFAULT 0,
    
                profile_level TEXT,
                comment TEXT,
                tag_id_csv TEXT,
                recent TEXT,
                recent_update_time TIMESTAMP WITHOUT TIME ZONE,

                nickname TEXT,
                photo_id_avatar TEXT DEFAULT 0,
                photo_bg TEXT,

                helmet INTEGER DEFAULT 7,
                helmet_color INTEGER DEFAULT 0,
                wear INTEGER DEFAULT 7,
                wear_color INTEGER DEFAULT 0,
                menu_color INTEGER DEFAULT 0,
                menu_matiere INTEGER DEFAULT 0,

                menu_helmet INTEGER DEFAULT 7,
                menu_helmet_color INTEGER DEFAULT 0,
                menu_wear INTEGER DEFAULT 7,
                menu_wear_color INTEGER DEFAULT 0,

                welcomemessage TEXT,

                banstatus TEXT,

                band_test INTEGER DEFAULT 0,
                band_up INTEGER DEFAULT 0,
                band_down INTEGER DEFAULT 0,
                band_update_time TIMESTAMP WITHOUT TIME ZONE,

                lastgame TEXT,
                discordid TEXT,
                numbers_of_login INTEGER DEFAULT 0,
                last_activity TIMESTAMP WITHOUT TIME ZONE,
                player_token TEXT,
                ticket_expiry TIMESTAMP WITHOUT TIME ZONE,
                platform TEXT,
                console_id TEXT
			);";

            _con.Execute(query);
            _con.Execute(@"ALTER TABLE users ADD COLUMN IF NOT EXISTS auth_provider TEXT;
                ALTER TABLE users ADD COLUMN IF NOT EXISTS rpcn_user_id BIGINT;");

            string query2 = @"CREATE INDEX IF NOT EXISTS users_psnuserid_idx ON users (psn_user_id)";
            _con.Execute(query2);

            _con.Execute(@"CREATE TABLE IF NOT EXISTS player_bans (
                id BIGSERIAL PRIMARY KEY,
                user_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                reason TEXT,
                created_by TEXT,
                created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                revoked_at TIMESTAMP WITH TIME ZONE
            )");
            _con.Execute(@"CREATE INDEX IF NOT EXISTS player_bans_active_idx ON player_bans (user_id) WHERE revoked_at IS NULL");
        }
    }
}
