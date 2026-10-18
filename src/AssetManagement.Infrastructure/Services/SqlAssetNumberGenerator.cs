using AssetManagement.Application.Abstractions;
using AssetManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace AssetManagement.Infrastructure.Services
{
    public class SqlAssetNumberGenerator : IAssetNumberGenerator
    {
        private readonly AppDbContext _db;
        public SqlAssetNumberGenerator(AppDbContext db) => _db = db;

        public async Task<string> NextAsync(CancellationToken ct = default)
        {
            var conn = _db.Database.GetDbConnection();
            var wasClosed = conn.State != System.Data.ConnectionState.Open;
            if (wasClosed) await conn.OpenAsync(ct);

            try
            {
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT NEXT VALUE FOR AssetNumberSeq";

                var tx = _db.Database.CurrentTransaction?.GetDbTransaction();
                if (tx is not null) cmd.Transaction = tx;

                var result = await cmd.ExecuteScalarAsync(ct);
                return $"AST-{Convert.ToInt32(result):000}";
            }
            finally
            {
                if (wasClosed) await conn.CloseAsync();
            }
        }
    }
}
