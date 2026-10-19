using AssetManagement.Api.Clients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers
{
    [ApiController, Route("api/integration-demo"), Authorize]
    public class IntegrationDemoController : ControllerBase
    {
        private readonly IEnumerable<IAssetClient> _clients;
        private readonly IConfiguration _cfg;
        public IntegrationDemoController(IEnumerable<IAssetClient> clients, IConfiguration cfg) 
        { 
            _clients = clients; 
            _cfg = cfg; 
        }

        public record DemoResult(string Protocol, bool Ok, AssetSummary? Data, string? Error);

        [HttpGet("{id:int}")]
        public async Task<IActionResult> Run(int id, CancellationToken ct)
        {
            var token = Request.Headers.Authorization.ToString()["Bearer ".Length..];
            var configured = _cfg["Integration:BaseUrl"];
            var baseUrl = string.IsNullOrWhiteSpace(configured)
                ? $"{Request.Scheme}://{Request.Host}"
                : configured.TrimEnd('/');
            var ctx = new ClientContext(baseUrl, token);

            var results = await Task.WhenAll(_clients.Select(async c =>
            {
                try { return new DemoResult(c.Protocol, true, await c.GetAssetAsync(id, ctx, ct), null); }
                catch (Exception e) { return new DemoResult(c.Protocol, false, null, e.Message); }
            }));

            var consistent = results.All(r => r.Ok) && results.Select(r => r.Data).Distinct().Count() == 1;
            return Ok(new { assetId = id, baseUrl, consistent, results });
        }
    }
}
