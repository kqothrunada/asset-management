using AssetManagement.Application.Dtos;
using AssetManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers
{
    [ApiController]
    [Route("api/assets/{assetId:int}/maintenance")]
    [Authorize]
    public class MaintenancesController : ControllerBase
    {
        private readonly MaintenanceService _svc;
        public MaintenancesController(MaintenanceService svc) => _svc = svc;

        [HttpGet, Authorize(Policy = "assets:view")]
        public async Task<ActionResult<List<MaintenanceDto>>> List(int assetId, CancellationToken ct)
            => Ok(await _svc.ListAsync(assetId, User, ct));

        [HttpPost, Authorize(Policy = "assets:edit")]
        public async Task<ActionResult<MaintenanceResult>> Add(int assetId, MaintenanceUpsertDto dto, CancellationToken ct)
            => StatusCode(201, await _svc.AddAsync(assetId, dto, User, ct));

        [HttpPut("{id:int}"), Authorize(Policy = "assets:edit")]
        public async Task<ActionResult<MaintenanceResult>> Update(int assetId, int id, MaintenanceUpsertDto dto, CancellationToken ct)
            => Ok(await _svc.UpdateAsync(assetId, id, dto, User, ct));

        [HttpDelete("{id:int}"), Authorize(Policy = "assets:edit")]
        public async Task<ActionResult<MaintenanceResult>> Delete(int assetId, int id, CancellationToken ct)
            => Ok(await _svc.DeleteAsync(assetId, id, User, ct));
    }
}
