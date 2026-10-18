using AssetManagement.Application;
using AssetManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AssetManagement.Api.Controllers
{
    [ApiController]
    [Route("api/lookups")]
    [Authorize]
    public class LookupsController : ControllerBase
    {
        private readonly AppDbContext _context;
        public LookupsController(AppDbContext context) => _context = context;

        [HttpGet("users")]
        public async Task<IActionResult> Users(CancellationToken ct) =>
        Ok(await _context.Users.AsNoTracking()
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName, Division = u.Division.Name })
            .ToListAsync(ct));

        [HttpGet("categories")]
        public IActionResult Categories() =>
        Ok(AssetRules.SubcategoriesByCategory.Select(kv => new
        {
            category = kv.Key.ToString(),
            subcategories = kv.Value.Select(s => new
            {
                value = s.ToString(),
                hasMaintenance = AssetRules.HasMaintenance(s)
            })
        }));
    }
}
