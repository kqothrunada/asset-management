using AssetManagement.Application.Dtos;
using AssetManagement.Application.Exceptions;
using AssetManagement.Domain;
using AssetManagement.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AssetManagement.Infrastructure.Services
{
    public class ApprovalService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ApprovalService> _log;
        public ApprovalService(AppDbContext context, ILogger<ApprovalService> log) 
        { 
            _context = context; 
            _log = log; 
        }

        public async Task StartReviewAsync(Asset a, CancellationToken ct)
        {
            var requester = await _context.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == a.RequesterId, ct)
                            ?? throw new BusinessRuleException("Requester is not found.");
            var head = await _context.Users.AsNoTracking()
                            .Where(x => x.DivisionId == requester.DivisionId && x.IsManager)
                            .OrderBy(x => x.FullName).FirstOrDefaultAsync(ct)
                       ?? throw new BusinessRuleException("Department Head for requester's division is not found.");
            var managerId = await (from ur in _context.UserRoles
                                   join r in _context.Roles on ur.RoleId equals r.Id
                                   where r.Name == "AssetManager"
                                   select ur.UserId).FirstOrDefaultAsync(ct)
                            ?? throw new BusinessRuleException("There is no user with the Asset Manager role.");

            _context.ApprovalRequests.RemoveRange(await _context.ApprovalRequests.Where(x => x.AssetId == a.Id).ToListAsync(ct));
            _context.ApprovalRequests.AddRange(
                new ApprovalRequest { AssetId = a.Id, Role = "Department Head", ApproverId = head.Id },
                new ApprovalRequest { AssetId = a.Id, Role = "Asset Manager", ApproverId = managerId });
        }

        public async Task<List<ApprovalDto>> ListAsync(int assetId, string userId, CancellationToken ct)
        {
            var assetStatus = await _context.Assets.Where(x => x.Id == assetId).Select(x => x.Status).FirstAsync(ct);
            var rows = await _context.ApprovalRequests.AsNoTracking().Where(x => x.AssetId == assetId).OrderBy(x => x.Id).ToListAsync(ct);
            var ids = rows.Select(r => r.ApproverId).Distinct().ToList();
            var names = await _context.Users.Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);

            return rows.Select(r => new ApprovalDto(r.Id, r.Role, r.ApproverId, names.GetValueOrDefault(r.ApproverId) ?? "-",
                r.Status, r.Notes, r.DecidedAt,
                CanDecide: r.ApproverId == userId && r.Status == ApprovalStatus.Pending && assetStatus == AssetStatus.UnderReview)).ToList();
        }

        public async Task DecideAsync(int approvalId, bool approve, string? notes, string userId, CancellationToken ct)
        {
            var assetId = await _context.ApprovalRequests.Where(x => x.Id == approvalId).Select(x => (int?)x.AssetId).FirstOrDefaultAsync(ct)
                          ?? throw new NotFoundException("Approval is not found.");

            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            await _context.LockAssetRowAsync(assetId, ct);

            var req = await _context.ApprovalRequests.Include(x => x.Asset).FirstAsync(x => x.Id == approvalId, ct);
            if (req.ApproverId != userId) throw new ForbiddenException("You are not the approver for this request.");
            if (req.Status != ApprovalStatus.Pending) throw new BusinessRuleException("This approval has been decided.");
            if (req.Asset.Status != AssetStatus.UnderReview) throw new BusinessRuleException("Asset is not in review.");

            req.Status = approve ? ApprovalStatus.Approved : ApprovalStatus.Rejected;
            req.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            req.DecidedAt = DateTime.UtcNow;

            var all = await _context.ApprovalRequests.Where(x => x.AssetId == assetId).ToListAsync(ct); 
            var before = req.Asset.Status;
            if (all.Any(x => x.Status == ApprovalStatus.Rejected)) req.Asset.Status = AssetStatus.Discarded;
            else if (all.All(x => x.Status == ApprovalStatus.Approved)) req.Asset.Status = AssetStatus.Assigned;

            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            _log.LogInformation("Approval {Role} {Decision} by {User} for {Code}; status {Old}→{New}",
                req.Role, req.Status, userId, req.Asset.AssetCode, before, req.Asset.Status);
        }
    }
}
