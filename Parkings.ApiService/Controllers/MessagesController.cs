using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Parkings.ApiService.Controllers
{
    [ApiController, Route("api/messages"), Authorize]
    public class MessagesController(AppDbContext db) : ControllerBase
    {
        private int Me => int.Parse(User.FindFirstValue("uid") ?? "0");

        [HttpGet("dialogs")]
        public async Task<IActionResult> GetDialogs()
        {
            var myId = Me;
            var all = await db.Messages
                .Where(m => m.SenderId == myId || m.ReceiverId == myId)
                .Include(m => m.Parking)
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            var dialogs = all
                .GroupBy(m => new {
                    m.ParkingId,
                    OtherId = m.SenderId == myId ? m.ReceiverId : m.SenderId
                })
                .Select(g => {
                    var last = g.First();
                    var other = g.Key.OtherId == last.SenderId ? last.Sender : last.Receiver;
                    return new
                    {
                        g.Key.ParkingId,
                        g.Key.OtherId,
                        ParkingName = last.Parking.Name,
                        OtherName = other.Login,
                        LastMessage = last.Text,
                        LastAt = last.CreatedAt,
                        Unread = g.Count(m => m.ReceiverId == myId && !m.IsRead)
                    };
                })
                .OrderByDescending(d => d.LastAt)
                .ToList();

            return Ok(dialogs);
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> UnreadCount() =>
            Ok(new
            {
                count = await db.Messages
                .CountAsync(m => m.ReceiverId == Me && !m.IsRead)
            });

        [HttpGet("{parkingId:int}/{otherUserId:int}")]
        public async Task<IActionResult> GetHistory(int parkingId, int otherUserId)
        {
            var myId = Me;
            var msgs = await db.Messages
                .Where(m => m.ParkingId == parkingId
                         && ((m.SenderId == myId && m.ReceiverId == otherUserId)
                          || (m.SenderId == otherUserId && m.ReceiverId == myId)))
                .Include(m => m.Sender)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new {
                    m.Id,
                    m.SenderId,
                    m.ReceiverId,
                    m.ParkingId,
                    m.Text,
                    m.IsRead,
                    m.CreatedAt,
                    SenderName = m.Sender.Login
                })
                .ToListAsync();
            return Ok(msgs);
        }

        // Чтобы кнопка "Написать владельцу" знала кому писать
        [HttpGet("owner/{parkingId:int}")]
        public async Task<IActionResult> GetOwner(int parkingId)
        {
            var p = await db.Parkings
                .Include(x => x.User)
                .FirstOrDefaultAsync(x => x.Id == parkingId);
            if (p == null) return NotFound();
            return Ok(new { ownerId = p.UserId, ownerName = p.User.Login, parkingName = p.Name });
        }
    }
}
