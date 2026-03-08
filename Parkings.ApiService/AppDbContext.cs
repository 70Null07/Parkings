using Microsoft.EntityFrameworkCore;

namespace Parkings.ApiService
{
    public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Parking> Parkings { get; set; }
        public DbSet<ParkingSpace> ParkingSpaces { get; set; }
        public DbSet<OccupancyLog> OccupancyLogs { get; set; }
    }

    public class User
    {
        public int Id { get; set; }
        public string Login { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string? Email { get; set; }
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
        public int UserId { get; set; }
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
}