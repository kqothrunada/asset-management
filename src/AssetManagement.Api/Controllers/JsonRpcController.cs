using AssetManagement.Application.Dtos;
using AssetManagement.Application.Exceptions;
using AssetManagement.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace AssetManagement.Api.Controllers
{
    [ApiController]
    [Route("rpc/json")]
    [Authorize]
    public class JsonRpcController : ControllerBase
    {
        private readonly AssetService _svc;
        public JsonRpcController(AssetService svc) => _svc = svc;

        [HttpPost, Consumes("application/json")]
        public async Task<IActionResult> Post(CancellationToken ct)
        {
            JsonDocument doc;
            try { doc = await JsonDocument.ParseAsync(Request.Body, cancellationToken: ct); }
            catch (JsonException) { return Ok(pErr(null, -32700, "Parse error")); }

            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !root.TryGetProperty("method", out var m) || m.ValueKind != JsonValueKind.String)
                    return Ok(pErr(null, -32600, "Invalid Request"));

                var hasId = root.TryGetProperty("id", out var idEl);
                object? id = hasId ? idEl.Clone() : null;   
                root.TryGetProperty("params", out var p);

                IActionResult Reply(object r) => hasId ? Ok(r) : NoContent();   

                try
                {
                    switch (m.GetString())
                    {
                        case "asset.get":
                            if (!pTryGetId(p, out var assetId)) return Reply(pErr(id, -32602, "Invalid params: 'id' (int) is required"));
                            return Reply(pResult(id, await _svc.GetAsync(assetId, User, ct)));
                        case "asset.list":
                            var page = await _svc.ListAsync(User, new AssetListQuery { PageSize = 100 }, ct);
                            return Reply(pResult(id, page.Items));
                        default:
                            return Reply(pErr(id, -32601, "Method not found"));
                    }
                }
                catch (NotFoundException e) { return Reply(pErr(id, -32001, e.Message)); }
                catch (ForbiddenException e) { return Reply(pErr(id, -32003, e.Message)); }
            }
        }

        #region Private
        private static bool pTryGetId(JsonElement p, out int id)
        {
            id = 0;
            if (p.ValueKind == JsonValueKind.Object && p.TryGetProperty("id", out var v))
                return v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out id);
            if (p.ValueKind == JsonValueKind.Array && p.GetArrayLength() > 0)
                return p[0].ValueKind == JsonValueKind.Number && p[0].TryGetInt32(out id);
            return false;
        }
        private static object pResult(object? id, object? result) => new { jsonrpc = "2.0", id, result };
        private static object pErr(object? id, int code, string message) => new { jsonrpc = "2.0", id, error = new { code, message } };
        #endregion
    }
}
