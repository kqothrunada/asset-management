using AssetManagement.Application.Dtos;
using AssetManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers
{
    [ApiController]
    [Route("api/assets")]
    [Authorize]
    public class AssetsController : ControllerBase
    {
        private readonly AssetService _svc;
        public AssetsController(AssetService svc) => _svc = svc;

        [HttpGet, Authorize(Policy = "assets:view")]
        public async Task<ActionResult<PagedResult<AssetListDto>>> List([FromQuery] AssetListQuery q, CancellationToken ct)
            => Ok(await _svc.ListAsync(User, q, ct));

        [HttpGet("{id:int}"), Authorize(Policy = "assets:view")]
        public async Task<ActionResult<AssetDetailDto>> Get(int id, CancellationToken ct)
        => Ok(await _svc.GetAsync(id, User, ct));

        [HttpPost, Authorize(Policy = "assets:add")]
        public async Task<ActionResult<AssetDetailDto>> Create(AssetUpsertDto dto, CancellationToken ct)
        {
            var created = await _svc.CreateAsync(dto, User, ct);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }

        [HttpPut("{id:int}"), Authorize(Policy = "assets:edit")]
        public async Task<ActionResult<AssetDetailDto>> Update(int id, AssetUpsertDto dto, CancellationToken ct)
        => Ok(await _svc.UpdateAsync(id, dto, User, ct));

        [HttpDelete("{id:int}"), Authorize(Policy = "assets:delete")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            await _svc.DeleteAsync(id, User, ct);
            return NoContent();
        }

        [HttpGet("export"), Authorize(Policy = "assets:export")]
        public async Task<IActionResult> Export([FromQuery] AssetListQuery q, CancellationToken ct)
        {
            var bytes = await _svc.ExportAsync(User, q, ct);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"assets-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx");
        }
    }
}
