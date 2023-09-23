using AIPack;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Reflection;
using System;
using System.Text;
using System.Threading;

namespace AI_App {
    class Program {

        static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        async static Task Main(string[] args) {
            AIManager aIManager = new AIManager();
            Saver saver = new Saver();
            List<Task> taskList = new List<Task>();
            CancellationTokenSource cts = new CancellationTokenSource();

            var keyPressTask = Task.Run(() => { 
                ConsoleKeyInfo keyInfo = Console.ReadKey();
                cts.Cancel();
            });

            foreach (var filename in args) {
                var image = Image.Load<Rgb24>("..\\..\\..\\..\\in_photo\\" + filename);
                CancellationToken token = cts.Token;
                Task task = aIManager.CallModelAsync(saver, image, filename, token);
                taskList.Add(task);
            }

            try {
                Task.WaitAll(taskList.ToArray());
            }
            catch (AggregateException ex) {
                Console.WriteLine("Tasks were cancaled.");
            }
            finally {
                cts.Dispose();
                semaphore.Dispose();
            }
        }

        class Saver : ISave {
            public void SaveToCSV(double X, double Y, double W, double H, int Class) {
                string filename = "..\\..\\..\\..\\res.csv";
                FileInfo fileInfo = new FileInfo(filename);

                semaphore.Wait();
                //Console.WriteLine(Task.CurrentId);
                if (!fileInfo.Exists || fileInfo.Length == 0) {
                    var file = File.Create(filename);
                    file.Write(Encoding.Default.GetBytes("X, Y, W, H, Class\n"));
                    file.Close();
                }
                else {
                    File.AppendAllText(filename, $"{X.ToString().Replace(',', '.')},{Y.ToString().Replace(',', '.')},{W.ToString().Replace(',', '.')},{H.ToString().Replace(',', '.')},{Class.ToString()}\n");
                }
                //Console.WriteLine(Task.CurrentId);
                semaphore.Release();
            }
            public void SavePhoto(Image<Rgb24> Image, string filname) {
                Image.SaveAsJpeg("..\\..\\..\\..\\out_photo\\" + filname);
            }
        }
    }

}