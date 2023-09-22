using AIPack;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text;
using System.Threading;

namespace AI_App {
    class Program {
        async static Task Main(string[] args) {
            AIManager aIManager = new AIManager();
            Saver saver = new Saver();
            List<Task> taskList = new List<Task>();
            CancellationTokenSource cts = new CancellationTokenSource();

            foreach (var filename in args) {
                var image = Image.Load<Rgb24>("..\\..\\..\\..\\in_photo\\" + filename);
                CancellationToken token = cts.Token;
                Task task = aIManager.CallModelAsync(saver, image, filename, token);
                taskList.Add(task);
            }

            //Task.WaitAll(taskList.ToArray());
            cts.Cancel();
            Task.WaitAll(taskList.ToArray());
            //foreach (var task in taskList) {
            //    cts.Cancel();
            //    Console.WriteLine(task.IsCanceled);
            //}
        }

        class Saver : ISave {
            public void SaveToCSV(double X, double Y, double W, double H, int Class) {
                lock(this) { 
                    if (!File.Exists("..\\..\\..\\..\\res.csv")) {
                        var file = File.Create("..\\..\\..\\..\\res.csv");
                        file.Write(Encoding.Default.GetBytes("X, Y, W, H, Class\n"));
                    }
                    else {
                        File.AppendAllText("..\\..\\..\\..\\res.csv", $"{X.ToString().Replace(',', '.')},{Y.ToString().Replace(',', '.')},{W.ToString().Replace(',', '.')},{H.ToString().Replace(',', '.')},{Class.ToString()}\n");
                    }
                }
            }
            public void SavePhoto(Image<Rgb24> Image, string filname) {
                Image.SaveAsJpeg("..\\..\\..\\..\\out_photo\\" + filname);
            }
        }
    }

}