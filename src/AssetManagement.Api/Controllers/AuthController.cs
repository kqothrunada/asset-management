using AssetManagement.Domain;
using AssetManagement.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AssetManagement.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        public AuthController(UserManager<AppUser> um, AppDbContext db, IConfiguration cfg)
        {
            _userManager = um;
            _db = db;
            _config = cfg;
        }

        public record LoginRequest(string userName, string password);

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest req)
        {
            var user = await _db.Users.Include(x => x.Division)
                                      .FirstOrDefaultAsync(x => x.UserName == req.userName);

            if (user is null || !await _userManager.CheckPasswordAsync(user, req.password))
                return Unauthorized(new { message = "Username atau password salah." });

            var roles = await _userManager.GetRolesAsync(user);
            var roleIds = await _db.Roles.Where(x => roles.Contains(x.Name!))
                                         .Select(x => x.Id).ToListAsync();

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.FullName),
                new("division_id", user.DivisionId.ToString()),
                new("division_name", user.Division.Name),
                new("is_manager", user.IsManager.ToString()),
            };
            claims.AddRange(roles.Select(x => new Claim(ClaimTypes.Role, x)));
            claims.AddRange(roleIds.Select(x => new Claim("role_id", x)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(8),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            );

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token),
                name = user.FullName,
                division = user.Division.Name,
                isManager = user.IsManager,
                roles
            });
        }
    }
}
