using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using PhotoSite.Api.Models;
using PhotoSite.Api.DTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;

namespace PhotoSite.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly IConfiguration _config;

        public AuthController(UserManager<AppUser> userManager, IConfiguration config)
        {
            _userManager = userManager;
            _config = config;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _userManager.FindByNameAsync(dto.Username);
            if (user == null)
                return Unauthorized("Неверный логин или пароль");

            var valid = await _userManager.CheckPasswordAsync(user, dto.Password);
            if (!valid)
                return Unauthorized("Неверный логин или пароль");

            var token = GenerateJwt(user);
            return Ok(new { token });
        }

        // ✅ Проверка валидности JWT + базовая информация о пользователе
        [Authorize]
        [HttpGet("me")]
        public IActionResult Me()
        {
            var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var username = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Name);

            return Ok(new
            {
                id,
                username
            });
        }

        private string GenerateJwt(AppUser user)
        {
            var claims = new[]
            {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.UserName!)
        };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_config["Jwt:Key"]!)
            );

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(
                    int.Parse(_config["Jwt:ExpireMinutes"]!)
                ),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }

}
