using AssetManagement.Api.Controllers.Dtos;
using AssetManagement.Domain;
using AssetManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AssetManagement.Api.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<AppRole> _roleManager;
        private readonly AppDbContext _context;

        public UsersController(UserManager<AppUser> userManager, RoleManager<AppRole> roleManager, AppDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
        }

        #region View
        [HttpGet, Authorize(Policy = "users:view")]
        public async Task<IActionResult> List([FromQuery] string? q, CancellationToken ct)
        {
            var query = _context.Users.Include(u => u.Division).AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(u => u.UserName!.Contains(q) || u.FullName.Contains(q));

            var users = await query.OrderBy(u => u.FullName).ToListAsync(ct);

            var roleMap = await (from ur in _context.UserRoles
                                 join r in _context.Roles on ur.RoleId equals r.Id
                                 select new { ur.UserId, RoleName = r.Name! })
                                .ToListAsync(ct);
            var rolesByUser = roleMap.GroupBy(x => x.UserId)
                                     .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName).ToList());

            return Ok(users.Select(u => new UserListDto(
                u.Id, u.UserName!, u.FullName, u.Email, u.DivisionId, u.Division.Name, u.IsManager,
                rolesByUser.GetValueOrDefault(u.Id) ?? new List<string>())));
        }

        [HttpGet("{id}"), Authorize(Policy = "users:view")]
        public async Task<IActionResult> Get(string id, CancellationToken ct)
        {
            var u = await _context.Users.Include(x => x.Division).FirstOrDefaultAsync(x => x.Id == id, ct);
            if (u is null) return NotFound();

            var roles = (await _userManager.GetRolesAsync(u)).ToList();
            return Ok(new UserListDto(u.Id, u.UserName!, u.FullName, u.Email,
                                      u.DivisionId, u.Division.Name, u.IsManager, roles));
        }
        #endregion

        #region Create
        [HttpPost, Authorize(Policy = "users:add")]
        public async Task<IActionResult> Create(CreateUserDto dto, CancellationToken ct)
        {
            if (!await _context.Divisions.AnyAsync(d => d.Id == dto.DivisionId, ct))
                return BadRequest(new { message = "Division tidak ditemukan." });

            var badRole = await FindUnknownRoleAsync(dto.Roles);
            if (badRole is not null)
                return BadRequest(new { message = $"Role '{badRole}' tidak ditemukan." });

            var user = new AppUser
            {
                UserName = dto.UserName,
                Email = dto.Email,
                FullName = dto.FullName,
                DivisionId = dto.DivisionId,
                IsManager = dto.IsManager,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded) return IdentityError(result);

            if (dto.Roles.Count > 0)
            {
                var roleResult = await _userManager.AddToRolesAsync(user, dto.Roles);
                if (!roleResult.Succeeded)
                {
                    await _userManager.DeleteAsync(user);
                    return IdentityError(roleResult);
                }
            }

            return CreatedAtAction(nameof(Get), new { id = user.Id }, new { user.Id, user.UserName });
        }
        #endregion

        #region Update
        [HttpPut("{id}"), Authorize(Policy = "users:edit")]
        public async Task<IActionResult> Update(string id, UpdateUserDto dto, CancellationToken ct)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null) return NotFound();

            if (!await _context.Divisions.AnyAsync(d => d.Id == dto.DivisionId, ct))
                return BadRequest(new { message = "Division tidak ditemukan." });

            var badRole = await FindUnknownRoleAsync(dto.Roles);
            if (badRole is not null)
                return BadRequest(new { message = $"Role '{badRole}' tidak ditemukan." });

            if (id == CurrentUserId && !dto.Roles.Contains("Admin")
                && await _userManager.IsInRoleAsync(user, "Admin"))
                return BadRequest(new { message = "Anda tidak dapat mencabut role Admin dari akun sendiri." });

            user.FullName = dto.FullName;
            user.Email = dto.Email;
            user.DivisionId = dto.DivisionId;
            user.IsManager = dto.IsManager;

            var update = await _userManager.UpdateAsync(user);
            if (!update.Succeeded) return IdentityError(update);

            var current = await _userManager.GetRolesAsync(user);
            var toRemove = current.Except(dto.Roles).ToList();
            var toAdd = dto.Roles.Except(current).ToList();

            if (toRemove.Count > 0)
            {
                var r = await _userManager.RemoveFromRolesAsync(user, toRemove);
                if (!r.Succeeded) return IdentityError(r);
            }
            if (toAdd.Count > 0)
            {
                var r = await _userManager.AddToRolesAsync(user, toAdd);
                if (!r.Succeeded) return IdentityError(r);
            }

            if (!string.IsNullOrWhiteSpace(dto.NewPassword))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var r = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);
                if (!r.Succeeded) return IdentityError(r);
            }

            return NoContent();
        }
        #endregion

        #region Delete
        [HttpDelete("{id}"), Authorize(Policy = "users:delete")]
        public async Task<IActionResult> Delete(string id, CancellationToken ct)
        {
            if (id == CurrentUserId)
                return BadRequest(new { message = "Anda tidak dapat menghapus akun sendiri." });

            var user = await _userManager.FindByIdAsync(id);
            if (user is null) return NotFound();

            // User yang masih menjadi approver pending tidak boleh dihapus, workflow akan macet
            var hasPending = await _context.ApprovalRequests
                .AnyAsync(a => a.ApproverId == id && a.Status == ApprovalStatus.Pending, ct);
            if (hasPending)
                return Conflict(new { message = "User masih memiliki approval yang belum diputuskan." });

            var result = await _userManager.DeleteAsync(user);
            return result.Succeeded ? NoContent() : IdentityError(result);
        }
        #endregion

        #region Helper
        private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

        private async Task<string?> FindUnknownRoleAsync(IEnumerable<string> names)
        {
            foreach (var n in names.Distinct())
                if (!await _roleManager.RoleExistsAsync(n)) return n;
            return null;
        }

        private BadRequestObjectResult IdentityError(IdentityResult r) =>
            BadRequest(new { message = string.Join("; ", r.Errors.Select(e => e.Description)) });
        #endregion
    }
}
