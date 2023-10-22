using AIPack;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static System.Net.Mime.MediaTypeNames;

namespace AI_WPF {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    
    class Saver : IManagerTools {
        public void Logger(string message) => Console.WriteLine(message);
        public void SaveToCSV(double X, double Y, double W, double H, int Class, string resfilename) {
            string csvfilename = "..\\..\\..\\..\\res.csv";
            FileInfo fileInfo = new FileInfo(csvfilename);


            ///ВЕРНУТЬ СЕМАФОР
            lock (this) {
            //Console.WriteLine(Task.CurrentId);
                if (!fileInfo.Exists || fileInfo.Length == 0) {
                    var file = File.Create(csvfilename);
                    file.Write(Encoding.Default.GetBytes("Filename, Class, X, Y, W, H\n"));
                    file.Close();
                    File.AppendAllText(csvfilename,
                        $"{System.IO.Path.GetFullPath("..\\..\\..\\..\\out_photo\\" + resfilename)}, " +
                        $"{Class.ToString()}, " +
                        $"{X.ToString().Replace(',', '.')}, " +
                        $"{Y.ToString().Replace(',', '.')}, " +
                        $"{W.ToString().Replace(',', '.')}, " +
                        $"{H.ToString().Replace(',', '.')}\n");
                }
                else {
                    File.AppendAllText(csvfilename,
                        $"{System.IO.Path.GetFullPath("..\\..\\..\\..\\out_photo\\" + resfilename)}, " +
                        $"{Class.ToString()}, " +
                        $"{X.ToString().Replace(',', '.')}, " +
                        $"{Y.ToString().Replace(',', '.')}, " +
                        $"{W.ToString().Replace(',', '.')}, " +
                        $"{H.ToString().Replace(',', '.')}\n");
                }
            //Console.WriteLine(Task.CurrentId);
            }
        }
        public void SavePhoto(Image<Rgb24> Image, string filname) {
            Image.SaveAsJpeg("..\\..\\..\\..\\out_photo\\" + filname);
        }
    }

    public class ReadyImages {
        public string Path { get; set; }
        public string Title { get; set; }

        public static List<ReadyImages> GetReadyImages { 
            get {
                List<ReadyImages> list = new List<ReadyImages>();
                var lines= File.ReadLines("..\\..\\..\\..\\res.csv").ToList();
                List<String> files = new List<String>();
                for (var i = 1; i < lines.Count; i++) {
                    files.Add(lines[i].Split(',')[0]);
                }
                //var files = Directory.GetFiles("C:\\Users\\jur_chik\\source\\repos\\441_2_zelenchuk\\out_photo");
                var quary = files.OrderBy(x => x).GroupBy(x => x).OrderByDescending(x => x.Count());
                foreach(var q in quary) {
                    list.Add(new ReadyImages { Path = q.Key, Title = q.Key.Split('\\').ToList().Last() });
                }
                return list;
            }
        }
        public static List<ReadyImages> Empty { get {
                var list = new List<ReadyImages>();
                return list;
            } 
        }
    }

    public partial class MainWindow : Window {

        private CancellationTokenSource cts;
        private ICommand startCommand;

        public MainWindow() {
            InitializeComponent();

            //File.Delete("..\\..\\..\\..\\res.csv");
            //var dir = new DirectoryInfo("..\\..\\..\\..\\out_photo\\");
            //foreach (FileInfo file in dir.GetFiles()) {
            //    file.Delete();
            //}

            startCommand = new AsyncRelayCommand(async _ => {
                cts = new CancellationTokenSource();
                Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
                dialog.DefaultExt = ".jpg";
                dialog.Filter = "Images (.jpg)|*.jpg";
                dialog.Multiselect = true;
                dialog.ShowDialog();

                AIManager aIManager = new AIManager();
                try {
                    aIManager.DownloadModel();
                }
                catch (Exception ex) {
                    Console.WriteLine(ex.Message);
                    return;
                }

                Saver saver = new Saver();
                List<Task> taskList = new List<Task>();

                //File.Delete("..\\..\\..\\..\\res.csv");
                var dir = new DirectoryInfo("..\\..\\..\\..\\out_photo\\");
                foreach (FileInfo file in dir.GetFiles()) {
                    file.Delete();
                }
                if (!Directory.Exists("..\\..\\..\\..\\out_photo\\")) {
                    Directory.CreateDirectory("..\\..\\..\\..\\out_photo\\");
                }

                for (int i = 0; i < dialog.SafeFileNames.Length; i++) {
                    var image = SixLabors.ImageSharp.Image.Load<Rgb24>(dialog.FileNames[i]);
                    CancellationToken token = cts.Token;
                    await aIManager.CallModelAsync(saver, image, dialog.SafeFileNames[i], token);

                    List<ReadyImages> list = new List<ReadyImages>();
                    var files = Directory.GetFiles("C:\\Users\\jur_chik\\source\\repos\\441_2_zelenchuk\\out_photo");
                    for (int j = 0; i < files.Length; i++) {
                        list.Add(new ReadyImages { Path = files[j], Title = files[j] });
                    }
                    this.Dispatcher.Invoke(() => {
                        //MessageBox.Show(String.Join("\n", files));
                        Preview.ItemsSource = ReadyImages.GetReadyImages;
                    });
                }
            });

            DataContext = this;
        }

        public ICommand StartCommand => startCommand;

        private void Preview_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (Preview.ItemsSource != null) {
                ReadyImages image = (ReadyImages)Preview.SelectedItem;
                SelectedImage.Source = new BitmapImage(new Uri(image.Path));
                SelectedText.Text = image.Title;
            }
        }

        private void Cancle_Click(object sender, RoutedEventArgs e) {
            if (cts != null)
                cts.Cancel();
        }
        
        private void Delete_Click(object sender, RoutedEventArgs e) {
            Preview.ItemsSource = ReadyImages.Empty;
            SelectedImage.Source = new BitmapImage();
            SelectedText.Text= String.Empty;
        }
    }


    public class AsyncRelayCommand : ICommand {
        private readonly Func<object, bool> canExecute;
        private readonly Func<object, Task> executeAsync;
        private bool isExecuting;

        public AsyncRelayCommand(Func<object, Task> executeAsync, Func<object, bool> canExecute = null) {
            this.executeAsync = executeAsync;
            this.canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter) {
            if (isExecuting) {
                return false;
            }
            else {
                return canExecute is null || canExecute(parameter);
            }
        }

        public void Execute(object? parameter) {
            if (!isExecuting) {
                isExecuting = true;
                executeAsync(parameter).ContinueWith(_ => {
                    isExecuting = false;
                    CommandManager.InvalidateRequerySuggested();
                }, scheduler: TaskScheduler.FromCurrentSynchronizationContext());
            }
        }
    }


}
