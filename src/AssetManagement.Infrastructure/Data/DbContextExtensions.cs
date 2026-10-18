using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Infrastructure.Data
{
    public static class DbContextExtensions
    {
        public static async Task LockAssetRowAsync(this AppDbContext db, int assetId, CancellationToken ct)
        {
            if (!db.Database.IsSqlServer()) return;
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT Id FROM Assets WITH (UPDLOCK, ROWLOCK) WHERE Id = {assetId}", ct);
        }
    }
}
