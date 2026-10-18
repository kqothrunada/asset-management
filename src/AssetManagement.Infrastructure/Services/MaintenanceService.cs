using AssetManagement.Application;
using AssetManagement.Application.Dtos;
using AssetManagement.Application.Exceptions;
using AssetManagement.Domain;
using AssetManagement.Infrastructure.Data;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace AssetManagement.Infrastructure.Services
{
    public class MaintenanceService
    {
        private readonly AppDbContext _context;
        private readonly AssetService _assets;
        private readonly IValidator<MaintenanceUpsertDto> _validator;
        private readonly ILogger<MaintenanceService> _log;


        public MaintenanceService(AppDbContext context, AssetService assets, IValidator<MaintenanceUpsertDto> validator, ILogger<MaintenanceService> log)
        { 
            _context = context; 
            _assets = assets; 
            _validator = validator; 
            _log = log; 
        }

        #region View
        public async Task<List<MaintenanceDto>> ListAsync(int assetId, ClaimsPrincipal u, CancellationToken ct)
        {
            await _assets.FindVisibleAsync(assetId, u, ct);
            var rows = await _context.AssetMaintenances.AsNoTracking().Where(m => m.AssetId == assetId)
                .OrderByDescending(m => m.Date).ThenByDescending(m => m.Id).ToListAsync(ct);
            return rows.Select(ToDto).ToList();
        }
        #endregion

        #region Transaction
        public async Task<MaintenanceResult> AddAsync(int assetId, MaintenanceUpsertDto dto, ClaimsPrincipal u, CancellationToken ct)
        {
            await _validator.ValidateAndThrowAsync(dto, ct);
            var asset = await LoadForWriteAsync(assetId, u, ct);

            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            await _context.LockAssetRowAsync(assetId, ct);

            var m = new AssetMaintenance { AssetId = assetId };
            Apply(m, dto);
            _context.AssetMaintenances.Add(m);
            await _context.SaveChangesAsync(ct);
            await RecalculateAsync(asset, ct);
            await tx.CommitAsync(ct);

            _log.LogInformation("Maintenance {Id} is added to {Code}, total {Total}", m.Id, asset.AssetCode, asset.AssetValueTotal);
            return new MaintenanceResult(ToDto(m), asset.AssetValueTotal);
        }

        public async Task<MaintenanceResult> UpdateAsync(int assetId, int id, MaintenanceUpsertDto dto, ClaimsPrincipal u, CancellationToken ct)
        {
            await _validator.ValidateAndThrowAsync(dto, ct);
            var asset = await LoadForWriteAsync(assetId, u, ct);

            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            await _context.LockAssetRowAsync(assetId, ct);

            var m = await _context.AssetMaintenances.FirstOrDefaultAsync(x => x.Id == id && x.AssetId == assetId, ct)
                    ?? throw new NotFoundException("Maintenance not found.");
            Apply(m, dto);
            await _context.SaveChangesAsync(ct);
            await RecalculateAsync(asset, ct);
            await tx.CommitAsync(ct);
            return new MaintenanceResult(ToDto(m), asset.AssetValueTotal);
        }

        public async Task<MaintenanceResult> DeleteAsync(int assetId, int id, ClaimsPrincipal u, CancellationToken ct)
        {
            var asset = await LoadForWriteAsync(assetId, u, ct);

            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            await _context.LockAssetRowAsync(assetId, ct);

            var m = await _context.AssetMaintenances.FirstOrDefaultAsync(x => x.Id == id && x.AssetId == assetId, ct)
                    ?? throw new NotFoundException("Maintenance not found.");
            _context.AssetMaintenances.Remove(m);
            await _context.SaveChangesAsync(ct);
            await RecalculateAsync(asset, ct);
            await tx.CommitAsync(ct);
            return new MaintenanceResult(null, asset.AssetValueTotal);
        }
        #endregion

        #region Private
        private async Task<Asset> LoadForWriteAsync(int assetId, ClaimsPrincipal u, CancellationToken ct)
        {
            var asset = await _assets.FindVisibleAsync(assetId, u, ct);
            if (!_assets.CanModify(asset, u)) throw new ForbiddenException("You only have read access for this asset.");
            if (!AssetRules.HasMaintenance(asset.Subcategory))
                throw new BusinessRuleException("Asset furniture do not have maintenance.");
            return asset;
        }

        private async Task RecalculateAsync(Asset asset, CancellationToken ct)
        {
            var costs = await _context.AssetMaintenances.Where(x => x.AssetId == asset.Id).Select(x => x.Cost).ToListAsync(ct);
            asset.AssetValueTotal = costs.Sum();
            await _context.SaveChangesAsync(ct);
        }

        private static void Apply(AssetMaintenance m, MaintenanceUpsertDto d)
        {
            m.Cost = d.Cost; m.Type = d.Type; m.Date = d.Date;
            m.Vendor = string.IsNullOrWhiteSpace(d.Vendor) ? null : d.Vendor.Trim();
            m.Comments = string.IsNullOrWhiteSpace(d.Comments) ? null : d.Comments.Trim();
        }

        private static MaintenanceDto ToDto(AssetMaintenance m) => new(m.Id, m.AssetId, m.Cost, m.Type, m.Comments, m.Vendor, m.Date);
        #endregion
    }
}
