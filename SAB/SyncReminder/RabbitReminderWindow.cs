using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SAB.SyncReminder
{
    internal class RabbitReminderWindow : Window
    {
        private const double RabbitWidth = 184.0;
        private const double RabbitHeight = 88.0;
        private const double CarrotWidth = 36.0;
        private const double CarrotHeight = 60.0;
        private const double RabbitSpeed = 6.2;
        private const int InitialCarrotCount = 4;
        private const int MaximumCarrotCount = 8;

        private readonly IntPtr _ownerHandle;
        private readonly Random _random;
        private readonly DispatcherTimer _moveTimer;
        private readonly DispatcherTimer _frameTimer;
        private readonly DispatcherTimer _carrotTimer;
        private readonly DispatcherTimer _ownerStateTimer;
        private readonly BitmapImage[] _runFrames;
        private readonly BitmapImage _idleFrame;
        private readonly BitmapImage _carrotImageSource;
        private readonly List<CarrotSprite> _carrots;

        private Rect _allowedArea;
        private Canvas _canvas;
        private Image _rabbitImage;
        private Ellipse _shadow;
        private double _rabbitLeft;
        private double _rabbitTop;
        private int _currentFrameIndex;
        private bool _isMovingLeft;

        public RabbitReminderWindow(IntPtr ownerHandle)
        {
            _ownerHandle = ownerHandle;
            _random = new Random();
            _allowedArea = Rect.Empty;
            _carrots = new List<CarrotSprite>();
            _runFrames = LoadRabbitRunFrames();
            _idleFrame = LoadRabbitFrame("Rabbit Idle.png");
            _carrotImageSource = LoadCarrotImage();

            Width = 800;
            Height = 500;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = false;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Content = CreateContent();

            WindowInteropHelper helper = new WindowInteropHelper(this);
            helper.Owner = ownerHandle;

            SourceInitialized += OnSourceInitialized;

            _moveTimer = new DispatcherTimer();
            _moveTimer.Interval = TimeSpan.FromMilliseconds(28);
            _moveTimer.Tick += OnMoveTimerTick;

            _frameTimer = new DispatcherTimer();
            _frameTimer.Interval = TimeSpan.FromMilliseconds(115);
            _frameTimer.Tick += OnFrameTimerTick;

            _carrotTimer = new DispatcherTimer();
            _carrotTimer.Interval = TimeSpan.FromSeconds(2);
            _carrotTimer.Tick += OnCarrotTimerTick;

            _ownerStateTimer = new DispatcherTimer();
            _ownerStateTimer.Interval = TimeSpan.FromMilliseconds(700);
            _ownerStateTimer.Tick += OnOwnerStateTimerTick;
        }

        public void SetAllowedArea(Rect allowedArea)
        {
            _allowedArea = allowedArea;

            if (_allowedArea.IsEmpty)
            {
                return;
            }

            Left = _allowedArea.Left;
            Top = _allowedArea.Top;
            Width = Math.Max(RabbitWidth + 40.0, _allowedArea.Width);
            Height = Math.Max(RabbitHeight + 80.0, _allowedArea.Height);

            if (_canvas != null)
            {
                _canvas.Width = Width;
                _canvas.Height = Height;
            }

            ClampRabbitToWindow();
            ClampCarrotsToWindow();
            UpdateRabbitPosition();
        }

        public void ShowRabbit()
        {
            if (_allowedArea.IsEmpty)
            {
                return;
            }

            if (!IsVisible)
            {
                PrepareRabbitStartPosition();
                Show();
            }

            EnsureInitialCarrots();
            StartTimers();
        }

        public void HideRabbit()
        {
            StopTimers();
            ClearCarrots();

            if (IsVisible)
            {
                Hide();
            }
        }

        public void CloseRabbit()
        {
            StopTimers();
            ClearCarrots();
            Close();
        }

        private UIElement CreateContent()
        {
            _canvas = new Canvas();
            _canvas.Width = Width;
            _canvas.Height = Height;

            _shadow = new Ellipse();
            _shadow.Width = 128;
            _shadow.Height = 18;
            _shadow.Fill = new SolidColorBrush(Color.FromArgb(72, 50, 66, 72));
            _canvas.Children.Add(_shadow);

            _rabbitImage = new Image();
            _rabbitImage.Width = RabbitWidth;
            _rabbitImage.Height = RabbitHeight;
            _rabbitImage.Stretch = Stretch.Uniform;
            _rabbitImage.Source = _runFrames.Length > 0 ? _runFrames[0] : _idleFrame;
            _rabbitImage.RenderTransformOrigin = new Point(0.5, 0.5);
            RenderOptions.SetBitmapScalingMode(_rabbitImage, BitmapScalingMode.HighQuality);
            _canvas.Children.Add(_rabbitImage);

            return _canvas;
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            SyncReminderWindowUtils.MakeWindowNoActivateAndClickThrough(this);
        }

        private void StartTimers()
        {
            if (!_moveTimer.IsEnabled)
            {
                _moveTimer.Start();
            }

            if (!_frameTimer.IsEnabled)
            {
                _frameTimer.Start();
            }

            if (!_carrotTimer.IsEnabled)
            {
                _carrotTimer.Start();
            }

            if (!_ownerStateTimer.IsEnabled)
            {
                _ownerStateTimer.Start();
            }
        }

        private void StopTimers()
        {
            _moveTimer.Stop();
            _frameTimer.Stop();
            _carrotTimer.Stop();
            _ownerStateTimer.Stop();
        }

        private void OnMoveTimerTick(object sender, EventArgs e)
        {
            if (_canvas == null || _carrots.Count == 0)
            {
                return;
            }

            CarrotSprite targetCarrot = FindNearestCarrot();
            if (targetCarrot == null)
            {
                return;
            }

            double rabbitCenterX = _rabbitLeft + RabbitWidth / 2.0;
            double rabbitCenterY = _rabbitTop + RabbitHeight / 2.0;
            double carrotCenterX = targetCarrot.Left + CarrotWidth / 2.0;
            double carrotCenterY = targetCarrot.Top + CarrotHeight / 2.0;
            double directionX = carrotCenterX - rabbitCenterX;
            double directionY = carrotCenterY - rabbitCenterY;
            double distance = Math.Sqrt(directionX * directionX + directionY * directionY);

            if (distance <= 34.0)
            {
                CollectCarrot(targetCarrot);
                return;
            }

            if (distance <= 0.01)
            {
                return;
            }

            _isMovingLeft = directionX < 0;
            _rabbitLeft += directionX / distance * RabbitSpeed;
            _rabbitTop += directionY / distance * RabbitSpeed;

            ClampRabbitToWindow();
            UpdateRabbitPosition();
        }

        private void OnFrameTimerTick(object sender, EventArgs e)
        {
            if (_rabbitImage == null || _runFrames.Length == 0)
            {
                return;
            }

            _currentFrameIndex++;
            if (_currentFrameIndex >= _runFrames.Length)
            {
                _currentFrameIndex = 0;
            }

            _rabbitImage.Source = _runFrames[_currentFrameIndex];
        }

        private void OnCarrotTimerTick(object sender, EventArgs e)
        {
            if (_carrots.Count < MaximumCarrotCount)
            {
                AddCarrotAtRandomPoint();
            }
        }

        private void OnOwnerStateTimerTick(object sender, EventArgs e)
        {
            if (!SyncReminderWindowUtils.CanShowOverlayForRevit(_ownerHandle))
            {
                HideRabbit();
            }
        }

        private void PrepareRabbitStartPosition()
        {
            _rabbitLeft = Math.Max(20.0, Width * 0.12);
            _rabbitTop = Math.Max(20.0, Height * 0.52);
            ClampRabbitToWindow();
            UpdateRabbitPosition();
        }

        private void EnsureInitialCarrots()
        {
            while (_carrots.Count < InitialCarrotCount)
            {
                AddCarrotAtRandomPoint();
            }
        }

        private void AddCarrotAtRandomPoint()
        {
            if (_canvas == null || _carrotImageSource == null)
            {
                return;
            }

            double maxLeft = Math.Max(20.0, Width - CarrotWidth - 24.0);
            double maxTop = Math.Max(20.0, Height - CarrotHeight - 24.0);
            double left = 24.0 + _random.NextDouble() * Math.Max(1.0, maxLeft - 24.0);
            double top = 34.0 + _random.NextDouble() * Math.Max(1.0, maxTop - 34.0);

            Image carrotImage = new Image();
            carrotImage.Width = CarrotWidth;
            carrotImage.Height = CarrotHeight;
            carrotImage.Stretch = Stretch.Uniform;
            carrotImage.Source = _carrotImageSource;
            RenderOptions.SetBitmapScalingMode(carrotImage, BitmapScalingMode.NearestNeighbor);
            Canvas.SetLeft(carrotImage, left);
            Canvas.SetTop(carrotImage, top);

            CarrotSprite carrot = new CarrotSprite();
            carrot.Image = carrotImage;
            carrot.Left = left;
            carrot.Top = top;
            _carrots.Add(carrot);

            int insertIndex = Math.Max(0, _canvas.Children.Count - 2);
            _canvas.Children.Insert(insertIndex, carrotImage);
        }

        private CarrotSprite FindNearestCarrot()
        {
            CarrotSprite nearestCarrot = null;
            double nearestDistance = double.MaxValue;
            double rabbitCenterX = _rabbitLeft + RabbitWidth / 2.0;
            double rabbitCenterY = _rabbitTop + RabbitHeight / 2.0;

            for (int i = 0; i < _carrots.Count; i++)
            {
                CarrotSprite carrot = _carrots[i];
                if (carrot == null)
                {
                    continue;
                }

                double carrotCenterX = carrot.Left + CarrotWidth / 2.0;
                double carrotCenterY = carrot.Top + CarrotHeight / 2.0;
                double directionX = carrotCenterX - rabbitCenterX;
                double directionY = carrotCenterY - rabbitCenterY;
                double distance = directionX * directionX + directionY * directionY;

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestCarrot = carrot;
                }
            }

            return nearestCarrot;
        }

        private void CollectCarrot(CarrotSprite carrot)
        {
            if (carrot == null)
            {
                return;
            }

            if (_canvas != null && carrot.Image != null)
            {
                _canvas.Children.Remove(carrot.Image);
            }

            _carrots.Remove(carrot);

            if (_carrots.Count == 0)
            {
                AddCarrotAtRandomPoint();
            }
        }

        private void ClearCarrots()
        {
            if (_canvas != null)
            {
                for (int i = 0; i < _carrots.Count; i++)
                {
                    CarrotSprite carrot = _carrots[i];
                    if (carrot != null && carrot.Image != null)
                    {
                        _canvas.Children.Remove(carrot.Image);
                    }
                }
            }

            _carrots.Clear();
        }

        private void ClampRabbitToWindow()
        {
            double maxLeft = Math.Max(0.0, Width - RabbitWidth - 12.0);
            double maxTop = Math.Max(0.0, Height - RabbitHeight - 24.0);

            _rabbitLeft = Clamp(_rabbitLeft, 8.0, maxLeft);
            _rabbitTop = Clamp(_rabbitTop, 8.0, maxTop);
        }

        private void ClampCarrotsToWindow()
        {
            double maxLeft = Math.Max(0.0, Width - CarrotWidth - 12.0);
            double maxTop = Math.Max(0.0, Height - CarrotHeight - 12.0);

            for (int i = 0; i < _carrots.Count; i++)
            {
                CarrotSprite carrot = _carrots[i];
                if (carrot == null)
                {
                    continue;
                }

                carrot.Left = Clamp(carrot.Left, 8.0, maxLeft);
                carrot.Top = Clamp(carrot.Top, 8.0, maxTop);

                if (carrot.Image != null)
                {
                    Canvas.SetLeft(carrot.Image, carrot.Left);
                    Canvas.SetTop(carrot.Image, carrot.Top);
                }
            }
        }

        private void UpdateRabbitPosition()
        {
            if (_rabbitImage == null || _shadow == null)
            {
                return;
            }

            Canvas.SetLeft(_rabbitImage, _rabbitLeft);
            Canvas.SetTop(_rabbitImage, _rabbitTop);
            Canvas.SetLeft(_shadow, _rabbitLeft + 28.0);
            Canvas.SetTop(_shadow, _rabbitTop + RabbitHeight - 14.0);

            _rabbitImage.RenderTransform = _isMovingLeft
                ? new ScaleTransform(-1.0, 1.0)
                : new ScaleTransform(1.0, 1.0);
        }

        private static BitmapImage[] LoadRabbitRunFrames()
        {
            List<BitmapImage> frames = new List<BitmapImage>();
            AddFrameIfExists(frames, LoadRabbitFrame("Rabbit Run 001.png"));
            AddFrameIfExists(frames, LoadRabbitFrame("Rabbit Run 002.png"));
            AddFrameIfExists(frames, LoadRabbitFrame("Rabbit Run 003.png"));
            return frames.ToArray();
        }

        private static void AddFrameIfExists(List<BitmapImage> frames, BitmapImage frame)
        {
            if (frames == null || frame == null)
            {
                return;
            }

            frames.Add(frame);
        }

        private static BitmapImage LoadRabbitFrame(string fileName)
        {
            return LoadImageFromAssetFolder("Rabbit", fileName);
        }

        private static BitmapImage LoadCarrotImage()
        {
            return LoadImageFromAssetFolder("Carrot", "Carrot.png");
        }

        private static BitmapImage LoadImageFromAssetFolder(string folderName, string fileName)
        {
            string assemblyFolder = System.IO.Path.GetDirectoryName(typeof(RabbitReminderWindow).Assembly.Location);
            if (string.IsNullOrWhiteSpace(assemblyFolder))
            {
                return null;
            }

            string filePath = System.IO.Path.Combine(assemblyFolder, "SyncReminder", "Assets", folderName, fileName);
            if (!File.Exists(filePath))
            {
                return null;
            }

            BitmapImage image = new BitmapImage();
            image.BeginInit();
            image.UriSource = new Uri(filePath, UriKind.Absolute);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            image.EndInit();
            image.Freeze();
            return image;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (maximum < minimum)
            {
                return minimum;
            }

            if (value < minimum)
            {
                return minimum;
            }

            if (value > maximum)
            {
                return maximum;
            }

            return value;
        }

        private sealed class CarrotSprite
        {
            public Image Image { get; set; }

            public double Left { get; set; }

            public double Top { get; set; }
        }
    }
}
