using AIPack;
using Microsoft.EntityFrameworkCore;
using ObjectDetectionWPF.ViewModel;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Imaging;


namespace AI_WPF {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>

    public class Photo {
        public int PhotoId { get; set; }
        public byte[] SourcePhoto { get; set; }
        public byte[] ResultPhoto { get; set; }
        public string Path { get; set; }
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
            modelBuilder.Entity<Photo>().Property(ph => ph.PhotoId).ValueGeneratedOnAdd();
        }
    }

    public class ReadyImages {
        public ImageSharpImageSource<Rgb24> SourceImage { get; set; }
        public ImageSharpImageSource<Rgb24> ResultImage { get; set; }
        public List<string> Classes { get; set; }
        public string Title { get; set; }   
        public int ObjectsCount { get; set; }
        public static List<ReadyImages> Empty { get => new List<ReadyImages>(); }
    }

    public partial class MainWindow : Window {

        private CancellationTokenSource cts;
        private ICommand startCommand;

        public MainWindow() {
            InitializeComponent();
            DrawPhotos();
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

                

                for (int i = 0; i < dialog.SafeFileNames.Length; i++) {
                    
                    //проверка на вхождение в бд
                    int count;
                    using (var db = new AIPhotosContext()) {
                        count = db.Photos.Where(ph => ph.Path == dialog.FileNames[i]).Count();
                    }
                    if (count != 0) {
                        //вытаскивание из бд во вьюшку
                        DrawPhotos();
                        continue;
                    }

                    //обработка и добавление в бд
                    var image = SixLabors.ImageSharp.Image.Load<Rgb24>(dialog.FileNames[i]);
                    CancellationToken token = cts.Token;
                    var task = await aIManager.CallModelAsync(image, token);
                    using (var db = new AIPhotosContext()) {
                        List<Class> classes = new List<Class>();
                        Photo photo = new Photo();
                        foreach(var c in task.Classes) {
                            classes.Add(db.Classes.Where(db_c => db_c.ClassId == (c+1)).First());
                        }
                        
                        using (MemoryStream memoryStream = new MemoryStream()) {
                            image.Save(memoryStream, new JpegEncoder());
                            photo.SourcePhoto = memoryStream.ToArray();
                        }
                        using (MemoryStream memoryStream = new MemoryStream()) {
                            task.ResultImage.Save(memoryStream, new JpegEncoder());
                            photo.ResultPhoto = memoryStream.ToArray();
                        }

                        photo.Path = dialog.FileNames[i];
                        photo.Classes = classes;

                        db.Add(photo);
                        db.SaveChanges();
                    }

                    //вытаскивание из бд во вьюшку
                    DrawPhotos();
                }

                Delete.IsEnabled = true;
            });

            DataContext = this;
        }

        private void DrawPhotos() {
            List<Photo> photoList = new List<Photo>();
            List<ReadyImages> list = new List<ReadyImages>();

            using (var db = new AIPhotosContext()) {
                photoList = db.Photos.ToList();

                foreach (var photo in photoList) {
                    Image<Rgb24> resImg;
                    Image<Rgb24> srcImg;
                    using (MemoryStream ms = new MemoryStream(photo.SourcePhoto)) {
                        srcImg = SixLabors.ImageSharp.Image.Load<Rgb24>(ms);
                    }
                    using (MemoryStream ms = new MemoryStream(photo.ResultPhoto)) {
                        resImg = SixLabors.ImageSharp.Image.Load<Rgb24>(ms);
                    }
                    var c = photo.Classes.Select(c => c.ClassName).ToList();
                    list.Add(new ReadyImages() {
                        SourceImage = new ImageSharpImageSource<Rgb24>(srcImg),
                        ResultImage = new ImageSharpImageSource<Rgb24>(resImg),
                        Title = photo.Path.Split('\\').Last(),
                        Classes = photo.Classes.Select(c => c.ClassName).ToList(),
                        ObjectsCount = photo.Classes.Count
                    });
                }

                var ordered_list = list.OrderByDescending(x => x.ObjectsCount).ThenBy(x => x.Title);

                this.Dispatcher.Invoke(() => {
                    Preview.ItemsSource = null;
                    Preview.ItemsSource = ordered_list;
                });
            }
        }

        public ICommand StartCommand => startCommand;

        private void Preview_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (Preview.SelectedItem != null) {
                ReadyImages image = (ReadyImages)Preview.SelectedItem;
                SelectedImage.Source = image.ResultImage;
                SelectedText.Text = String.Join(", ", image.Classes);
            }
        }

        private async void Cancle_Click(object sender, RoutedEventArgs e) {
            if (cts != null)
                cts.Cancel();
        }

        private void Delete_Click(object sender, RoutedEventArgs e) {
            using (var db = new AIPhotosContext()) {
                db.Database.ExecuteSqlRaw("DELETE from Photos");
            }
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