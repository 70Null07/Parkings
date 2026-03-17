using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parkings.ApiService.ApiDTOs;
using Parkings.ApiService.Services;
using System.Security.Claims;

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
        public async Task<IActionResult> Register(
            [FromBody] ApiDTOs.RegisterRequest req,
            [FromServices] EmailService email)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var existsName = await _db.Users.AnyAsync(u => u.Login == req.Login);
            if (existsName) return BadRequest("Имя уже занято");

            // ← генерируем код
            var code = email.GenerateCode();

            var user = new User
            {
                Login = req.Login,
                Email = req.Email,
                EmailConfirmCode = code,
                EmailConfirmed = false
            };

            user.Password = _passwordHasher.HashPassword(user, req.Password);

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            // ← отправляем письмо
            await email.SendAsync(req.Email, "Подтверждение регистрации",
                $"Ваш код подтверждения: {code}");

            return Ok(new { userId = user.Id, needConfirm = true });
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

        // POST api/auth/confirm-email
        [HttpPost("confirm-email")]
        public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailRequest req)
        {
            var user = await db.Users.FindAsync(req.UserId);
            if (user == null) return NotFound();
            if (user.EmailConfirmCode != req.Code) return BadRequest("Неверный код");

            user.EmailConfirmed = true;
            user.EmailConfirmCode = null;
            await db.SaveChangesAsync();

            // Возвращаем токен как при логине
            var token = _jwt.GenerateToken(user.Id, user.Login);
            return Ok(new { token, user = new { userId = user.Id, displayName = user.Login } });
        }

        // POST api/auth/forgot-password
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(
            [FromBody] ApiDTOs.ForgotPasswordRequest req,
            [FromServices] EmailService email)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
            if (user == null) return Ok(); // не раскрываем существование

            var code = email.GenerateCode();
            user.PasswordResetCode = code;
            user.PasswordResetCodeExpiry = DateTime.UtcNow.AddMinutes(15);
            await db.SaveChangesAsync();

            await email.SendAsync(req.Email, "Сброс пароля",
                $"Код для сброса пароля: {code}\nДействует 15 минут.");

            return Ok();
        }

        // POST api/auth/reset-password
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ApiDTOs.ResetPasswordRequest req)
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == req.Email);
            if (user == null) return BadRequest("Пользователь не найден");

            if (user.PasswordResetCode != req.Code)
                return BadRequest("Неверный код");

            if (user.PasswordResetCodeExpiry < DateTime.UtcNow)
                return BadRequest("Код истёк");

            user.Password = _passwordHasher.HashPassword(null!, req.NewPassword);
            user.PasswordResetCode = null;
            user.PasswordResetCodeExpiry = null;
            await db.SaveChangesAsync();

            var token = _jwt.GenerateToken(user.Id, user.Login);
            return Ok(new { token, user = new { userId = user.Id, displayName = user.Login } });
        }

        // POST api/auth/change-password — для авторизованных
        [HttpPost("change-password")]
        [Authorize]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
        {
            var userId = int.Parse(User.FindFirstValue("uid") ?? "0");
            var user = await db.Users.FindAsync(userId);
            if (user == null) return NotFound();

            var result = _passwordHasher.VerifyHashedPassword(null!, user.Password, req.OldPassword);
            if (result == PasswordVerificationResult.Failed)
                return BadRequest("Неверный текущий пароль");

            if (req.NewPassword != req.ConfirmPassword)
                return BadRequest("Пароли не совпадают");

            user.Password = _passwordHasher.HashPassword(null!, req.NewPassword);
            await db.SaveChangesAsync();
            return Ok();
        }

        // POST api/auth/request-email-change — отправить код на новый email
        [HttpPost("request-email-change")]
        [Authorize]
        public async Task<IActionResult> RequestEmailChange(
            [FromBody] RequestEmailChangeRequest req,
            [FromServices] EmailService email)
        {
            var userId = int.Parse(User.FindFirstValue("uid") ?? "0");
            var user = await db.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (await db.Users.AnyAsync(u => u.Email == req.NewEmail && u.Id != userId))
                return BadRequest("Email уже используется");

            var code = email.GenerateCode();
            user.EmailConfirmCode = code;
            user.EmailConfirmed = false;
            await db.SaveChangesAsync();

            await email.SendAsync(req.NewEmail, "Подтверждение смены email",
                $"Код подтверждения: {code}\nЕсли вы не запрашивали смену email — проигнорируйте письмо.");

            return Ok();
        }

        // POST api/auth/confirm-email-change — подтвердить смену
        [HttpPost("confirm-email-change")]
        [Authorize]
        public async Task<IActionResult> ConfirmEmailChange([FromBody] ConfirmEmailChangeRequest req)
        {
            var userId = int.Parse(User.FindFirstValue("uid") ?? "0");
            var user = await db.Users.FindAsync(userId);
            if (user == null) return NotFound();

            if (user.EmailConfirmCode != req.Code)
                return BadRequest("Неверный код");

            user.Email = req.NewEmail;
            user.EmailConfirmed = true;
            user.EmailConfirmCode = null;
            await db.SaveChangesAsync();

            return Ok(new { email = user.Email });
        }
    }
}
