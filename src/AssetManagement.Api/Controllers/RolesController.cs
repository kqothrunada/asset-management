using AssetManagement.Api.Controllers.Dtos;
using AssetManagement.Domain;
using AssetManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Api.Controllers
{
    [ApiController]
    [Route("api/roles")]
    public class RolesController : ControllerBase
    {
        private static readonly HashSet<string> Protected = new() { "Admin", "Manager", "User", "CanViewAllAssets", "AssetManager" };

        private readonly RoleManager<AppRole> _roleManager;
        private readonly AppDbContext _context;

        public RolesController(RoleManager<AppRole> roleManager, AppDbContext context)
        {
            _roleManager = roleManager; 
            _context = context;
        }

        [HttpGet, Authorize(Policy = "users:view")]
        public async Task<IActionResult> List(CancellationToken ct)
        {
            var counts = await _context.UserRoles.GroupBy(x => x.RoleId)
                .Select(g => new { RoleId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.RoleId, x => x.Count, ct);

            var roles = await _roleManager.Roles.OrderBy(r => r.Name).ToListAsync(ct);
            return Ok(roles.Select(r => new RoleListDto(r.Id, r.Name!, counts.GetValueOrDefault(r.Id))));
        }

        [HttpGet("{id}"), Authorize(Policy = "users:view")]
        public async Task<IActionResult> Get(string id)
        {
            var r = await _roleManager.FindByIdAsync(id);
            if (r is null) return NotFound();
            var count = await _context.UserRoles.CountAsync(x => x.RoleId == id);
            return Ok(new RoleListDto(r.Id, r.Name!, count));
        }

        [HttpPost, Authorize(Policy = "users:add")]
        public async Task<IActionResult> Create(RoleUpsertDto dto)
        {
            if (await _roleManager.RoleExistsAsync(dto.Name))
                return Conflict(new { message = "Nama role sudah dipakai." });

            var role = new AppRole { Name = dto.Name.Trim() };
            var result = await _roleManager.CreateAsync(role);
            if (!result.Succeeded) return IdentityError(result);

            return CreatedAtAction(nameof(Get), new { id = role.Id }, new RoleListDto(role.Id, role.Name!, 0));
        }

        [HttpPut("{id}"), Authorize(Policy = "users:edit")]
        public async Task<IActionResult> Update(string id, RoleUpsertDto dto)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role is null) return NotFound();

            if (Protected.Contains(role.Name!) && role.Name != dto.Name.Trim())
                return BadRequest(new { message = $"Role '{role.Name}' dipakai sistem dan namanya tidak dapat diubah." });

            var other = await _roleManager.FindByNameAsync(dto.Name.Trim());
            if (other is not null && other.Id != id)
                return Conflict(new { message = "Nama role sudah dipakai." });

            role.Name = dto.Name.Trim();
            var result = await _roleManager.UpdateAsync(role);
            return result.Succeeded ? NoContent() : IdentityError(result);
        }

        [HttpDelete("{id}"), Authorize(Policy = "users:delete")]
        public async Task<IActionResult> Delete(string id, CancellationToken ct)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role is null) return NotFound();

            if (Protected.Contains(role.Name!))
                return BadRequest(new { message = $"Role '{role.Name}' dipakai sistem dan tidak dapat dihapus." });

            if (await _context.UserRoles.AnyAsync(x => x.RoleId == id, ct))
                return Conflict(new { message = "Role masih dipakai oleh user. Lepaskan dari user terlebih dahulu." });

            var perms = await _context.RoleMenuPermissions.Where(p => p.RoleId == id).ToListAsync(ct);
            _context.RoleMenuPermissions.RemoveRange(perms);

            var result = await _roleManager.DeleteAsync(role);
            if (!result.Succeeded) return IdentityError(result);

            await _context.SaveChangesAsync(ct);
            return NoContent();
        }

        private BadRequestObjectResult IdentityError(IdentityResult r) =>
            BadRequest(new { message = string.Join("; ", r.Errors.Select(e => e.Description)) });
    }
}
