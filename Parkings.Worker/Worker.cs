using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
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
                    await Task.WhenAll(
                        ProcessAllParkings(stoppingToken),
                        CheckAllUrls(stoppingToken)
                    );
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Ошибка в цикле обработки");
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        private async Task ProcessAllParkings(CancellationToken ct)
        {
            var apiClient = httpFactory.CreateClient("api");
            var workerKey = config["Worker:ApiKey"];

            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"https+http://apiservice/api/parkings/with-spaces");
            req.Headers.Add("X-Worker-Key", workerKey);

            HttpResponseMessage resp;
            try { resp = await apiClient.SendAsync(req, ct); }
            catch (Exception ex)
            {
                logger.LogWarning("Не удалось получить парковки: {Msg}", ex.Message);
                return;
            }

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Не удалось получить парковки: {Status}", resp.StatusCode);
                return;
            }

            var parkings = await resp.Content
                .ReadFromJsonAsync<List<ParkingWithSpacesDto>>(cancellationToken: ct) ?? new();

            logger.LogInformation("Обрабатываем {Count} парковок", parkings.Count);

            // Каждая парковка — отдельный таск со своим таймаутом
            // Исключение в одном таске не влияет на остальные
            var tasks = parkings.Select(p => Task.Run(async () =>
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(25));
                try
                {
                    await ProcessParking(p, workerKey!, cts.Token);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Необработанная ошибка парковки {Id}", p.Id);
                }
            }, ct));

            await Task.WhenAll(tasks);
        }

        private async Task ProcessParking(ParkingWithSpacesDto parking,
            string workerKey, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(parking.StreamUrl)) return;

            List<BoundingBox> carBoxes;
            int frameWidth = 640, frameHeight = 480;

            try
            {
                if (parking.StreamType == "MJPEG")
                {
                    (carBoxes, frameWidth, frameHeight) =
                        _detector.DetectFromMjpegWithSize(
                        parking.StreamUrl,
                        parking.YoloModel,
                        parking.StreamLogin,
                        parking.StreamPassword);
                }
                else if (parking.StreamType == "RTSP")
                {
                    (carBoxes, frameWidth, frameHeight) =
                        _detector.DetectFromRtspWithSize(
                            parking.StreamUrl,
                            parking.YoloModel,
                            parking.StreamLogin,
                            parking.StreamPassword);
                }
                else
                {
                    using var imgClient = httpFactory.CreateClient("camera");
                    if (!string.IsNullOrEmpty(parking.StreamLogin))
                    {
                        var creds = Convert.ToBase64String(
                            System.Text.Encoding.UTF8.GetBytes(
                                $"{parking.StreamLogin}:{parking.StreamPassword}"));
                        imgClient.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", creds);
                    }
                    var imageBytes = await imgClient.GetByteArrayAsync(parking.StreamUrl, ct);
                    (carBoxes, frameWidth, frameHeight) = _detector.DetectWithSize(imageBytes);
                }
            }
            catch (OperationCanceledException)
            {
                logger.LogWarning("Таймаут детекции парковки {Id}", parking.Id);
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ошибка детекции парковки {Id}", parking.Id);
                return;
            }

            logger.LogInformation("Парковка {Id}: {W}x{H}, машин: {Cars}",
                parking.Id, frameWidth, frameHeight, carBoxes.Count);

            var (free, busy, statuses) = MatchCarsToSpaces(
                carBoxes, parking.Spaces, frameWidth, frameHeight);

            try
            {
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
                logger.LogError(ex, "Ошибка отправки результатов парковки {Id}", parking.Id);
            }
        }

        private static (int free, int busy, List<SpaceOccupancyItem> statuses) MatchCarsToSpaces(
            List<BoundingBox> cars, List<ParkingSpaceDto> spaces,
            int frameWidth, int frameHeight)
        {
            // Canvas всегда 640x480
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

        private async Task CheckAllUrls(CancellationToken ct)
        {
            var apiClient = httpFactory.CreateClient("api");
            var workerKey = config["Worker:ApiKey"]!;

            using var req = new HttpRequestMessage(HttpMethod.Get,
                "https+http://apiservice/api/parkings/with-spaces");
            req.Headers.Add("X-Worker-Key", workerKey);
            var resp = await apiClient.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return;

            var parkings = await resp.Content
                .ReadFromJsonAsync<List<ParkingWithSpacesDto>>(cancellationToken: ct) ?? new();

            var tasks = parkings
                .Where(p => !string.IsNullOrEmpty(p.StreamUrl))
                .Select(p => Task.Run(() => CheckUrl(p, workerKey, ct), ct));

            await Task.WhenAll(tasks);
        }

        private async Task CheckUrl(ParkingWithSpacesDto parking,
            string workerKey, CancellationToken ct)
        {
            bool isAvailable = false;
            int? responseMs = null;
            string? errorMsg = null;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                if (parking.StreamType == "RTSP")
                {
                    // RTSP — проверяем через VideoCapture, HttpClient не поддерживает rtsp://
                    var streamUrl = string.IsNullOrEmpty(parking.StreamLogin)
                        ? parking.StreamUrl!
                        : BuildUrlWithAuth(parking.StreamUrl!, parking.StreamLogin, parking.StreamPassword);

                    var available = await Task.Run(() =>
                    {
                        using var cap = new VideoCapture(streamUrl, VideoCaptureAPIs.FFMPEG);
                        if (!cap.IsOpened()) return false;
                        using var frame = new Mat();
                        cap.Read(frame);
                        return !frame.Empty();
                    }, ct);

                    isAvailable = available;
                    responseMs = (int)sw.ElapsedMilliseconds;
                }
                else if (parking.StreamType == "MJPEG")
                {
                    using var http = httpFactory.CreateClient("camera");
                    if (!string.IsNullOrEmpty(parking.StreamLogin))
                    {
                        var creds = Convert.ToBase64String(
                            System.Text.Encoding.UTF8.GetBytes(
                                $"{parking.StreamLogin}:{parking.StreamPassword}"));
                        http.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", creds);
                    }
                    using var req = new HttpRequestMessage(HttpMethod.Head, parking.StreamUrl);
                    using var cts2 = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts2.CancelAfter(TimeSpan.FromSeconds(10));
                    var resp = await http.SendAsync(req, cts2.Token);
                    isAvailable = resp.IsSuccessStatusCode;
                    responseMs = (int)sw.ElapsedMilliseconds;
                }
                else
                {
                    // JPG
                    using var http = httpFactory.CreateClient("camera");
                    if (!string.IsNullOrEmpty(parking.StreamLogin))
                    {
                        var creds = Convert.ToBase64String(
                            System.Text.Encoding.UTF8.GetBytes(
                                $"{parking.StreamLogin}:{parking.StreamPassword}"));
                        http.DefaultRequestHeaders.Authorization =
                            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", creds);
                    }
                    using var cts2 = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts2.CancelAfter(TimeSpan.FromSeconds(10));
                    var bytes = await http.GetByteArrayAsync(parking.StreamUrl!, cts2.Token);
                    isAvailable = bytes.Length > 1000;
                    responseMs = (int)sw.ElapsedMilliseconds;
                }
            }
            catch (Exception ex)
            {
                isAvailable = false;
                errorMsg = ex.Message.Length > 200
                    ? ex.Message[..200]
                    : ex.Message;
                responseMs = (int)sw.ElapsedMilliseconds;
            }
            finally { sw.Stop(); }

            // Отправляем результат в API
            try
            {
                var apiClient = httpFactory.CreateClient("api");
                using var req = new HttpRequestMessage(HttpMethod.Post,
                    $"https+http://apiservice/api/parkings/{parking.Id}/health");
                req.Headers.Add("X-Worker-Key", workerKey);
                req.Content = JsonContent.Create(new
                {
                    isAvailable,
                    responseMs,
                    errorMessage = errorMsg
                });
                await apiClient.SendAsync(req, ct);
            }
            catch { }

            // Проверяем нужно ли слать алёрт
            if (!isAvailable)
                await CheckAndSendAlert(parking.Id, workerKey, ct);
        }

        private static string BuildUrlWithAuth(string url, string? login, string? password)
        {
            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
                return url;
            var uri = new Uri(url);
            return $"{uri.Scheme}://{login}:{password}@{uri.Host}:{uri.Port}{uri.PathAndQuery}";
        }

        private async Task CheckAndSendAlert(int parkingId,
            string workerKey, CancellationToken ct)
        {
            // Проверяем через API — был ли алёрт уже отправлен
            var apiClient = httpFactory.CreateClient("api");
            using var req = new HttpRequestMessage(HttpMethod.Post,
                $"https+http://apiservice/api/parkings/{parkingId}/health/alert");
            req.Headers.Add("X-Worker-Key", workerKey);
            await apiClient.SendAsync(req, ct);
        }
    }

    public class ParkingWithSpacesDto
    {
        public int Id { get; set; }
        public string? StreamUrl { get; set; }
        public string StreamType { get; set; } = "JPG";
        public int RefreshInterval { get; set; }
        public string YoloModel { get; set; } = "classic";     
        public string? StreamLogin { get; set; }                
        public string? StreamPassword { get; set; }             
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