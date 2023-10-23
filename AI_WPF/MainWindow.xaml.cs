using AIPack;
using ObjectDetectionWPF.ViewModel;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace AI_WPF {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public class ReadyImages {
        public ImageSharpImageSource<Rgb24> Image { get; set; }
        public string Title { get; set; }
        public int ObjectsCount { get; set; }
        public static List<ReadyImages> Empty { get => new List<ReadyImages>(); }
    }

    public partial class MainWindow : Window {

        private CancellationTokenSource cts;
        private ICommand startCommand;

        public MainWindow() {
            InitializeComponent();

            startCommand = new AsyncRelayCommand(async _ => {
                Delete.IsEnabled = false; 
                cts = new CancellationTokenSource();

                Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog();
                dialog.DefaultExt = ".jpg";
                dialog.Filter = "Images (.jpg)|*.jpg";
                dialog.Multiselect = true;
                dialog.ShowDialog();
                AIManager aIManager = new AIManager();
                Preview.ItemsSource = ReadyImages.Empty;
                SelectedImage.Source = new BitmapImage();
                SelectedText.Text= string.Empty;

                try {
                    try {
                        await Task.Run(() => aIManager.DownloadModel());
                    }
                    catch (Exception ex) {
                        Console.WriteLine(ex.Message);
                        return;
                    }
                }
                catch(Exception ex) {
                    MessageBox.Show(ex.Message, ex.StackTrace);
                }

                List<ReadyImages> list = new List<ReadyImages>();

                for (int i = 0; i < dialog.SafeFileNames.Length; i++) {
                    var image = SixLabors.ImageSharp.Image.Load<Rgb24>(dialog.FileNames[i]);
                    CancellationToken token = cts.Token;
                    var task = await aIManager.CallModelAsync(image, token);

                    list.Add(new ReadyImages() { Image = new ImageSharpImageSource<Rgb24>(task.ResultImage), Title = dialog.SafeFileNames[i], ObjectsCount = task.ObjectCount });
                    var ordered_list = list.OrderByDescending(x => x.ObjectsCount).ThenBy(x => x.Title);
                    
                    this.Dispatcher.Invoke(() => {
                        Preview.ItemsSource = null;
                        Preview.ItemsSource = ordered_list;
                    });
                }

                Delete.IsEnabled = true;
            });

            DataContext = this;
        }

        public ICommand StartCommand => startCommand;

        private void Preview_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (Preview.SelectedItem != null) {
                ReadyImages image = (ReadyImages)Preview.SelectedItem;
                SelectedImage.Source = image.Image;
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
            SelectedText.Text= string.Empty;
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