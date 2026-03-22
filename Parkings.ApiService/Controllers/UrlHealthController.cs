using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parkings.ApiService.Services;
using System.Security.Claims;

namespace Parkings.ApiService.Controllers
{
    [ApiController]
    [Route("api/parkings/{parkingId:int}/health")]
    public class UrlHealthController(AppDbContext db) : ControllerBase
    {
        // GET — история доступности для графика
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetHistory(
            int parkingId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var fromDate = from?.ToUniversalTime() ?? DateTime.UtcNow.AddDays(-1);
            var toDate = to?.ToUniversalTime() ?? DateTime.UtcNow;

            var logs = await db.UrlHealthLogs
                .Where(l => l.ParkingId == parkingId
                         && l.CheckedAt >= fromDate
                         && l.CheckedAt <= toDate)
                .OrderBy(l => l.CheckedAt)
                .Select(l => new {
                    l.IsAvailable,
                    l.ResponseMs,
                    l.ErrorMessage,
                    l.CheckedAt
                })
                .ToListAsync();

            // Uptime %
            var total = logs.Count;
            var up = logs.Count(l => l.IsAvailable);
            var uptime = total > 0 ? Math.Round(up * 100.0 / total, 1) : 0;

            return Ok(new { logs, uptime, total });
        }

        // POST — от Worker
        [HttpPost]
        public async Task<IActionResult> AddCheck(
            int parkingId,
            [FromBody] UrlHealthCheckRequest req,
            [FromHeader(Name = "X-Worker-Key")] string? workerKey,
            IConfiguration config)
        {
            if (workerKey != config["Worker:ApiKey"])
                return Unauthorized();

            db.UrlHealthLogs.Add(new UrlHealthLog
            {
                ParkingId = parkingId,
                IsAvailable = req.IsAvailable,
                ResponseMs = req.ResponseMs,
                ErrorMessage = req.ErrorMessage,
                CheckedAt = DateTime.UtcNow
            });

            // Удаляем старые записи старше 7 дней
            var cutoff = DateTime.UtcNow.AddDays(-7);
            await db.UrlHealthLogs
                .Where(l => l.ParkingId == parkingId && l.CheckedAt < cutoff)
                .ExecuteDeleteAsync();

            await db.SaveChangesAsync();
            return Ok();
        }

        // POST api/parkings/{id}/health/alert — Worker вызывает при недоступности
        [HttpPost("alert")]
        public async Task<IActionResult> CheckAlert(
    int parkingId,
    [FromHeader(Name = "X-Worker-Key")] string? workerKey,
    IConfiguration config,
    [FromServices] EmailService email)
        {
            if (workerKey != config["Worker:ApiKey"]) return Unauthorized();

            var parking = await db.Parkings
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == parkingId);
            if (parking == null) return NotFound();

            var since = DateTime.UtcNow.AddHours(-1);
            var recentLogs = await db.UrlHealthLogs
                .Where(l => l.ParkingId == parkingId && l.CheckedAt >= since)
                .OrderBy(l => l.CheckedAt)
                .ToListAsync();

            if (recentLogs.Count < 10) return Ok(new { sent = false });

            var allDown = recentLogs.All(l => !l.IsAvailable);
            var wasDown = recentLogs.Take(recentLogs.Count - 1).All(l => !l.IsAvailable);
            var lastIsUp = recentLogs.Last().IsAvailable;

            // URL восстановился
            if (wasDown && lastIsUp)
            {
                var restoredAlert = new ParkingAlert
                {
                    ParkingId = parkingId,
                    Type = "url_restored",
                    Message = $"Видеопоток восстановлен после недоступности",
                    EmailSent = false
                };
                db.ParkingAlerts.Add(restoredAlert);

                if (parking.AlertsEnabled && !string.IsNullOrEmpty(parking.User?.Email))
                {
                    await email.SendAsync(
                        parking.User.Email,
                        $"✅ SmartParkings: камера восстановлена — {parking.Name}",
                        $"Видеопоток парковки «{parking.Name}» снова доступен.");
                    restoredAlert.EmailSent = true;
                }

                await db.SaveChangesAsync();
                return Ok(new { sent = true, type = "restored" });
            }

            if (!allDown) return Ok(new { sent = false });

            // Проверяем что алёрт не отправляли в последние 2 часа
            var twoHoursAgo = DateTime.UtcNow.AddHours(-2);
            var recentAlert = await db.ParkingAlerts
                .AnyAsync(a => a.ParkingId == parkingId
                            && a.Type == "url_down"
                            && a.CreatedAt >= twoHoursAgo);

            if (recentAlert) return Ok(new { sent = false });

            var lastError = recentLogs.LastOrDefault(l => !string.IsNullOrEmpty(l.ErrorMessage))
                                      ?.ErrorMessage ?? "Нет ответа";

            var alert = new ParkingAlert
            {
                ParkingId = parkingId,
                Type = "url_down",
                Message = $"URL недоступен более 1 часа. Ошибка: {lastError}",
                EmailSent = false
            };
            db.ParkingAlerts.Add(alert);

            if (parking.AlertsEnabled && !string.IsNullOrEmpty(parking.User?.Email))
            {
                await email.SendAsync(
                    parking.User.Email,
                    $"⚠ SmartParkings: камера недоступна — {parking.Name}",
                    $"Видеопоток парковки «{parking.Name}» ({parking.StreamUrl}) " +
                    $"недоступен более часа.\n\nОшибка: {lastError}\n\n" +
                    $"Открыть: smartparkings.ru/parking/{parking.Id}");
                alert.EmailSent = true;
            }

            await db.SaveChangesAsync();
            return Ok(new { sent = alert.EmailSent, type = "down" });
        }

        // GET api/parkings/{id}/alerts
        [HttpGet("/api/parkings/{parkingId:int}/alerts")]
        [Authorize]
        public async Task<IActionResult> GetAlerts(
            int parkingId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = int.Parse(User.FindFirstValue("uid") ?? "0");
            var parking = await db.Parkings.FindAsync(parkingId);
            if (parking == null || parking.UserId != userId) return Forbid();

            var total = await db.ParkingAlerts
                .CountAsync(a => a.ParkingId == parkingId);

            var alerts = await db.ParkingAlerts
                .Where(a => a.ParkingId == parkingId)
                .OrderByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new {
                    a.Id,
                    a.Type,
                    a.Message,
                    a.EmailSent,
                    a.CreatedAt
                })
                .ToListAsync();

            return Ok(new { alerts, total, page, pageSize });
        }

        // PUT api/parkings/{id}/alerts/toggle
        [HttpPut("/api/parkings/{parkingId:int}/alerts/toggle")]
        [Authorize]
        public async Task<IActionResult> ToggleAlerts(int parkingId)
        {
            var userId = int.Parse(User.FindFirstValue("uid") ?? "0");
            var parking = await db.Parkings.FindAsync(parkingId);
            if (parking == null || parking.UserId != userId) return Forbid();

            parking.AlertsEnabled = !parking.AlertsEnabled;
            await db.SaveChangesAsync();
            return Ok(new { alertsEnabled = parking.AlertsEnabled });
        }

        // DELETE api/parkings/{id}/alerts
        [HttpDelete("/api/parkings/{parkingId:int}/alerts")]
        [Authorize]
        public async Task<IActionResult> ClearAlerts(int parkingId)
        {
            var userId = int.Parse(User.FindFirstValue("uid") ?? "0");
            var parking = await db.Parkings.FindAsync(parkingId);
            if (parking == null || parking.UserId != userId) return Forbid();

            await db.ParkingAlerts
                .Where(a => a.ParkingId == parkingId)
                .ExecuteDeleteAsync();
            return Ok();
        }
    }


    public class UrlHealthCheckRequest
    {
        public bool IsAvailable { get; set; }
        public int? ResponseMs { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
