using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parkings.ApiService.ApiDTOs;
using System.Security.Claims;
using System.Text.Json;

namespace Parkings.ApiService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ParkingsController(AppDbContext db) : ControllerBase
    {
        private readonly AppDbContext _db = db;

        private int CurrentUserId =>
            int.Parse(User.FindFirstValue("uid") ?? "0");

        // GET api/parkings
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await _db.Parkings
                .Where(p => p.UserId == CurrentUserId)
                .Select(p => ToDto(p))
                .ToListAsync();
            return Ok(list);
        }

        // GET api/parkings/5
        [HttpGet("{id:int}")]
        [AllowAnonymous]  // ← публичный, чтобы любой мог открыть страницу парковки
        public async Task<IActionResult> GetById(int id)
        {
            var p = await _db.Parkings.FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return NotFound();
            return Ok(ToDto(p));
        }

        // POST api/parkings
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateParkingRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var entity = new Parking
            {
                Name = req.Name.Trim(),
                Address = req.Address.Trim(),
                Latitude = req.Latitude,        // ← добавить
                Longitude = req.Longitude,      // ← добавить
                StreamUrl = req.StreamUrl?.Trim(),
                StreamType = req.StreamType,
                RefreshInterval = req.RefreshInterval,
                UserId = CurrentUserId
            };

            _db.Parkings.Add(entity);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToDto(entity));
        }

        // PUT api/parkings/5
        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateParkingRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var entity = await _db.Parkings
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == CurrentUserId);
            if (entity == null) return NotFound();

            entity.Name = req.Name.Trim();
            entity.Address = req.Address.Trim();
            entity.Latitude = req.Latitude;         // ← добавить
            entity.Longitude = req.Longitude;       // ← добавить
            entity.StreamUrl = req.StreamUrl?.Trim();
            entity.StreamType = req.StreamType;
            entity.RefreshInterval = req.RefreshInterval;

            await _db.SaveChangesAsync();
            return Ok(ToDto(entity));
        }

        // DELETE api/parkings/5
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var entity = await _db.Parkings
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == CurrentUserId);
            if (entity == null) return NotFound();

            _db.Parkings.Remove(entity);
            await _db.SaveChangesAsync();
            return NoContent();
        }

        private static ParkingDto ToDto(Parking p) => new()
        {
            Id = p.Id,
            Name = p.Name,
            Address = p.Address,
            Latitude = p.Latitude,
            Longitude = p.Longitude,
            StreamUrl = p.StreamUrl,
            StreamType = p.StreamType,
            RefreshInterval = p.RefreshInterval,
            FreeSpots = p.FreeSpots,
            BusySpots = p.BusySpots,
            UserId = p.UserId
        };

        // GET api/parkings/with-spaces — только для Worker (по API-ключу)
        [HttpGet("with-spaces")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllWithSpaces(
            [FromHeader(Name = "X-Worker-Key")] string? workerKey,
            IConfiguration config)
        {
            var expectedKey = config["Worker:ApiKey"];
            if (string.IsNullOrEmpty(expectedKey) || workerKey != expectedKey)
                return Unauthorized("Invalid worker key");

            var parkings = await _db.Parkings
                .Include(p => p.Spaces)
                .Where(p => p.Spaces.Any()) // только с разметкой
                .ToListAsync();

            var result = parkings.Select(p => new ParkingWithSpacesDto
            {
                Id = p.Id,
                StreamUrl = p.StreamUrl,
                StreamType = p.StreamType,
                RefreshInterval = p.RefreshInterval,
                Spaces = p.Spaces.Select(s => new ParkingSpaceDto
                {
                    Id = s.Id,
                    SpaceNumber = s.SpaceNumber,
                    Points = JsonSerializer.Deserialize<List<SpacePointDto>>(s.PointsJson) ?? new()
                }).ToList()
            });

            return Ok(result);
        }

        // GET api/parkings/public — без авторизации, для карты
        [HttpGet("public")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublic([FromQuery] string? polygon = null)
        {
            var parkings = await _db.Parkings
                .Where(p => p.Latitude != null && p.Longitude != null)
                .Select(p => new PublicParkingDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Address = p.Address,
                    Latitude = p.Latitude,
                    Longitude = p.Longitude,
                    FreeSpots = p.FreeSpots,
                    BusySpots = p.BusySpots
                })
                .ToListAsync();

            if (!string.IsNullOrEmpty(polygon))
            {
                try
                {
                    var points = JsonSerializer.Deserialize<List<double[]>>(polygon) ?? new();
                    if (points.Count >= 3)
                        parkings = parkings
                            .Where(p => IsPointInPolygon(p.Latitude!.Value, p.Longitude!.Value, points))
                            .ToList();
                }
                catch { }
            }

            return Ok(parkings);
        }

        private static bool IsPointInPolygon(double lat, double lon, List<double[]> polygon)
        {
            bool inside = false;
            int j = polygon.Count - 1;
            for (int i = 0; i < polygon.Count; i++)
            {
                double xi = polygon[i][0], yi = polygon[i][1];
                double xj = polygon[j][0], yj = polygon[j][1];
                if ((yi > lon) != (yj > lon) &&
                    lat < (xj - xi) * (lon - yi) / (yj - yi) + xi)
                    inside = !inside;
                j = i;
            }
            return inside;
        }
    }
}