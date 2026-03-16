using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parkings.ApiService.ApiDTOs;
using System.Security.Claims;

namespace Parkings.ApiService.Controllers
{
    [ApiController]
    [Route("api/favorites")]
    [Authorize]
    public class FavoritesController(AppDbContext db) : ControllerBase
    {
        private int CurrentUserId =>
            int.Parse(User.FindFirstValue("uid") ?? "0");

        // GET api/favorites
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await db.Favorites
                .Where(f => f.UserId == CurrentUserId)
                .Include(f => f.Parking)
                .Select(f => new PublicParkingDto
                {
                    Id = f.Parking.Id,
                    Name = f.Parking.Name,
                    Address = f.Parking.Address,
                    Latitude = f.Parking.Latitude,
                    Longitude = f.Parking.Longitude,
                    FreeSpots = f.Parking.FreeSpots,
                    BusySpots = f.Parking.BusySpots
                })
                .ToListAsync();
            return Ok(list);
        }

        // GET api/favorites/ids — просто список id для проверки звезды
        [HttpGet("ids")]
        public async Task<IActionResult> GetIds()
        {
            var ids = await db.Favorites
                .Where(f => f.UserId == CurrentUserId)
                .Select(f => f.ParkingId)
                .ToListAsync();
            return Ok(ids);
        }

        // POST api/favorites/{parkingId}
        [HttpPost("{parkingId:int}")]
        public async Task<IActionResult> Add(int parkingId)
        {
            var exists = await db.Favorites.AnyAsync(
                f => f.UserId == CurrentUserId && f.ParkingId == parkingId);
            if (exists) return Ok(new { added = false });

            var parkingExists = await db.Parkings.AnyAsync(p => p.Id == parkingId);
            if (!parkingExists) return NotFound();

            db.Favorites.Add(new Favorite
            {
                UserId = CurrentUserId,
                ParkingId = parkingId
            });
            await db.SaveChangesAsync();
            return Ok(new { added = true });
        }

        // DELETE api/favorites/{parkingId}
        [HttpDelete("{parkingId:int}")]
        public async Task<IActionResult> Remove(int parkingId)
        {
            var fav = await db.Favorites.FirstOrDefaultAsync(
                f => f.UserId == CurrentUserId && f.ParkingId == parkingId);
            if (fav == null) return NotFound();

            db.Favorites.Remove(fav);
            await db.SaveChangesAsync();
            return Ok(new { removed = true });
        }
    }
}