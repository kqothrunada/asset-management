using AssetManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Api.Controllers
{
    [ApiController]
    [Route("api/me")]
    [Authorize]
    public class MeController : ControllerBase
    {
        private readonly AppDbContext _context;
        public MeController(AppDbContext context) => _context = context;

        [HttpGet]
        public IActionResult Get() => Ok(new
        {
            claims = User.Claims.Select(c => new { c.Type, c.Value })
        });

        [HttpGet("menu")]
        public async Task<IActionResult> Menu(CancellationToken ct)
        {
            var roleIds = User.FindAll("role_id").Select(c => c.Value).ToList();
            var permission = await _context.RoleMenuPermissions
                .Where(p => roleIds.Contains(p.RoleId))
                .ToListAsync(ct);
            var menus = await _context.Menus.OrderBy(m => m.Order).ToListAsync(ct);

            var result = menus.Select(m =>
            {
                var p = permission.Where(x => x.MenuKey == m.Key).ToList();
                return new
                {
                    m.Key,
                    m.Title,
                    m.Route,
                    view = p.Any(x => x.CanView),
                    add = p.Any(x => x.CanAdd),
                    edit = p.Any(x => x.CanEdit),
                    delete = p.Any(x => x.CanDelete)
                };
            }).Where(x => x.view);

            return Ok(result);
        }
    }
}
