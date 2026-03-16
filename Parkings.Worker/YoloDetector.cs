using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System.Drawing;

namespace Parkings.Worker
{
    public class YoloDetector : IDisposable
    {
        private readonly InferenceSession _session;
        private const int InputSize = 640;
        private const float ConfThreshold = 0.3f;
        private const int CarClassId = 2;
        private const int TruckClassId = 7;

        public YoloDetector(string modelPath)
        {
            _session = new InferenceSession(modelPath);
        }

        public List<BoundingBox> Detect(byte[] imageBytes)
        {
            using var mat = Cv2.ImDecode(imageBytes, ImreadModes.Color);
            return DetectFromMat(mat);
        }

        public List<BoundingBox> DetectFromMat(Mat image)
        {
            float scaleX = (float)image.Width / InputSize;
            float scaleY = (float)image.Height / InputSize;

            using var resized = image.Resize(new OpenCvSharp.Size(InputSize, InputSize));
            var tensor = MatToTensor(resized);

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("images", tensor)
            };

            using var results = _session.Run(inputs);
            var output = results.First().AsTensor<float>();

            return ParseOutput(output, scaleX, scaleY);
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

        private static List<BoundingBox> ParseOutput(Tensor<float> output, float scaleX, float scaleY)
        {
            var boxes = new List<BoundingBox>();
            int numPredictions = output.Dimensions[1];

            for (int i = 0; i < numPredictions; i++)
            {
                float conf = output[0, i, 4];
                int cls = (int)Math.Round(output[0, i, 5]);

                if ((cls == CarClassId || cls == TruckClassId) && conf > ConfThreshold)
                {
                    // YOLOv10 отдаёт в координатах InputSize — масштабируем обратно
                    boxes.Add(new BoundingBox(
                        output[0, i, 0] * scaleX,
                        output[0, i, 1] * scaleY,
                        output[0, i, 2] * scaleX,
                        output[0, i, 3] * scaleY
                    ));
                }
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

        public (List<BoundingBox> boxes, int width, int height) DetectWithSize(byte[] imageBytes)
        {
            using var mat = Cv2.ImDecode(imageBytes, ImreadModes.Color);
            return (DetectFromMat(mat), mat.Width, mat.Height);
        }

        public (List<BoundingBox> boxes, int width, int height) DetectFromMjpegWithSize(string url)
        {
            using var capture = new VideoCapture(url);
            if (!capture.IsOpened()) return (new(), 640, 480);

            using var frame = new Mat();
            // Пропускаем первые кадры — бывают пустые
            for (int i = 0; i < 3; i++)
                capture.Read(frame);

            if (frame.Empty()) return (new(), 640, 480);

            int w = frame.Width, h = frame.Height;
            return (DetectFromMat(frame), w, h);
        }

        public void Dispose() => _session.Dispose();
    }
}