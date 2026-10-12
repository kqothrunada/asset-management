using AssetManagement.Domain;
using AssetManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Api.Controllers
{
    [ApiController]
    [Route("api/roles/{roleId}/permissions")]
    public class PermissionsController : ControllerBase
    {
        private readonly AppDbContext _context;
        public PermissionsController(AppDbContext context) => _context = context;

        public record PermissionsDto(string MenuKey, bool CanView, bool CanAdd, bool CanEdit, bool CanDelete);

        [HttpGet, Authorize(Policy = "users:view")]
        public async Task<IActionResult> Get(string roleId, CancellationToken ct)
        {
            var existing = await _context.RoleMenuPermissions.Where(p => p.RoleId == roleId).ToListAsync(ct);
            var menus = await _context.Menus.OrderBy(m => m.Order).ToListAsync(ct);

            return Ok(menus.Select(m =>
            {
                var p = existing.FirstOrDefault(x => x.MenuKey == m.Key);

                return new
                {
                    menuKey = m.Key,
                    title = m.Title,
                    canView = p?.CanView ?? false,
                    canAdd = p?.CanAdd ?? false,
                    canEdit = p?.CanEdit ?? false,
                    canDelete = p?.CanDelete ?? false
                };
            }));
        }

        [HttpPut, Authorize(Policy = "users:edit")]
        public async Task<IActionResult> Put(string roleId, List<PermissionsDto> items, CancellationToken ct)
        {
            if (!await _context.Roles.AnyAsync(r => r.Id == roleId, ct))
                return NotFound();

            var validMenus = await _context.Menus.Select(m => m.Key).ToListAsync(ct);
            if (items.Any(i => !validMenus.Contains(i.MenuKey)))
                return BadRequest(new { message = "MenuKey tidak ada." });

            var existing = await _context.RoleMenuPermissions.Where(p => p.RoleId == roleId).ToListAsync(ct);
            foreach (var i in items)
            {
                var row = existing.FirstOrDefault(x => x.MenuKey == i.MenuKey);
                if (row is null)
                {
                    _context.RoleMenuPermissions.Add(new RoleMenuPermission
                    {
                        RoleId = roleId,
                        MenuKey = i.MenuKey,
                        CanView = i.CanView,
                        CanAdd = i.CanAdd,
                        CanEdit = i.CanEdit,
                        CanDelete = i.CanDelete
                    });
                }
                else
                {
                    row.CanView = i.CanView; row.CanAdd = i.CanAdd;
                    row.CanEdit = i.CanEdit; row.CanDelete = i.CanDelete;
                }
            }

            await _context.SaveChangesAsync(ct);
            return NoContent();
        }
    }
}
