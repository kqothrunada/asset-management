using AssetManagement.Application.Abstraction;
using AssetManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.Services
{
    public class SqlAssetNumberGenerator : IAssetNumberGenerator
    {
        private readonly AppDbContext _db;
        public SqlAssetNumberGenerator(AppDbContext db) => _db = db;

        public async Task<string> NextAsync(CancellationToken ct = default)
        {
            var n = await _db.Database
                .SqlQuery<int>($"SELECT NEXT VALUE FOR AssetNumberSeq AS [Value]")
                .SingleAsync(ct);

            return $"AST-{n:000}";
        }
    }
}
