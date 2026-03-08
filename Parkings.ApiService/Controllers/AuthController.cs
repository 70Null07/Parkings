using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parkings.ApiService.ApiDTOs;

namespace Parkings.ApiService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(AppDbContext db, IPasswordHasher<User> passwordHasher, JwtService jwt) : ControllerBase
    {
        private readonly AppDbContext _db = db;
        private readonly IPasswordHasher<User> _passwordHasher = passwordHasher;
        private readonly JwtService _jwt = jwt;

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] ApiDTOs.RegisterRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var existsName = await _db.Users.AnyAsync(u => u.Login == req.Login);
            if (existsName) return BadRequest("Имя уже занято");

            var user = new User
            {
                Login = req.Login
            };

            user.Password = _passwordHasher.HashPassword(user, req.Password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var dto = new UserDto { UserId = user.Id, DisplayName = user.Login, Email = user.Email };
            return Ok(dto);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] ApiDTOs.LoginRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _db.Users.Where(u => (u.Login == req.Login))
                .FirstOrDefaultAsync();

            if (user == null)
                return Unauthorized("Неверный логин или пароль");

            var verify = _passwordHasher.VerifyHashedPassword(user, user.Password ?? "", req.Password);
            if (verify == PasswordVerificationResult.Failed)
                return Unauthorized("Неверный логин или пароль");

            var token = _jwt.GenerateToken(user.Id, user.Login);

            var dto = new UserDto
            {
                UserId = user.Id,
                DisplayName = user.Login,
                Email = user.Email
            };

            return Ok(new
            {
                user = dto,
                token
            });
        }

        [HttpGet("validate")]
        [Authorize]
        public IActionResult ValidateToken()
        {
            return Ok(new { Valid = true });
        }
    }
}
