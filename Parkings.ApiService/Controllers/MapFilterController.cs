using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace Parkings.ApiService.Controllers
{
    [ApiController, Route("api/mapfilter"), Authorize]
    public class MapFilterController(AppDbContext db) : ControllerBase
    {
        private int Me => int.Parse(User.FindFirstValue("uid") ?? "0");

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var list = await db.UserMapPolygons
                .Where(p => p.UserId == Me)
                .Select(p => new { p.Id, p.Name, p.PointsJson })
                .ToListAsync();
            return Ok(list);
        }

        [HttpPost]
        public async Task<IActionResult> Add([FromBody] SavePolygonRequest req)
        {
            var p = new UserMapPolygon
            {
                UserId = Me,
                Name = req.Name,
                PointsJson = req.PointsJson
            };
            db.UserMapPolygons.Add(p);
            await db.SaveChangesAsync();
            return Ok(new { p.Id, p.Name, p.PointsJson });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] SavePolygonRequest req)
        {
            var p = await db.UserMapPolygons
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == Me);
            if (p == null) return NotFound();
            p.Name = req.Name;
            p.PointsJson = req.PointsJson;
            await db.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            var p = await db.UserMapPolygons
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == Me);
            if (p == null) return NotFound();
            db.UserMapPolygons.Remove(p);
            await db.SaveChangesAsync();
            return Ok();
        }
    }

    public class SavePolygonRequest
    {
        public string Name { get; set; } = "Зона";
        public string PointsJson { get; set; } = "[]";
    }
}
