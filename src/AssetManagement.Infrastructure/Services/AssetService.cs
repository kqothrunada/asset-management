using AssetManagement.Application;
using AssetManagement.Application.Abstractions;
using AssetManagement.Application.Dtos;
using AssetManagement.Application.Exceptions;
using AssetManagement.Domain;
using AssetManagement.Infrastructure.Data;
using ClosedXML.Excel;
using FluentValidation;
using Ganss.Xss;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AssetManagement.Infrastructure.Services
{
    public class AssetService
    {
        private readonly AppDbContext _context;
        private readonly IAssetNumberGenerator _num;
        private readonly IValidator<AssetUpsertDto> _validator;
        private readonly HtmlSanitizer _sanitizer = new();

        public AssetService(AppDbContext context, IAssetNumberGenerator num, IValidator<AssetUpsertDto> validator)
        {
            _context = context; 
            _num = num; 
            _validator = validator;
        }

        public IQueryable<Asset> Visible(ClaimsPrincipal u)
        {
            var uid = u.GetUserId();
            var divId = u.GetDivisionId();
            var isIt = u.IsItDepartment();
            var viewAll = u.IsInRole(AppConstants.CanViewAllAssets);

            return _context.Assets.Where(a => viewAll
                || a.CreatedById == uid
                || (isIt && a.CreatedByDivisionId == divId)      
                || a.Approvals.Any(x => x.ApproverId == uid));   
        }

        public bool CanModify(Asset a, ClaimsPrincipal u) => a.CreatedById == u.GetUserId()
            || (u.IsItDepartment() && a.CreatedByDivisionId == u.GetDivisionId());

        public async Task<Asset> FindVisibleAsync(int id, ClaimsPrincipal u, CancellationToken ct) =>
            await Visible(u).FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Asset is not found.");

        #region View
        public async Task<PagedResult<AssetListDto>> ListAsync(ClaimsPrincipal u, AssetListQuery q, CancellationToken ct)
        {
            var page = Math.Max(q.Page, 1);
            var size = Math.Clamp(q.PageSize, 1, 100);
            var query = pFiltered(u, q);

            var total = await query.CountAsync(ct);
            var items = await query.OrderBy(a => a.Id)
                .Skip((page - 1) * size).Take(size)
                .Select(a => new AssetListDto(a.Id, a.AssetCode, a.Name, a.Category, a.Subcategory, a.Status, a.AssetValueTotal))
                .ToListAsync(ct);

            return new PagedResult<AssetListDto>(items, total, page, size);
        }

        public async Task<AssetDetailDto> GetAsync(int id, ClaimsPrincipal u, CancellationToken ct)
        {
            var a = await Visible(u).FirstOrDefaultAsync(x => x.Id == id, ct)
                    ?? throw new NotFoundException("Asset not found.");
            return await pToDetailAsync(a, u, ct);
        }
        #endregion

        #region Transaction
        public async Task<AssetDetailDto> CreateAsync(AssetUpsertDto dto, ClaimsPrincipal u, CancellationToken ct)
        {
            dto.Status = AssetStatus.New;
            dto.Description = Sanitize(dto.Description);
            await _validator.ValidateAndThrowAsync(dto, ct);

            var uid = u.GetUserId();
            var requesterId = string.IsNullOrWhiteSpace(dto.RequesterId) ? uid : dto.RequesterId;
            await pEnsureUserExistsAsync(requesterId, "Requester", ct);

            var responsibleId = NullIfEmpty(dto.ResponsiblePersonId);
            if (responsibleId is not null) await pEnsureUserExistsAsync(responsibleId, "Responsible Person", ct);

            var asset = new Asset
            {
                AssetCode = await _num.NextAsync(ct),
                Name = dto.Name.Trim(),
                Description = dto.Description,
                RequesterId = requesterId,
                ResponsiblePersonId = responsibleId,
                Category = dto.Category,
                Subcategory = dto.Subcategory,
                Status = AssetStatus.New,
                CreatedById = uid,
                CreatedByDivisionId = u.GetDivisionId()
            };

            _context.Assets.Add(asset);
            await _context.SaveChangesAsync(ct);

            return await pToDetailAsync(asset, u, ct);
        }

        public async Task<AssetDetailDto> UpdateAsync(int id, AssetUpsertDto dto, ClaimsPrincipal u, CancellationToken ct)
        {
            var a = await Visible(u).FirstOrDefaultAsync(x => x.Id == id, ct)
                    ?? throw new NotFoundException("Asset not found.");
            
            if (!CanModify(a, u))
                throw new ForbiddenException("You have read-only access to this asset..");

            dto.Description = Sanitize(dto.Description);
            await _validator.ValidateAndThrowAsync(dto, ct);

            if (AssetRules.IsFurniture(dto.Subcategory)
                && await _context.AssetMaintenances.AnyAsync(m => m.AssetId == id, ct))
                throw new BusinessRuleException("The asset still has maintenance records. Delete those records before changing the subcategory to furniture.");

            if (dto.Status != AssetStatus.Assigned
                && !string.IsNullOrWhiteSpace(dto.RequesterId)
                && dto.RequesterId != a.RequesterId)
            {
                await pEnsureUserExistsAsync(dto.RequesterId, "Requester", ct);
                a.RequesterId = dto.RequesterId;
            }

            var responsibleId = NullIfEmpty(dto.ResponsiblePersonId);
            if (responsibleId is not null && responsibleId != a.ResponsiblePersonId)
                await pEnsureUserExistsAsync(responsibleId, "Responsible Person", ct);
            a.ResponsiblePersonId = responsibleId;

            a.Name = dto.Name.Trim();
            a.Description = dto.Description;
            a.Category = dto.Category;
            a.Subcategory = dto.Subcategory;
            a.Status = dto.Status;

            await _context.SaveChangesAsync(ct);
            return await pToDetailAsync(a, u, ct);
        }

        public async Task DeleteAsync(int id, ClaimsPrincipal u, CancellationToken ct)
        {
            var a = await Visible(u).FirstOrDefaultAsync(x => x.Id == id, ct)
                    ?? throw new NotFoundException("Asset not found.");
            if (!CanModify(a, u))
                throw new ForbiddenException("You do not have the privilege to delete this asset.");

            _context.Assets.Remove(a);      
            await _context.SaveChangesAsync(ct);
        }
        #endregion

        #region Export
        public async Task<byte[]> ExportAsync(ClaimsPrincipal u, AssetListQuery q, CancellationToken ct)
        {
            var rows = await pFiltered(u, q).OrderBy(a => a.Id).ToListAsync(ct);

            var userIds = rows.SelectMany(r => new[] { r.RequesterId, r.ResponsiblePersonId })
                              .Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
            var names = await _context.Users.Where(x => userIds.Contains(x.Id))
                                       .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Assets");

            var headers = new[] { "Asset ID", "Name", "Category", "Subcategory", "Status",
                              "Requester", "Responsible Person", "Total Value", "Created At" };
            for (var i = 0; i < headers.Length; i++) ws.Cell(1, i + 1).Value = headers[i];
            ws.Row(1).Style.Font.Bold = true;

            var r = 2;
            foreach (var a in rows)
            {
                ws.Cell(r, 1).SetValue(a.AssetCode);
                ws.Cell(r, 2).SetValue(Safe(a.Name));
                ws.Cell(r, 3).SetValue(a.Category.ToString());
                ws.Cell(r, 4).SetValue(a.Subcategory.ToString());
                ws.Cell(r, 5).SetValue(a.Status.ToString());
                ws.Cell(r, 6).SetValue(Safe(names.GetValueOrDefault(a.RequesterId) ?? ""));
                ws.Cell(r, 7).SetValue(Safe(a.ResponsiblePersonId is null ? "" : names.GetValueOrDefault(a.ResponsiblePersonId) ?? ""));
                ws.Cell(r, 8).SetValue(a.AssetValueTotal);
                ws.Cell(r, 8).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(r, 9).SetValue(a.CreatedAt);
                ws.Cell(r, 9).Style.DateFormat.Format = "yyyy-MM-dd HH:mm";
                r++;
            }

            ws.Columns().AdjustToContents();
            using var ms = new MemoryStream();
            wb.SaveAs(ms);

            return ms.ToArray();
        }
        #endregion

        #region Private
        private IQueryable<Asset> pFiltered(ClaimsPrincipal u, AssetListQuery q)
        {
            var query = Visible(u).AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q.Q))
            {
                var s = q.Q.Trim();
                query = query.Where(a => a.Name.Contains(s) || a.AssetCode.Contains(s));
            }

            if (q.Category is { } c) 
                query = query.Where(a => a.Category == c);

            if (q.Status is { } st) 
                query = query.Where(a => a.Status == st);

            return query;
        }

        private async Task<AssetDetailDto> pToDetailAsync(Asset a, ClaimsPrincipal u, CancellationToken ct)
        {
            var ids = new[] { a.RequesterId, a.ResponsiblePersonId }
                .Where(x => !string.IsNullOrEmpty(x)).Select(x => x!).Distinct().ToList();
            var names = await _context.Users.Where(x => ids.Contains(x.Id))
                                       .ToDictionaryAsync(x => x.Id, x => x.FullName, ct);

            return new AssetDetailDto(
                a.Id, a.AssetCode, a.Name, a.Description,
                a.RequesterId, names.GetValueOrDefault(a.RequesterId),
                a.ResponsiblePersonId, a.ResponsiblePersonId is null ? null : names.GetValueOrDefault(a.ResponsiblePersonId),
                a.Category, a.Subcategory, a.Status, a.AssetValueTotal,
                ShowMaintenance: AssetRules.HasMaintenance(a.Subcategory),
                RequesterReadOnly: a.Status == AssetStatus.Assigned,
                CanEdit: CanModify(a, u),
                a.CreatedById, a.CreatedAt);
        }

        private async Task pEnsureUserExistsAsync(string userId, string label, CancellationToken ct)
        {
            if (!await _context.Users.AnyAsync(x => x.Id == userId, ct))
                throw new BusinessRuleException($"{label} tidak ditemukan.");
        }

        private string? Sanitize(string? html) => string.IsNullOrWhiteSpace(html) ? null : _sanitizer.Sanitize(html);

        private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

        private static string Safe(string s) => s.Length > 0 && "=+-@".Contains(s[0]) ? "'" + s : s;
        #endregion
    }
}
