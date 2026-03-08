using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parkings.ApiService.ApiDTOs;
using System.Security.Claims;
using System.Text.Json;

namespace Parkings.ApiService.Controllers
{
    [ApiController]
    [Route("api/parkings/{parkingId:int}/spaces")]
    [Authorize]
    public class ParkingSpacesController(AppDbContext db) : ControllerBase
    {
        private readonly AppDbContext _db = db;

        private int CurrentUserId =>
            int.Parse(User.FindFirstValue("uid") ?? "0");

        // GET api/parkings/5/spaces
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetSpaces(int parkingId)
        {
            var exists = await _db.Parkings.AnyAsync(p => p.Id == parkingId);
            if (!exists) return NotFound();

            var spaces = await _db.ParkingSpaces
                .Where(s => s.ParkingId == parkingId)
                .OrderBy(s => s.SpaceNumber)
                .ToListAsync();

            var result = spaces.Select(s => new ParkingSpaceDto
            {
                Id = s.Id,
                SpaceNumber = s.SpaceNumber,
                IsOccupied = s.IsOccupied,
                Points = JsonSerializer.Deserialize<List<SpacePointDto>>(s.PointsJson) ?? new()
            });

            return Ok(result);
        }

        // POST api/parkings/5/spaces — полная замена разметки
        [HttpPost]
        public async Task<IActionResult> SaveSpaces(int parkingId, [FromBody] SaveSpacesRequest req)
        {
            var parking = await _db.Parkings
                .FirstOrDefaultAsync(p => p.Id == parkingId && p.UserId == CurrentUserId);
            if (parking == null) return NotFound();

            // Удаляем старую разметку и сохраняем новую целиком
            var existing = await _db.ParkingSpaces
                .Where(s => s.ParkingId == parkingId)
                .ToListAsync();
            _db.ParkingSpaces.RemoveRange(existing);

            var newSpaces = req.Spaces.Select(s => new ParkingSpace
            {
                ParkingId = parkingId,
                SpaceNumber = s.Id,
                PointsJson = JsonSerializer.Serialize(s.Points)
            });
            await _db.ParkingSpaces.AddRangeAsync(newSpaces);

            // Обновляем общее количество мест
            parking.FreeSpots = req.Spaces.Count;
            parking.BusySpots = 0;

            await _db.SaveChangesAsync();

            return Ok(new { saved = req.Spaces.Count });
        }
    }
}