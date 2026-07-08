using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SAB.SyncReminder
{
    internal class DuckReminderWindow : Window
    {
        private readonly DispatcherTimer _moveTimer;
        private readonly DispatcherTimer _frameTimer;
        private readonly DispatcherTimer _messageTimer;
        private readonly Random _random;
        private readonly BitmapImage[] _runningFrames;
        private readonly string[] _messages;
        private Rect _allowedArea;
        private double _velocityX;
        private double _velocityY;
        private int _currentFrameIndex;
        private int _currentMessageIndex;
        private Image _duckImage;
        private TextBlock _bubbleText;

        public DuckReminderWindow(IntPtr ownerHandle)
        {
            Width = 430;
            Height = 246;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = false;
            AllowsTransparency = true;
            Background = Brushes.Transparent;

            _random = new Random();
            _velocityX = 1.7;
            _velocityY = 1.0;
            _allowedArea = Rect.Empty;
            _runningFrames = LoadRunningFrames();
            _messages = CreateMessages();
            _currentMessageIndex = -1;

            Content = CreateDuckContent();

            WindowInteropHelper helper = new WindowInteropHelper(this);
            helper.Owner = ownerHandle;

            SourceInitialized += OnSourceInitialized;
            MouseEnter += OnMouseEnter;

            _moveTimer = new DispatcherTimer();
            _moveTimer.Interval = TimeSpan.FromMilliseconds(28);
            _moveTimer.Tick += OnMoveTimerTick;

            _frameTimer = new DispatcherTimer();
            _frameTimer.Interval = TimeSpan.FromMilliseconds(140);
            _frameTimer.Tick += OnFrameTimerTick;

            _messageTimer = new DispatcherTimer();
            _messageTimer.Interval = TimeSpan.FromSeconds(5);
            _messageTimer.Tick += OnMessageTimerTick;
        }

        public void SetAllowedArea(Rect allowedArea)
        {
            _allowedArea = allowedArea;

            if (!_allowedArea.IsEmpty && (Left < _allowedArea.Left || Top < _allowedArea.Top))
            {
                PutDuckInsideAllowedArea();
            }
        }

        public void ShowDuck()
        {
            if (!IsVisible)
            {
                PutDuckInsideAllowedArea();
                Show();
            }

            if (!_moveTimer.IsEnabled)
            {
                _moveTimer.Start();
            }

            if (!_frameTimer.IsEnabled)
            {
                _frameTimer.Start();
            }

            if (!_messageTimer.IsEnabled)
            {
                _messageTimer.Start();
            }

            SayNextMessage();
        }

        public void HideDuck()
        {
            _moveTimer.Stop();
            _frameTimer.Stop();
            _messageTimer.Stop();

            if (IsVisible)
            {
                Hide();
            }
        }

        public void CloseDuck()
        {
            _moveTimer.Stop();
            _frameTimer.Stop();
            _messageTimer.Stop();
            Close();
        }

        private UIElement CreateDuckContent()
        {
            Canvas canvas = new Canvas();
            canvas.Width = Width;
            canvas.Height = Height;

            Border bubble = new Border();
            bubble.Width = 394;
            bubble.Height = 104;
            bubble.CornerRadius = new CornerRadius(12);
            bubble.Padding = new Thickness(14, 8, 14, 8);
            bubble.Background = new SolidColorBrush(Color.FromArgb(238, 255, 255, 255));
            bubble.BorderBrush = new SolidColorBrush(Color.FromArgb(200, 120, 148, 165));
            bubble.BorderThickness = new Thickness(1);
            bubble.ClipToBounds = true;

            _bubbleText = new TextBlock();
            _bubbleText.Text = "Кря! Давай синхронизируйся!";
            _bubbleText.FontFamily = new FontFamily("Segoe UI");
            _bubbleText.FontSize = 14;
            _bubbleText.FontWeight = FontWeights.SemiBold;
            _bubbleText.Foreground = new SolidColorBrush(Color.FromRgb(33, 43, 52));
            _bubbleText.TextWrapping = TextWrapping.Wrap;
            _bubbleText.TextTrimming = TextTrimming.CharacterEllipsis;
            _bubbleText.LineHeight = 18;
            _bubbleText.Height = 86;
            _bubbleText.MaxHeight = 86;
            _bubbleText.VerticalAlignment = VerticalAlignment.Center;
            bubble.Child = _bubbleText;

            Canvas.SetLeft(bubble, 12);
            Canvas.SetTop(bubble, 4);
            canvas.Children.Add(bubble);

            Polygon bubbleTail = new Polygon();
            bubbleTail.Points = new PointCollection
            {
                new Point(86, 103),
                new Point(106, 103),
                new Point(98, 118)
            };
            bubbleTail.Fill = bubble.Background;
            bubbleTail.Stroke = bubble.BorderBrush;
            bubbleTail.StrokeThickness = 1;
            canvas.Children.Add(bubbleTail);

            Ellipse shadow = new Ellipse();
            shadow.Width = 104;
            shadow.Height = 18;
            shadow.Fill = new SolidColorBrush(Color.FromArgb(70, 50, 66, 72));
            Canvas.SetLeft(shadow, 72);
            Canvas.SetTop(shadow, 220);
            canvas.Children.Add(shadow);

            _duckImage = new Image();
            _duckImage.Width = 104;
            _duckImage.Height = 104;
            _duckImage.Stretch = Stretch.Uniform;
            _duckImage.Source = _runningFrames.Length > 0 ? _runningFrames[0] : null;
            RenderOptions.SetBitmapScalingMode(_duckImage, BitmapScalingMode.NearestNeighbor);
            Canvas.SetLeft(_duckImage, 68);
            Canvas.SetTop(_duckImage, 132);
            canvas.Children.Add(_duckImage);

            return canvas;
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            SyncReminderWindowUtils.MakeWindowNoActivate(this);
        }

        private void OnMoveTimerTick(object sender, EventArgs e)
        {
            if (_allowedArea.IsEmpty)
            {
                return;
            }

            Left += _velocityX;
            Top += _velocityY;

            double minLeft = _allowedArea.Left;
            double maxLeft = _allowedArea.Right - Width;
            double minTop = _allowedArea.Top;
            double maxTop = _allowedArea.Bottom - Height;

            if (Left <= minLeft || Left >= maxLeft)
            {
                _velocityX = -_velocityX;
                Left = Clamp(Left, minLeft, maxLeft);
            }

            if (Top <= minTop || Top >= maxTop)
            {
                _velocityY = -_velocityY;
                Top = Clamp(Top, minTop, maxTop);
            }
        }

        private void OnFrameTimerTick(object sender, EventArgs e)
        {
            if (_duckImage == null || _runningFrames.Length == 0)
            {
                return;
            }

            _currentFrameIndex++;
            if (_currentFrameIndex >= _runningFrames.Length)
            {
                _currentFrameIndex = 0;
            }

            _duckImage.Source = _runningFrames[_currentFrameIndex];
        }

        private void OnMessageTimerTick(object sender, EventArgs e)
        {
            SayNextMessage();
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            SayNextMessage();
            RunAwayFromMouse(e.GetPosition(this));
        }

        private void RunAwayFromMouse(Point mousePosition)
        {
            if (_allowedArea.IsEmpty)
            {
                return;
            }

            double directionX = mousePosition.X < Width / 2.0 ? 1.0 : -1.0;
            double directionY = mousePosition.Y < Height / 2.0 ? 1.0 : -1.0;

            _velocityX = directionX * (2.8 + _random.NextDouble() * 1.8);
            _velocityY = directionY * (1.8 + _random.NextDouble() * 1.4);

            Left = Clamp(Left + directionX * 95, _allowedArea.Left, _allowedArea.Right - Width);
            Top = Clamp(Top + directionY * 60, _allowedArea.Top, _allowedArea.Bottom - Height);
        }

        private void PutDuckInsideAllowedArea()
        {
            if (_allowedArea.IsEmpty)
            {
                return;
            }

            double maxLeft = Math.Max(_allowedArea.Left, _allowedArea.Right - Width);
            double maxTop = Math.Max(_allowedArea.Top, _allowedArea.Bottom - Height);

            Left = _allowedArea.Left + Math.Max(0, (maxLeft - _allowedArea.Left) * 0.68);
            Top = _allowedArea.Top + Math.Max(0, (maxTop - _allowedArea.Top) * 0.18);
        }

        private void Say(string text)
        {
            if (_bubbleText == null)
            {
                return;
            }

            _bubbleText.Text = text;
        }

        private void SayNextMessage()
        {
            if (_messages == null || _messages.Length == 0)
            {
                Say("Кря! Давай синхронизируйся!");
                return;
            }

            _currentMessageIndex++;
            if (_currentMessageIndex >= _messages.Length)
            {
                _currentMessageIndex = 0;
            }

            Say(_messages[_currentMessageIndex]);
        }

        private static string[] CreateMessages()
        {
            return new[]
            {
                "Кря! Давай синхронизируйся!",
                "Кря! Синхронься, а то сейчас Ревит тебе закрою!",
                "Кря! Кто не синхронизирует модель - работает по ночам",
                "Кря! Напомнить где кнопка синронизации?",
                "Кря! Либо лови меня, либо синхронизируйся",
                "Кря! Синхронься уже, я устала бегать тут!",
                "Кря! Да не бегай ты за мной, а просто синхронизируйся!",
                "Кря! Вот это у тебя чертеж! Давай отсинхронизируемся, чтобы коллеги увидели!"
            };
        }

        private static BitmapImage[] LoadRunningFrames()
        {
            BitmapImage firstFrame = LoadFrame("Running 001.png");
            BitmapImage secondFrame = LoadFrame("Running 002.png");

            if (firstFrame != null && secondFrame != null)
            {
                return new[] { firstFrame, secondFrame };
            }

            BitmapImage idleFrame = LoadFrame("Idle 001.png");
            if (idleFrame != null)
            {
                return new[] { idleFrame };
            }

            return new BitmapImage[0];
        }

        private static BitmapImage LoadFrame(string fileName)
        {
            string assemblyFolder = System.IO.Path.GetDirectoryName(typeof(DuckReminderWindow).Assembly.Location);
            if (string.IsNullOrWhiteSpace(assemblyFolder))
            {
                return null;
            }

            string filePath = System.IO.Path.Combine(assemblyFolder, "SyncReminder", "Assets", "Duck", fileName);
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
    }
}
