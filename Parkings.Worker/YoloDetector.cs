using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System.Drawing;

namespace Parkings.Worker
{
    public class YoloDetector : IDisposable
    {
        private readonly InferenceSession _classicSession;
        private readonly InferenceSession _extendedSession;
        private readonly InferenceSession _verticalSession;

        private readonly InferenceSession _session;
        private const int InputSize = 640;
        private const float ConfThreshold = 0.3f;
        private const int CarClassId = 2;
        private const int TruckClassId = 7;

        private static float GetThreshold(string model) => model switch
        {
            "extended" => 0.25f,  // расширенная — чуть мягче
            "vertical" => 0.35f,  // вертикальная — строже (много ложных)
            _ => 0.3f    // классическая
        };

        public YoloDetector(IConfiguration config)
        {
            _classicSession = new InferenceSession(config["Yolo:ClassicModel"]!);
            _extendedSession = new InferenceSession(config["Yolo:ExtendedModel"]!);
            _verticalSession = new InferenceSession(config["Yolo:VerticalModel"]!);
        }

        public List<BoundingBox> Detect(byte[] imageBytes)
        {
            using var mat = Cv2.ImDecode(imageBytes, ImreadModes.Color);
            return DetectFromMat(mat);
        }

        private InferenceSession GetSession(string model) => model switch
        {
            "extended" => _extendedSession,
            "vertical" => _verticalSession,
            _ => _classicSession
        };

        public List<BoundingBox> DetectFromMat(Mat image, string model = "classic")
        {
            float scaleX = (float)image.Width / InputSize;
            float scaleY = (float)image.Height / InputSize;

            using var resized = image.Resize(new OpenCvSharp.Size(InputSize, InputSize));
            var tensor = MatToTensor(resized);
            var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("images", tensor)
        };

            using var results = GetSession(model).Run(inputs);
            var output = results.First().AsTensor<float>();

            return ParseOutput(output, scaleX, scaleY, GetThreshold(model));
        }

        private static DenseTensor<float> MatToTensor(Mat image)
        {
            int w = image.Width, h = image.Height;
            var tensor = new DenseTensor<float>(new[] { 1, 3, h, w });

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var pixel = image.At<Vec3b>(y, x);
                    tensor[0, 0, y, x] = pixel[2] / 255f; // R
                    tensor[0, 1, y, x] = pixel[1] / 255f; // G
                    tensor[0, 2, y, x] = pixel[0] / 255f; // B
                }

            return tensor;
        }

        private static List<BoundingBox> ParseOutput(
            Tensor<float> output, float scaleX, float scaleY, float threshold)
        {
            var boxes = new List<BoundingBox>();
            int n = output.Dimensions[1];
            for (int i = 0; i < n; i++)
            {
                float conf = output[0, i, 4];
                int cls = (int)Math.Round(output[0, i, 5]);
                if ((cls == CarClassId || cls == TruckClassId) && conf > threshold)
                    boxes.Add(new BoundingBox(
                        output[0, i, 0] * scaleX,
                        output[0, i, 1] * scaleY,
                        output[0, i, 2] * scaleX,
                        output[0, i, 3] * scaleY));
            }
            return boxes;
        }

        public List<BoundingBox> DetectFromMjpeg(string url)
        {
            using var capture = new VideoCapture(url);
            if (!capture.IsOpened()) return new();

            using var frame = new Mat();
            if (!capture.Read(frame) || frame.Empty()) return new();

            capture.Release();
            return DetectFromMat(frame);
        }

        public (List<BoundingBox> boxes, int width, int height) DetectWithSize(
                byte[] imageBytes, string model = "classic")
        {
            using var mat = Cv2.ImDecode(imageBytes, ImreadModes.Color);
            return (DetectFromMat(mat, model), mat.Width, mat.Height);
        }

        private static string BuildUrlWithAuth(string url, string? login, string? password)
        {
            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
                return url;

            // http://user:pass@host/path
            var uri = new Uri(url);
            return $"{uri.Scheme}://{login}:{password}@{uri.Host}:{uri.Port}{uri.PathAndQuery}";
        }

        public (List<BoundingBox> boxes, int width, int height) DetectFromMjpegWithSize(
        string url, string model = "classic",
        string? login = null, string? password = null)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                var task = Task.Run(() =>
                {
                    // Для MJPEG с авторизацией — передаём credentials в URL
                    var streamUrl = BuildUrlWithAuth(url, login, password);

                    using var capture = new VideoCapture(streamUrl);
                    if (!capture.IsOpened())
                        return (new List<BoundingBox>(), 640, 480);

                    using var frame = new Mat();
                    for (int i = 0; i < 3; i++)
                    {
                        if (cts.Token.IsCancellationRequested)
                            return (new List<BoundingBox>(), 640, 480);
                        capture.Read(frame);
                    }

                    if (frame.Empty()) return (new List<BoundingBox>(), 640, 480);
                    return (DetectFromMat(frame, model), frame.Width, frame.Height);
                }, cts.Token);

                return task.Wait(TimeSpan.FromSeconds(20))
                    ? task.Result
                    : (new(), 640, 480);
            }
            catch { return (new(), 640, 480); }
        }



        public (List<BoundingBox> boxes, int width, int height) DetectFromRtspWithSize(
            string url, string model = "classic",
            string? login = null, string? password = null)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            try
            {
                var task = Task.Run(() =>
                {
                    // RTSP с авторизацией: rtsp://user:pass@host:port/path
                    var streamUrl = BuildUrlWithAuth(url, login, password);

                    using var capture = new VideoCapture(streamUrl,
                        VideoCaptureAPIs.FFMPEG); // ← FFMPEG лучше для RTSP чем default
                    if (!capture.IsOpened())
                        return (new List<BoundingBox>(), 640, 480);

                    // Для RTSP пропускаем больше кадров — буфер может быть глубже
                    using var frame = new Mat();
                    for (int i = 0; i < 5; i++)
                    {
                        if (cts.Token.IsCancellationRequested)
                            return (new List<BoundingBox>(), 640, 480);
                        capture.Read(frame);
                    }

                    if (frame.Empty()) return (new List<BoundingBox>(), 640, 480);
                    return (DetectFromMat(frame, model), frame.Width, frame.Height);
                }, cts.Token);

                return task.Wait(TimeSpan.FromSeconds(25))
                    ? task.Result
                    : (new(), 640, 480);
            }
            catch { return (new(), 640, 480); }
        }

        public void Dispose()
        {
            _classicSession.Dispose();
            _extendedSession.Dispose();
            _verticalSession.Dispose();
        }
    }
}