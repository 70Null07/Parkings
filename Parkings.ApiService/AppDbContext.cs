using Microsoft.EntityFrameworkCore;

namespace Parkings.ApiService
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Parking> Parkings { get; set; }
        public DbSet<ParkingSpace> ParkingSpaces { get; set; }
        public DbSet<OccupancyLog> OccupancyLogs { get; set; }
        public DbSet<Favorite> Favorites { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<UserMapPolygon> UserMapPolygons { get; set; }
        public DbSet<UrlHealthLog> UrlHealthLogs { get; set; }
        public DbSet<ParkingAlert> ParkingAlerts { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Favorite>()
                .HasOne(f => f.User)
                .WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Favorite>()
                .HasOne(f => f.Parking)
                .WithMany()
                .HasForeignKey(f => f.ParkingId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Sender).WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Message>()
                .HasOne(m => m.Receiver).WithMany()
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<UserMapPolygon>()
                .HasOne(f => f.User).WithMany()
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ParkingAlert>()
                .HasOne(a => a.Parking).WithMany()
                .HasForeignKey(a => a.ParkingId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Parking>()
                .Property(p => p.IsApproved)
                .HasDefaultValue(true);
        }
    }

    public class User
    {
        public int Id { get; set; }
        public string Login { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string? Email { get; set; }
        public string? EmailConfirmCode { get; set; }
        public bool EmailConfirmed { get; set; } = false;
        public string? PasswordResetCode { get; set; }
        public DateTime? PasswordResetCodeExpiry { get; set; }
        public string Role { get; set; } = "user"; // user | admin | superadmin
        public bool IsDeleted { get; set; } = false;
        public bool IsBlocked { get; set; } = false;
        public DateTime? BlockedAt { get; set; }
        public ICollection<Parking> Parkings { get; set; } = new List<Parking>();
    }

    public class Parking
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Address { get; set; } = null!;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? StreamUrl { get; set; }
        public string StreamType { get; set; } = "JPG";
        public int RefreshInterval { get; set; } = 5;
        public int FreeSpots { get; set; } = 0;
        public int BusySpots { get; set; } = 0;
        public DateTime? LastCheckedAt { get; set; }
        public bool IsDisabled { get; set; } = false;
        public bool IsApproved { get; set; } = true;
        public int UserId { get; set; }
        public bool AlertsEnabled { get; set; } = true;
        public string YoloModel { get; set; } = "classic"; // classic | extended | vertical
        public string? StreamLogin { get; set; }
        public string? StreamPassword { get; set; }
        public User User { get; set; } = null!;
        public ICollection<ParkingSpace> Spaces { get; set; } = new List<ParkingSpace>();
        public ICollection<OccupancyLog> OccupancyLogs { get; set; } = new List<OccupancyLog>();
    }

    public class ParkingSpace
    {
        public int Id { get; set; }
        public int ParkingId { get; set; }
        public int SpaceNumber { get; set; }
        public string PointsJson { get; set; } = null!;
        public bool IsOccupied { get; set; } = false;
        public bool? IsManuallyBlocked { get; set; } // null=авто, true=занято, false=свободно
        public Parking Parking { get; set; } = null!;
    }

    public class OccupancyLog
    {
        public int Id { get; set; }
        public int ParkingId { get; set; }
        public int FreeSpots { get; set; }
        public int BusySpots { get; set; }
        public int TotalSpots { get; set; }
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
        public Parking Parking { get; set; } = null!;
    }

    public class Favorite
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int ParkingId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public User User { get; set; } = null!;
        public Parking Parking { get; set; } = null!;
    }

    public class Message
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public int ReceiverId { get; set; }
        public int ParkingId { get; set; }
        public string Text { get; set; } = null!;
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public User Sender { get; set; } = null!;
        public User Receiver { get; set; } = null!;
        public Parking Parking { get; set; } = null!;
    }

    public class UserMapPolygon
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; } = "Зона";
        public string PointsJson { get; set; } = "[]";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public User User { get; set; } = null!;
    }

    public class UrlHealthLog
    {
        public int Id { get; set; }
        public int ParkingId { get; set; }
        public bool IsAvailable { get; set; }
        public int? ResponseMs { get; set; }       // время ответа в мс
        public string? ErrorMessage { get; set; }
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
        public Parking Parking { get; set; } = null!;
    }

    public class ParkingAlert
    {
        public int Id { get; set; }
        public int ParkingId { get; set; }
        public string Type { get; set; } = "url_down"; // url_down | url_restored
        public string Message { get; set; } = "";
        public bool EmailSent { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public Parking Parking { get; set; } = null!;
    }
}
