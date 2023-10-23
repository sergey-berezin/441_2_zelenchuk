using AIPack;
using System.Text;

namespace AI_App {
    class Program {

        static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

        async static Task Main(string[] args) {
            AIManager aIManager = new AIManager();
            try {
                aIManager.DownloadModel();
            }
            catch(Exception ex) {
                Console.WriteLine(ex.Message);
                return;
            }

            Tools tools = new Tools();
            List<Task> taskList = new List<Task>();
            using CancellationTokenSource cts = new CancellationTokenSource();

            if (!Directory.Exists("..\\..\\..\\..\\out_photo\\")) {
                Directory.CreateDirectory("..\\..\\..\\..\\out_photo\\");
            }

            var keyPressTask = Task.Run(() => { 
                ConsoleKeyInfo keyInfo = Console.ReadKey();
                cts.Cancel();
            });
            
            foreach (var filename in args) {
                var image = Image.Load<Rgb24>("..\\..\\..\\..\\in_photo\\" + filename);
                CancellationToken token = cts.Token;
                Task task = aIManager.CallModelAsync(image, token, filename, tools);
                taskList.Add(task);
            }

            try {
                Task.WaitAll(taskList.ToArray());
            }
            catch (AggregateException ex) {
                Console.WriteLine("Tasks were cancaled.");
            }
            finally {
                semaphore.Dispose();
            }
        }

        class Tools : IManagerTools {
            public void Logger(string message) => Console.WriteLine(message);
            public void SaveExtraData(double X, double Y, double W, double H, int Class, string resfilename) {
                string csvfilename = "..\\..\\..\\..\\res.csv";
                FileInfo fileInfo = new FileInfo(csvfilename);

                semaphore.Wait();
                //Console.WriteLine(Task.CurrentId);
                if (!fileInfo.Exists || fileInfo.Length == 0) {
                    var file = File.Create(csvfilename);
                    file.Write(Encoding.Default.GetBytes("Filename, Class, X, Y, W, H\n"));
                    file.Close();
                    File.AppendAllText(csvfilename,
                        $"{Path.GetFullPath("..\\..\\..\\..\\out_photo\\" + resfilename)}, " +
                        $"{Class.ToString()}, " +
                        $"{X.ToString().Replace(',', '.')}, " +
                        $"{Y.ToString().Replace(',', '.')}, " +
                        $"{W.ToString().Replace(',', '.')}, " +
                        $"{H.ToString().Replace(',', '.')}\n");
                }
                else {
                    File.AppendAllText(csvfilename,
                        $"{Path.GetFullPath("..\\..\\..\\..\\out_photo\\" + resfilename)}, " +
                        $"{Class.ToString()}, " +
                        $"{X.ToString().Replace(',', '.')}, " +
                        $"{Y.ToString().Replace(',', '.')}, " +
                        $"{W.ToString().Replace(',', '.')}, " +
                        $"{H.ToString().Replace(',', '.')}\n");
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