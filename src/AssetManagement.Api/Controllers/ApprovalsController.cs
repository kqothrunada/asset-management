using AssetManagement.Application;
using AssetManagement.Application.Dtos;
using AssetManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers
{
    [ApiController, Authorize]
    public class ApprovalsController : ControllerBase
    {
        private readonly ApprovalService _approvals;
        private readonly AssetService _assets;
        public ApprovalsController(ApprovalService approval, AssetService asset) 
        { 
            _approvals = approval; 
            _assets = asset; 
        }

        public record DecisionDto(string? Notes);

        [HttpGet("api/assets/{assetId:int}/approvals"), Authorize(Policy = "assets:view")]
        public async Task<ActionResult<List<ApprovalDto>>> List(int assetId, CancellationToken ct)
        {
            await _assets.FindVisibleAsync(assetId, User, ct);          
            return Ok(await _approvals.ListAsync(assetId, User.GetUserId(), ct));
        }

        [HttpPost("api/approvals/{id:int}/approve")]
        public async Task<IActionResult> Approve(int id, DecisionDto dto, CancellationToken ct)
        {
            await _approvals.DecideAsync(id, true, dto.Notes, User.GetUserId(), ct);
            return NoContent();
        }

        [HttpPost("api/approvals/{id:int}/reject")]
        public async Task<IActionResult> Reject(int id, DecisionDto dto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dto.Notes)) return BadRequest(new { message = "Notes is required on reject." });
            await _approvals.DecideAsync(id, false, dto.Notes, User.GetUserId(), ct);
            return NoContent();
        }
    }
}
