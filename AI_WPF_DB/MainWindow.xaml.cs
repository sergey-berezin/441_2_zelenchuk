using AIPack;
using Microsoft.EntityFrameworkCore;
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

    public class Photo {
        public int PhotoId { get; set; }
        //public byte[] PhotoBytes { get; set; }
        public string SoursePath { get; set; }
        public string ResultPath { get; set; }
        virtual public ICollection<Class> Classes { get; set; }
    }

    public class Class {
        public int ClassId { get; set; }
        public string ClassName { get; set; }
        virtual public ICollection<Photo> Photos { get; set; }
    }

    class AIPhotosContext : DbContext {
        public DbSet<Photo> Photos { get; set; }
        public DbSet<Class> Classes { get; set; }

        public AIPhotosContext() {
            Database.EnsureDeleted();
            Database.EnsureCreated();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder o)
            => o.UseLazyLoadingProxies().UseSqlite("Data Source=aiphotos.db");

        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            modelBuilder.Entity<Class>().HasData(
                new Class[] {
                    new Class { ClassId = 1, ClassName = "aeroplane" },
                    new Class { ClassId = 2, ClassName = "bicycle" },
                    new Class { ClassId = 3, ClassName = "bird" },
                    new Class { ClassId = 4, ClassName = "boat" },
                    new Class { ClassId = 5, ClassName = "bottle" },
                    new Class { ClassId = 6, ClassName = "bus" },
                    new Class { ClassId = 7, ClassName = "car" },
                    new Class { ClassId = 8, ClassName = "cat" },
                    new Class { ClassId = 9, ClassName = "chair" },
                    new Class { ClassId = 10, ClassName = "cow" },
                    new Class { ClassId = 11, ClassName = "diningtable" },
                    new Class { ClassId = 12, ClassName = "dog" },
                    new Class { ClassId = 13, ClassName = "horse" },
                    new Class { ClassId = 14, ClassName = "motorbike" },
                    new Class { ClassId = 15, ClassName = "person" },
                    new Class { ClassId = 16, ClassName = "pottedplant" },
                    new Class { ClassId = 17, ClassName = "sheep" },
                    new Class { ClassId = 18, ClassName = "sofa" },
                    new Class { ClassId = 19, ClassName = "train" },
                    new Class { ClassId = 20, ClassName = "tvmonitor" }
            });
        }
    }

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

            //using(var db = new AIPhotosContext()) {
            //    //var a = db.Authors.Where(a => a.FirstName.StartsWith("F")).FirstOrDefault();
            //    var ph = new Photo() { PhotoId = 123, ResultPath = "text", SoursePath = "text" };
            //    var c = new Class() { ClassId = 1, ClassName = "Test" };
            //    ph.Classes = new List<Class>() { c };
            //    ph.Classes.Add(c);
            //    //b.Authors = new List<Author>();
            //    //b.Authors.Add(a);
            //    //a.Books = new List<Book>();
            //    //a.Books.Add(b);
            //    //db.Add(b);

            //    db.SaveChanges();
            //}

            startCommand = new AsyncRelayCommand(async _ => {

                using (var db = new AIPhotosContext()) {
                    //var a = db.Authors.Where(a => a.FirstName.StartsWith("F")).FirstOrDefault();
                    var ph = new Photo() { PhotoId = 123, ResultPath = "text", SoursePath = "text" };
                    var c = db.Classes.Where(x => x.ClassName == "cat").ToArray();
                    ph.Classes = new List<Class>() { c[0] };
                    //b.Authors = new List<Author>();
                    //b.Authors.Add(a);
                    //a.Books = new List<Book>();
                    //a.Books.Add(b);
                    //db.Add(b);
                    //db.Add(ph);

                    db.SaveChanges();
                }

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
                SelectedText.Text = string.Empty;

                try {
                    try {
                        await Task.Run(() => aIManager.DownloadModel());
                    }
                    catch (Exception ex) {
                        Console.WriteLine(ex.Message);
                        return;
                    }
                }
                catch (Exception ex) {
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
            SelectedText.Text = string.Empty;
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