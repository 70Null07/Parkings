using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Drawing;
using System.Net.Http.Json;
using System.Text.Json;

namespace Parkings.Worker
{
    public class ParkingWorker(
        IHttpClientFactory httpFactory,
        IConfiguration config,
        YoloDetector _detector,           
        ILogger<ParkingWorker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("Worker запущен");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAllParkings(stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Ошибка в цикле обработки");
                }

                // Общий интервал опроса — минимальный из всех парковок
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        private async Task ProcessAllParkings(CancellationToken ct)
        {
            var apiClient = httpFactory.CreateClient("api");
            var workerKey = config["Worker:ApiKey"];

            // Получаем все парковки с полигонами
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"https+http://apiservice/api/parkings/with-spaces");
            req.Headers.Add("X-Worker-Key", workerKey);
            var resp = await apiClient.SendAsync(req, ct);

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Не удалось получить парковки: {Status}", resp.StatusCode);
                return;
            }

            var parkings = await resp.Content
                .ReadFromJsonAsync<List<ParkingWithSpacesDto>>(cancellationToken: ct) ?? new();

            logger.LogInformation("Обрабатываем {Count} парковок", parkings.Count);

            // Обрабатываем параллельно
            var tasks = parkings.Select(p => ProcessParking(p, workerKey!, ct));
            await Task.WhenAll(tasks);
        }

        private async Task ProcessParking(ParkingWithSpacesDto parking, string workerKey, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(parking.StreamUrl)) return;

            try
            {
                List<BoundingBox> carBoxes;
                int frameWidth = 640, frameHeight = 480; // дефолт = размер canvas

                if (parking.StreamType == "MJPEG")
                {
                    (carBoxes, frameWidth, frameHeight) = _detector.DetectFromMjpegWithSize(parking.StreamUrl);
                }
                else
                {
                    using var imgClient = httpFactory.CreateClient("camera");
                    var imageBytes = await imgClient.GetByteArrayAsync(parking.StreamUrl, ct);
                    (carBoxes, frameWidth, frameHeight) = _detector.DetectWithSize(imageBytes);
                }

                logger.LogInformation("Парковка {Id}: кадр {W}x{H}, найдено {Cars} машин",
                    parking.Id, frameWidth, frameHeight, carBoxes.Count);

                var (free, busy, statuses) = MatchCarsToSpaces(
                    carBoxes, parking.Spaces, frameWidth, frameHeight);

                var apiClient = httpFactory.CreateClient("api");
                using var req = new HttpRequestMessage(HttpMethod.Post,
                    "https+http://apiservice/api/parkings/" + parking.Id + "/occupancy");
                req.Headers.Add("X-Worker-Key", workerKey);
                req.Content = JsonContent.Create(new UpdateOccupancyRequest
                {
                    FreeSpots = free,
                    BusySpots = busy,
                    TotalSpots = parking.Spaces.Count,
                    SpaceStatuses = statuses
                });
                await apiClient.SendAsync(req, ct);

                logger.LogInformation("Парковка {Id}: {Busy}/{Total} занято",
                    parking.Id, busy, parking.Spaces.Count);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка обработки парковки {Id}", parking.Id);
            }
        }

        private static (int free, int busy, List<SpaceOccupancyItem> statuses) MatchCarsToSpaces(
            List<BoundingBox> cars, List<ParkingSpaceDto> spaces,
            int frameWidth, int frameHeight)
        {
            // Canvas всегда 640x480 — масштабируем полигоны под реальный размер кадра
            const float canvasW = 640f;
            const float canvasH = 480f;
            float scaleX = frameWidth / canvasW;
            float scaleY = frameHeight / canvasH;

            int busy = 0;
            var statuses = new List<SpaceOccupancyItem>();

            foreach (var space in spaces)
            {
                // Масштабируем точки полигона из canvas-координат в координаты кадра
                var scaledPoints = space.Points.Select(p => new SpacePointDto
                {
                    X = p.X * scaleX,
                    Y = p.Y * scaleY
                }).ToList();

                bool occupied = cars.Any(car =>
                {
                    var center = new PointF((car.X1 + car.X2) / 2f, (car.Y1 + car.Y2) / 2f);
                    return IsPointInPolygon(center, scaledPoints);
                });

                if (occupied) busy++;
                statuses.Add(new SpaceOccupancyItem { SpaceId = space.Id, IsOccupied = occupied });
            }

            return (spaces.Count - busy, busy, statuses);
        }

        private static (int free, int busy, List<SpaceOccupancyItem> statuses) MatchCarsToSpaces(
            List<BoundingBox> cars, List<ParkingSpaceDto> spaces)
        {
            int busy = 0;
            var statuses = new List<SpaceOccupancyItem>();

            foreach (var space in spaces)
            {
                bool occupied = cars.Any(car =>
                {
                    var center = new PointF((car.X1 + car.X2) / 2f, (car.Y1 + car.Y2) / 2f);
                    return IsPointInPolygon(center, space.Points);
                });

                if (occupied) busy++;
                statuses.Add(new SpaceOccupancyItem { SpaceId = space.Id, IsOccupied = occupied });
            }

            return (spaces.Count - busy, busy, statuses);
        }

        private static bool IsPointInPolygon(PointF point, List<SpacePointDto> polygon)
        {
            bool inside = false;
            int j = polygon.Count - 1;

            for (int i = 0; i < polygon.Count; i++)
            {
                float xi = (float)polygon[i].X, yi = (float)polygon[i].Y;
                float xj = (float)polygon[j].X, yj = (float)polygon[j].Y;

                if ((yi > point.Y) != (yj > point.Y) &&
                    point.X < (xj - xi) * (point.Y - yi) / (yj - yi) + xi)
                    inside = !inside;

                j = i;
            }

            return inside;
        }
    }

    // ── DTOs для общения с API ──────────────────────────────
    public class ParkingWithSpacesDto
    {
        public int Id { get; set; }
        public string? StreamUrl { get; set; }
        public string StreamType { get; set; } = "JPG";
        public int RefreshInterval { get; set; }
        public List<ParkingSpaceDto> Spaces { get; set; } = new();
    }

    public class ParkingSpaceDto
    {
        public int Id { get; set; }
        public int SpaceNumber { get; set; }
        public List<SpacePointDto> Points { get; set; } = new();
    }

    public class SpacePointDto
    {
        public double X { get; set; }
        public double Y { get; set; }
    }

    public class SpaceOccupancyItem
    {
        public int SpaceId { get; set; }
        public bool IsOccupied { get; set; }
    }

    public class UpdateOccupancyRequest
    {
        public int FreeSpots { get; set; }
        public int BusySpots { get; set; }
        public int TotalSpots { get; set; }
        public List<SpaceOccupancyItem> SpaceStatuses { get; set; } = new();
    }

    public record BoundingBox(float X1, float Y1, float X2, float Y2);
}