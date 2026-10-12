using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AssetManagement.Api.Controllers
{
    [ApiController]
    [Route("api/me")]
    [Authorize]
    public class MeController : ControllerBase
    {
        [HttpGet]
        public IActionResult Get() => Ok(new
        {
            claims = User.Claims.Select(c => new { c.Type, c.Value })
        });
    }
}
