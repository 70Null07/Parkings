using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Parkings.ApiService;
using System.Security.Claims;

[Authorize]
public class ChatHub(AppDbContext db) : Hub
{
    private int Me => int.Parse(Context.User!.FindFirstValue("uid") ?? "0");

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "user_" + Me);
        await base.OnConnectedAsync();
    }

    public async Task SendMessage(int receiverId, int parkingId, string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 1000) return;
        var msg = new Message
        {
            SenderId = Me,
            ReceiverId = receiverId,
            ParkingId = parkingId,
            Text = text.Trim()
        };
        db.Messages.Add(msg);
        await db.SaveChangesAsync();

        var sender = await db.Users.FindAsync(Me);
        var dto = new
        {
            msg.Id,
            msg.SenderId,
            msg.ReceiverId,
            msg.ParkingId,
            msg.Text,
            msg.IsRead,
            msg.CreatedAt,
            SenderName = sender?.Login ?? "?"
        };
        await Clients.Group("user_" + receiverId).SendAsync("ReceiveMessage", dto);
        await Clients.Caller.SendAsync("MessageSent", dto);
    }

    public async Task MarkRead(int otherUserId, int parkingId)
    {
        var msgs = await db.Messages
            .Where(m => m.ParkingId == parkingId
                     && m.SenderId == otherUserId
                     && m.ReceiverId == Me && !m.IsRead)
            .ToListAsync();
        msgs.ForEach(m => m.IsRead = true);
        await db.SaveChangesAsync();
        await Clients.Group("user_" + otherUserId)
            .SendAsync("MessagesRead", new { parkingId, readBy = Me });
    }
}