using System;
using System.Collections.Generic;
using System.Data;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Dapper;
using GrimService.Database.Tables;

namespace GrimService.Database.Controllers
{
    public class AdminUserDBManager
    {
        private readonly IDbConnection _con;

        public AdminUserDBManager(IDbConnection con) => _con = con;

        public void CreateTable()
        {
            _con.Execute(@"CREATE TABLE IF NOT EXISTS admin_users (
                id BIGSERIAL PRIMARY KEY,
                username TEXT NOT NULL UNIQUE,
                password_hash TEXT NOT NULL,
                created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
                created_by TEXT
            )");

            _con.Execute("ALTER TABLE admin_users ADD COLUMN IF NOT EXISTS is_admin BOOLEAN NOT NULL DEFAULT TRUE; ALTER TABLE admin_users ALTER COLUMN is_admin SET DEFAULT FALSE");
        }





        public async Task SynchronizeBootstrapUserAsync(string username, string password, string createdBy)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password)) return;
            var exists = await _con.ExecuteScalarAsync<bool>("SELECT EXISTS(SELECT 1 FROM admin_users WHERE username = @Username)", new { Username = username });
            if (!exists)
                await CreateAsync(username, password, createdBy, true);
            else
                await _con.ExecuteAsync("UPDATE admin_users SET password_hash = @PasswordHash, is_admin=TRUE WHERE username = @Username",
                    new { Username = username, PasswordHash = HashPassword(password) });
        }

        public async Task CreateAsync(string username, string password, string createdBy, bool isAdmin = false)
        {
            if (string.IsNullOrWhiteSpace(username) || username.Length > 64)
                throw new ArgumentException("Login administratora musi mieć od 1 do 64 znaków.");
            if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
                throw new ArgumentException("Hasło administratora musi mieć co najmniej 8 znaków.");

            await _con.ExecuteAsync(@"INSERT INTO admin_users (username, password_hash, created_by, is_admin)
                VALUES (@Username, @PasswordHash, @CreatedBy, @IsAdmin)", new { Username = username.Trim(), PasswordHash = HashPassword(password), CreatedBy = createdBy, IsAdmin = isAdmin });
        }

        public async Task<bool> VerifyAsync(string username, string password)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password)) return false;
            var hash = await _con.QueryFirstOrDefaultAsync<string>("SELECT password_hash FROM admin_users WHERE username = @Username", new { Username = username });
            return hash != null && VerifyPassword(password, hash);
        }

        public async Task<IEnumerable<AdminUserDTO>> GetAllAsync()
            => await _con.QueryAsync<AdminUserDTO>("SELECT id, username, created_at, created_by, is_admin FROM admin_users ORDER BY username");

        public Task<AdminUserDTO> FindAsync(string username)
            => _con.QueryFirstOrDefaultAsync<AdminUserDTO>("SELECT id, username, is_admin FROM admin_users WHERE username=@username", new { username });

        public Task SetRoleAsync(long id, bool isAdmin)
            => _con.ExecuteAsync("UPDATE admin_users SET is_admin=@isAdmin WHERE id=@id", new { id, isAdmin });

        private static string HashPassword(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210000, HashAlgorithmName.SHA256, 32);
            return $"210000.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
        }

        private static bool VerifyPassword(string password, string encoded)
        {
            var parts = encoded.Split('.');
            if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations)) return false;
            try
            {
                var salt = Convert.FromBase64String(parts[1]);
                var expected = Convert.FromBase64String(parts[2]);
                var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch (FormatException) { return false; }
        }
    }
}
