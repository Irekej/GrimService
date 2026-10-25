using System.Data;
using System.Threading.Tasks;
using Dapper;

namespace GrimService.Database.Controllers
{
    internal static class BSpecSlotLimit
    {
        internal const int Maximum = 6;



        internal static async Task<long> InsertAsync(IDbConnection connection, int userId,
            bool driver, string insertSql, object parameters)
        {
            bool openedHere = connection.State != ConnectionState.Open;
            if (openedHere) connection.Open();
            try
            {
                using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
                var owner = await connection.QuerySingleOrDefaultAsync<long?>(
                    "SELECT id FROM users WHERE id=@userId FOR UPDATE", new { userId }, transaction);
                if (!owner.HasValue) return -1;
                string table = driver ? "bspecdriver" : "bspeccar";
                int count = await connection.ExecuteScalarAsync<int>(
                    $"SELECT COUNT(*) FROM {table} WHERE user_id=@userId", new { userId }, transaction);
                if (count >= Maximum) return -1;
                long id = await connection.ExecuteScalarAsync<long>(insertSql, parameters, transaction);
                transaction.Commit();
                return id;
            }
            finally
            {
                if (openedHere) connection.Close();
            }
        }
    }
}
