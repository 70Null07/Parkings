using System.ComponentModel.DataAnnotations;

namespace Parkings.ApiService.ApiDTOs
{
    public class ApiDTOs
    {
    }

    public class LoginRequest
    {
        [Required] public string Login { get; set; } = null!;
        [Required] public string Password { get; set; } = null!;
    }

    public class RegisterRequest
    {
        [Required] public string Login { get; set; } = null!;
        [Required] public string Password { get; set; } = null!;
    }
    public class UserDto
    {
        public int UserId { get; set; }
        public string DisplayName { get; set; } = null!;
        public string? Email { get; set; }
    }

    public class ParkingDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Address { get; set; } = null!;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? StreamUrl { get; set; }
        public string StreamType { get; set; } = "JPG";
        public int RefreshInterval { get; set; }
        public int FreeSpots { get; set; }
        public int BusySpots { get; set; }
        public int UserId { get; set; }
        public DateTime? LastCheckedAt { get; set; }
    }

    public class CreateParkingRequest
    {
        [Required] public string Name { get; set; } = null!;
        [Required] public string Address { get; set; } = null!;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? StreamUrl { get; set; }
        public string StreamType { get; set; } = "JPG";
        [Range(1, 3600)] public int RefreshInterval { get; set; } = 5;
    }

    public class UpdateParkingRequest
    {
        [Required] public string Name { get; set; } = null!;
        [Required] public string Address { get; set; } = null!;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? StreamUrl { get; set; }
        public string StreamType { get; set; } = "JPG";
        [Range(1, 3600)] public int RefreshInterval { get; set; } = 5;
    }

    public class SpacePointDto
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class ParkingSpaceDto
    {
        public int Id { get; set; }
        public int SpaceNumber { get; set; }
        public bool IsOccupied { get; set; }
        public List<SpacePointDto> Points { get; set; } = new();
    }

    public class SaveSpacesRequest
    {
        public List<SaveSpaceItem> Spaces { get; set; } = new();
    }

    public class SaveSpaceItem
    {
        public int Id { get; set; }  // id из JS (P1, P2...)
        public List<SpacePointDto> Points { get; set; } = new();
    }

    // Запрос от Worker — обновить загруженность парковки
    public class UpdateOccupancyRequest
    {
        public int FreeSpots { get; set; }
        public int BusySpots { get; set; }
        public int TotalSpots { get; set; }
        public List<SpaceOccupancyItem> SpaceStatuses { get; set; } = new();
    }

    public class SpaceOccupancyItem
    {
        public int SpaceId { get; set; }
        public bool IsOccupied { get; set; }
    }

    // Ответ с данными парковки + полигонами для Worker
    public class ParkingWithSpacesDto
    {
        public int Id { get; set; }
        public string? StreamUrl { get; set; }
        public string StreamType { get; set; } = "JPG";
        public int RefreshInterval { get; set; }
        public List<ParkingSpaceDto> Spaces { get; set; } = new();
    }

    // Для публичной карты
    public class PublicParkingDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Address { get; set; } = null!;
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int FreeSpots { get; set; }
        public int BusySpots { get; set; }
    }
    public class OccupancyHistoryDto
    {
        public DateTime RecordedAt { get; set; }
        public int FreeSpots { get; set; }
        public int BusySpots { get; set; }
        public int TotalSpots { get; set; }
    }
}
