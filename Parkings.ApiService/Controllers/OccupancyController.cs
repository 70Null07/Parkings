using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parkings.ApiService.ApiDTOs;

namespace Parkings.ApiService.Controllers
{
    [ApiController]
    [Route("api/parkings/{parkingId:int}/occupancy")]
    public class OccupancyController(AppDbContext db) : ControllerBase
    {
        private readonly AppDbContext _db = db;

        // GET — текущая загруженность (для UI)
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetCurrent(int parkingId)
        {
            var p = await _db.Parkings.FindAsync(parkingId);
            if (p == null) return NotFound();

            return Ok(new
            {
                p.FreeSpots,
                p.BusySpots,
                TotalSpots = p.FreeSpots + p.BusySpots,
                p.LastCheckedAt
            });
        }

        // POST — вызывает Worker, без авторизации через JWT пользователя
        // Защищаем отдельным API-ключом из конфига
        [HttpPost]
        public async Task<IActionResult> UpdateOccupancy(
            int parkingId,
            [FromBody] UpdateOccupancyRequest req,
            [FromHeader(Name = "X-Worker-Key")] string? workerKey,
            IConfiguration config)
        {
            var expectedKey = config["Worker:ApiKey"];
            if (string.IsNullOrEmpty(expectedKey) || workerKey != expectedKey)
                return Unauthorized("Invalid worker key");

            var parking = await _db.Parkings.FindAsync(parkingId);
            if (parking == null) return NotFound();

            parking.FreeSpots = req.FreeSpots;
            parking.BusySpots = req.BusySpots;
            parking.LastCheckedAt = DateTime.UtcNow;

            // Обновляем IsOccupied для каждого места
            if (req.SpaceStatuses.Any())
            {
                var spaceIds = req.SpaceStatuses.Select(s => s.SpaceId).ToList();
                var spaces = await _db.ParkingSpaces
                    .Where(s => s.ParkingId == parkingId && spaceIds.Contains(s.Id))
                    .ToListAsync();

                foreach (var space in spaces)
                {
                    var status = req.SpaceStatuses.FirstOrDefault(s => s.SpaceId == space.Id);
                    if (status != null) space.IsOccupied = status.IsOccupied;
                }
            }

            _db.OccupancyLogs.Add(new OccupancyLog
            {
                ParkingId = parkingId,
                FreeSpots = req.FreeSpots,
                BusySpots = req.BusySpots,
                TotalSpots = req.TotalSpots,
                RecordedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return Ok(new { updated = true });
        }

        // GET history — последние 100 записей (для будущих графиков)
        //[HttpGet("history")]
        //[Authorize]
        //public async Task<IActionResult> GetHistory(int parkingId)
        //{
        //    var logs = await _db.OccupancyLogs
        //        .Where(l => l.ParkingId == parkingId)
        //        .OrderByDescending(l => l.RecordedAt)
        //        .Take(100)
        //        .Select(l => new
        //        {
        //            l.FreeSpots,
        //            l.BusySpots,
        //            l.TotalSpots,
        //            l.RecordedAt
        //        })
        //        .ToListAsync();

        //    return Ok(logs);
        //}

        // GET api/parkings/5/occupancy/history?from=2024-01-01&to=2024-01-31&group=hour
        [HttpGet("history")]
        [AllowAnonymous]
        public async Task<IActionResult> GetHistory(
            int parkingId,
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] string group = "raw") // raw | hour | day
        {
            var exists = await _db.Parkings.AnyAsync(p => p.Id == parkingId);
            if (!exists) return NotFound();

            var query = _db.OccupancyLogs
                .Where(l => l.ParkingId == parkingId);

            if (from.HasValue)
                query = query.Where(l => l.RecordedAt >= from.Value.ToUniversalTime());
            if (to.HasValue)
                query = query.Where(l => l.RecordedAt <= to.Value.ToUniversalTime());

            var logs = await query
                .OrderBy(l => l.RecordedAt)
                .Select(l => new
                {
                    l.RecordedAt,
                    l.FreeSpots,
                    l.BusySpots,
                    l.TotalSpots
                })
                .ToListAsync();

            // Группировка на стороне сервера
            var result = group switch
            {
                "hour" => logs
                    .GroupBy(l => new DateTime(
                        l.RecordedAt.Year, l.RecordedAt.Month, l.RecordedAt.Day,
                        l.RecordedAt.Hour, 0, 0))
                    .Select(g => new OccupancyHistoryDto
                    {
                        RecordedAt = g.Key,
                        FreeSpots = (int)g.Average(x => x.FreeSpots),
                        BusySpots = (int)g.Average(x => x.BusySpots),
                        TotalSpots = g.First().TotalSpots
                    }),
                "day" => logs
                    .GroupBy(l => l.RecordedAt.Date)
                    .Select(g => new OccupancyHistoryDto
                    {
                        RecordedAt = g.Key,
                        FreeSpots = (int)g.Average(x => x.FreeSpots),
                        BusySpots = (int)g.Average(x => x.BusySpots),
                        TotalSpots = g.First().TotalSpots
                    }),
                _ => logs.Select(l => new OccupancyHistoryDto
                {
                    RecordedAt = l.RecordedAt,
                    FreeSpots = l.FreeSpots,
                    BusySpots = l.BusySpots,
                    TotalSpots = l.TotalSpots
                })
            };

            return Ok(result.ToList());
        }
    }
}