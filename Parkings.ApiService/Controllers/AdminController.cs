using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Parkings.ApiService.Controllers
{
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = "superadmin,admin")]
    public class AdminController(AppDbContext db,
        IPasswordHasher<User> hasher,
        JwtService jwt) : ControllerBase
    {
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers([FromQuery] string? search)
        {
            var q = db.Users.Where(u => !u.IsDeleted);
            if (!string.IsNullOrEmpty(search))
                q = q.Where(u => u.Login.Contains(search)
                              || (u.Email != null && u.Email.Contains(search)));
            var list = await q.Select(u => new {
                u.Id,
                u.Login,
                u.Email,
                u.Role,
                u.IsBlocked,
                u.IsDeleted,
                u.EmailConfirmed,
                ParkingCount = u.Parkings.Count
            }).ToListAsync();
            return Ok(list);
        }

        [HttpPost("users/{id:int}/block")]
        public async Task<IActionResult> BlockUser(int id)
        {
            var user = await db.Users.FindAsync(id);
            if (user == null) return NotFound();
            user.IsBlocked = true;
            user.BlockedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("users/{id:int}/unblock")]
        public async Task<IActionResult> UnblockUser(int id)
        {
            var user = await db.Users.FindAsync(id);
            if (user == null) return NotFound();
            user.IsBlocked = false;
            user.BlockedAt = null;
            await db.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("users/{id:int}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await db.Users.FindAsync(id);
            if (user == null) return NotFound();
            user.IsDeleted = true;
            await db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("users/{id:int}/role")]
        public async Task<IActionResult> SetRole(int id,
            [FromBody] SetRoleRequest req)
        {
            var allowed = new[] { "user", "admin", "superadmin" };
            if (!allowed.Contains(req.Role)) return BadRequest("Неверная роль");
            var user = await db.Users.FindAsync(id);
            if (user == null) return NotFound();
            user.Role = req.Role;
            await db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("users/{id:int}/reset-password")]
        public async Task<IActionResult> ResetPassword(int id,
            [FromBody] AdminResetPasswordRequest req)
        {
            var user = await db.Users.FindAsync(id);
            if (user == null) return NotFound();
            user.Password = hasher.HashPassword(null!, req.NewPassword);
            await db.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("parkings")]
        public async Task<IActionResult> GetAllParkings([FromQuery] string? search)
        {
            var q = db.Parkings.Include(p => p.User).AsQueryable();
            if (!string.IsNullOrEmpty(search))
                q = q.Where(p => p.Name.Contains(search)
                              || p.Address.Contains(search));
            var list = await q.Select(p => new {
                p.Id,
                p.Name,
                p.Address,
                p.IsDisabled,
                p.IsApproved,
                p.FreeSpots,
                p.BusySpots,
                p.LastCheckedAt,
                Owner = p.User.Login,
                OwnerId = p.UserId
            }).ToListAsync();
            return Ok(list);
        }

        [HttpPost("parkings/{id:int}/disable")]
        public async Task<IActionResult> DisableParking(int id)
        {
            var p = await db.Parkings.FindAsync(id);
            if (p == null) return NotFound();
            p.IsDisabled = true;
            await db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("parkings/{id:int}/enable")]
        public async Task<IActionResult> EnableParking(int id)
        {
            var p = await db.Parkings.FindAsync(id);
            if (p == null) return NotFound();
            p.IsDisabled = false;
            await db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("parkings/{id:int}/approve")]
        public async Task<IActionResult> ApproveParking(int id)
        {
            var p = await db.Parkings.FindAsync(id);
            if (p == null) return NotFound();
            p.IsApproved = true;
            await db.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("parkings/{id:int}")]
        public async Task<IActionResult> DeleteParking(int id)
        {
            var p = await db.Parkings.FindAsync(id);
            if (p == null) return NotFound();
            db.Parkings.Remove(p);
            await db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("parkings/{id:int}/owner")]
        public async Task<IActionResult> ChangeOwner(int id,
            [FromBody] ChangeOwnerRequest req)
        {
            var p = await db.Parkings.FindAsync(id);
            if (p == null) return NotFound();
            var newOwner = await db.Users.FindAsync(req.NewOwnerId);
            if (newOwner == null) return BadRequest("Пользователь не найден");
            p.UserId = req.NewOwnerId;
            await db.SaveChangesAsync();
            return Ok();
        }

        [HttpPost("users/{id:int}/email")]
        public async Task<IActionResult> ForceChangeEmail(int id,
            [FromBody] ForceChangeEmailRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.NewEmail))
                return BadRequest("Email не может быть пустым");

            var user = await db.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (await db.Users.AnyAsync(u => u.Email == req.NewEmail && u.Id != id))
                return BadRequest("Email уже используется");

            user.Email = req.NewEmail;
            user.EmailConfirmed = true;
            user.EmailConfirmCode = null;
            await db.SaveChangesAsync();
            return Ok();
        }
    }

    public record SetRoleRequest(string Role);
    public record AdminResetPasswordRequest(string NewPassword);
    public record ChangeOwnerRequest(int NewOwnerId);

    public record ForceChangeEmailRequest(string NewEmail);
}
