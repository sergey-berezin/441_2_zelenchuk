using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.OnnxRuntime;
using SixLabors.Fonts;
using SixLabors.ImageSharp.Drawing.Processing;
using System.Net;

namespace AIPack {
    public class AIManager {

        private class UnsetTools : IManagerTools {
            public void Logger(string message) => Console.WriteLine("Not implemented");
            public void SaveExtraData(double X, double Y, double W, double H, int Class, string resfilename) => Console.WriteLine("Not implemented");
            public void SavePhoto(Image<Rgb24> Image, string filname) => Console.WriteLine("Not implemented");
        }

        private InferenceSession ? session;

        private (double, double)[] anchors = new (double, double)[] {
               (1.08, 1.19),
               (3.42, 4.41),
               (6.63, 11.38),
               (9.42, 5.11),
               (16.62, 10.52)
            };

        private string[] labels = new string[] {
                "aeroplane", "bicycle", "bird", "boat", "bottle",
                "bus", "car", "cat", "chair", "cow",
                "diningtable", "dog", "horse", "motorbike", "person",
                "pottedplant", "sheep", "sofa", "train", "tvmonitor"
            };

        private const int CellCount = 13; // 13x13 ячеек
        private const int BoxCount = 5; // 5 прямоугольников в каждой ячейке
        private const int ClassCount = 20; // 20 классов

        private bool IsDownloaded { get; set; }

        public AIManager() {
            session = null;
            IsDownloaded = false;
        }

        public void DownloadModel() {
            WebClient webclient = new WebClient();
            string url = "https://storage.yandexcloud.net/dotnet4/tinyyolov2-8.onnx";
            string modelFileName = "tinyyolov2-8.onnx";
            int downloadCounter = 0;
            if (File.Exists(modelFileName)) {
                IsDownloaded = true;
                session = new InferenceSession("tinyyolov2-8.onnx");
                return;
            }
            while (!File.Exists(modelFileName) && downloadCounter++ != 5) {
                try {
                    webclient.DownloadFile(url, modelFileName);
                    IsDownloaded = true;
                }
                catch (WebException ex) {
                    IsDownloaded = false;
                    throw new Exception("Unable to dowload file. Try again later.");
                }
            }
            if (downloadCounter == 5) {
                IsDownloaded = false;
                throw new Exception("Unable to dowload file. Try again later.");
            }

            session = new InferenceSession("tinyyolov2-8.onnx");
        }

        public Task<ResultData> CallModelAsync(Image<Rgb24> image, CancellationToken token, string filename = "unset", IManagerTools? tools = null) {
            return Task.Factory.StartNew(() => CallModel(image, filename, tools), token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private ResultData CallModel(Image<Rgb24> image, string filename, IManagerTools tools) {
            
            if (tools == null) {
                tools = new UnsetTools();
            }

            if (!IsDownloaded) {
                tools.Logger("Model is not downloaded.");
                return new ResultData();
            }
            tools.Logger($"Started file: {filename}");

            int imageWidth = image.Width;
            int imageHeight = image.Height;

            // Размер изображения
            const int TargetSize = 416;

            // Изменяем размер изображения до 416 x 416
            var resized = image.Clone(x => {
                x.Resize(new ResizeOptions {
                    Size = new Size(TargetSize, TargetSize),
                    Mode = ResizeMode.Pad // Дополнить изображение до указанного размера с сохранением пропорций
                });
            });

            // Перевод пикселов в тензор и нормализация
            var input = new DenseTensor<float>(new[] { 1, 3, TargetSize, TargetSize });
            resized.ProcessPixelRows(pa => {
                for (int y = 0; y < TargetSize; y++) {
                    Span<Rgb24> pixelSpan = pa.GetRowSpan(y);
                    for (int x = 0; x < TargetSize; x++) {
                        input[0, 0, y, x] = pixelSpan[x].R;
                        input[0, 1, y, x] = pixelSpan[x].G;
                        input[0, 2, y, x] = pixelSpan[x].B;
                    }
                }
            });
            
            // Подготавливаем входные данные нейросети. Имя input задано в файле модели
            var inputs = new List<NamedOnnxValue>
            {
               NamedOnnxValue.CreateFromTensor("image", input),
            };

            // Вычисляем предсказание нейросетью
            IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results;

            //Thread.Sleep(1000);
            //Thread.Sleep(10000);
            lock (session) {
                results = session.Run(inputs);
            }

            // Получаем результаты
            var outputs = results.First().AsTensor<float>();

            int cellSize = TargetSize / CellCount;

            var boundingBoxes = resized.Clone();

            List<ObjectBox> objects = new();

            for (var row = 0; row < CellCount; row++)
                for (var col = 0; col < CellCount; col++)
                    for (var box = 0; box < BoxCount; box++) {
                        var rawX = outputs[0, (5 + ClassCount) * box, row, col];
                        var rawY = outputs[0, (5 + ClassCount) * box + 1, row, col];

                        var rawW = outputs[0, (5 + ClassCount) * box + 2, row, col];
                        var rawH = outputs[0, (5 + ClassCount) * box + 3, row, col];

                        var x = (float)((col + Sigmoid(rawX)) * cellSize);
                        var y = (float)((row + Sigmoid(rawY)) * cellSize);

                        var w = (float)(Math.Exp(rawW) * anchors[box].Item1 * cellSize);
                        var h = (float)(Math.Exp(rawH) * anchors[box].Item2 * cellSize);

                        var conf = Sigmoid(outputs[0, (5 + ClassCount) * box + 4, row, col]);

                        if (conf > 0.5) {
                            var classes
                            = Enumerable
                            .Range(0, ClassCount)
                            .Select(i => outputs[0, (5 + ClassCount) * box + 5 + i, row, col])
                            .ToArray();
                            objects.Add(new ObjectBox(x - w / 2, y - h / 2, x + w / 2, y + h / 2, conf, IndexOfMax(Softmax(classes))));
                        }

                        if (conf > 0.01) {
                            boundingBoxes.Mutate(ctx => {
                                ctx.DrawPolygon(
                                    Pens.Solid(Color.Green, 1),
                                    new PointF[] {
                                        new PointF(x - w / 2, y - h / 2),
                                        new PointF(x + w / 2, y - h / 2),
                                        new PointF(x + w / 2, y + h / 2),
                                        new PointF(x - w / 2, y + h / 2)
                                    });
                            });
                        }
                    }
            //boundingBoxes.Save("boundingboxes.jpg");

            var annotated = resized.Clone();
            Annotate(annotated, objects);
            //annotated.SaveAsJpeg("annotated.jpg");

            // Убираем дубликаты
            for (int i = 0; i < objects.Count; i++) {
                var o1 = objects[i];
                for (int j = i + 1; j < objects.Count;) {
                    var o2 = objects[j];
                    if (o1.Class == o2.Class && o1.IoU(o2) > 0.6) {
                        if (o1.Confidence < o2.Confidence) {
                            objects[i] = o1 = objects[j];
                        }
                        objects.RemoveAt(j);
                    }
                    else {
                        j++;
                    }
                }
            }

            foreach (var obj in objects)
                tools.SaveExtraData(obj.XMin, obj.YMax, obj.XMax - obj.XMin, obj.YMax - obj.YMin, obj.Class, filename.Split('.')[0] + "_out.jpg");

            var final = resized.Clone();
            Annotate(final, objects);
            tools.SavePhoto(final, filename.Split('.')[0] + "_out.jpg");

            tools.Logger($"Finished file: {filename}");
            return new ResultData() { ResultImage = final, ObjectCount = objects.Count };
        }

        private float Sigmoid(float value) {
            var e = (float)Math.Exp(value);
            return e / (1.0f + e);
        }

        private float[] Softmax(float[] values) {
            var exps = values.Select(v => Math.Exp(v));
            var sum = exps.Sum();
            return exps.Select(e => (float)(e / sum)).ToArray();
        }

        private int IndexOfMax(float[] values) {
            int idx = 0;
            for (int i = 1; i < values.Length; i++)
                if (values[i] > values[idx])
                    idx = i;
            return idx;
        }

        private void Annotate(Image<Rgb24> target, IEnumerable<ObjectBox> objects) {
            foreach (var objbox in objects) {
                target.Mutate(ctx => {
                    ctx.DrawPolygon(
                        Pens.Solid(Color.Blue, 2),
                        new PointF[] {
                                new PointF((float)objbox.XMin, (float)objbox.YMin),
                                new PointF((float)objbox.XMin, (float)objbox.YMax),
                                new PointF((float)objbox.XMax, (float)objbox.YMax),
                                new PointF((float)objbox.XMax, (float)objbox.YMin)
                        });

                    ctx.DrawText(
                        $"{labels[objbox.Class]}",
                        SystemFonts.Families.First().CreateFont(16),
                        Color.Blue,
                        new PointF((float)objbox.XMin, (float)objbox.YMax));
                });
            }
        }
    }
    public record ObjectBox(double XMin, double YMin, double XMax, double YMax, double Confidence, int Class) {
        public double IoU(ObjectBox b2) =>
            (Math.Min(XMax, b2.XMax) - Math.Max(XMin, b2.XMin)) * (Math.Min(YMax, b2.YMax) - Math.Max(YMin, b2.YMin)) /
            ((Math.Max(XMax, b2.XMax) - Math.Min(XMin, b2.XMin)) * (Math.Max(YMax, b2.YMax) - Math.Min(YMin, b2.YMin)));
    }

    public interface IManagerTools {
        public void Logger(string message);
        public void SaveExtraData(double X, double Y, double W, double H, int Class, string resfilename);
        public void SavePhoto(Image<Rgb24> Image, string filname);
    }

    public class ResultData {
        public int ObjectCount { get; set; }
        public Image<Rgb24> ResultImage { get; set; }

    }
}