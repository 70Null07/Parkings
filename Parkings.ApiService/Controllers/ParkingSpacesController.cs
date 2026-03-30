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
                IsManuallyBlocked = s.IsManuallyBlocked,
                Points = JsonSerializer.Deserialize<List<SpacePointDto>>(s.PointsJson) ?? new()
            });

            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> SaveSpaces(int parkingId, [FromBody] SaveSpacesRequest req)
        {
            var parking = await _db.Parkings
                .FirstOrDefaultAsync(p => p.Id == parkingId && p.UserId == CurrentUserId);
            if (parking == null) return NotFound();

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

            parking.FreeSpots = req.Spaces.Count;
            parking.BusySpots = 0;

            await _db.SaveChangesAsync();

            return Ok(new { saved = req.Spaces.Count });
        }

        [HttpPost("{spaceId:int}/block")]
        [Authorize]
        public async Task<IActionResult> BlockSpace(
            int parkingId, int spaceId, [FromBody] BlockSpaceRequest req)
        {
            var userId = int.Parse(User.FindFirstValue("uid") ?? "0");
            var parking = await db.Parkings.FindAsync(parkingId);
            if (parking == null || parking.UserId != userId)
                return Forbid();

            var space = await db.ParkingSpaces
                .FirstOrDefaultAsync(s => s.Id == spaceId && s.ParkingId == parkingId);
            if (space == null) return NotFound();

            space.IsManuallyBlocked = req.Status; // true=занято, false=свободно, null=авто

            if (req.Status.HasValue)
                space.IsOccupied = req.Status.Value;

            var allSpaces = await db.ParkingSpaces
                .Where(s => s.ParkingId == parkingId)
                .ToListAsync();

            var busyCount = allSpaces.Count(s =>
                s.Id == spaceId
                    ? (req.Status ?? s.IsOccupied)
                    : s.IsOccupied);

            parking.BusySpots = busyCount;
            parking.FreeSpots = allSpaces.Count - busyCount;

            await db.SaveChangesAsync();

            return Ok(new { spaceId, status = req.Status });
        }

        public class BlockSpaceRequest
        {
            public bool? Status { get; set; }
        }
    }
}