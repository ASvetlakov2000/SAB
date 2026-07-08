using System;
using System.Collections.Generic;
using System.Globalization;
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
        private readonly DispatcherTimer _poopTimer;
        private readonly DispatcherTimer _ownerStateTimer;
        private readonly Random _random;
        private readonly BitmapImage[] _runningFrames;
        private readonly string[] _duckOnlyMessages;
        private readonly string[] _duckWithPoopMessages;
        private readonly List<PoopDropWindow> _poopDropWindows;
        private readonly IntPtr _ownerHandle;
        private Rect _allowedArea;
        private double _velocityX;
        private double _velocityY;
        private int _currentFrameIndex;
        private int _currentMessageIndex;
        private SyncReminderAnimationMode _animationMode;
        private Canvas _canvas;
        private Border _bubbleBorder;
        private Ellipse[] _thoughtDots;
        private Ellipse _shadow;
        private Image _duckImage;
        private TextBlock _bubbleText;

        public DuckReminderWindow(IntPtr ownerHandle)
        {
            _ownerHandle = ownerHandle;
            Width = 430;
            Height = 372;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = false;
            AllowsTransparency = true;
            Background = Brushes.Transparent;

            _random = new Random();
            // Block responsible for the default duck speed. Increase these values if the reminder should be more aggressive.
            _velocityX = 2.55;
            _velocityY = 1.5;
            _allowedArea = Rect.Empty;
            _runningFrames = LoadRunningFrames();
            _duckOnlyMessages = CreateDuckOnlyMessages();
            _duckWithPoopMessages = CreateDuckWithPoopMessages();
            _poopDropWindows = new List<PoopDropWindow>();
            _currentMessageIndex = -1;
            _animationMode = SyncReminderAnimationMode.DuckOnly;

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

            _poopTimer = new DispatcherTimer();
            _poopTimer.Interval = TimeSpan.FromSeconds(3);
            _poopTimer.Tick += OnPoopTimerTick;

            _ownerStateTimer = new DispatcherTimer();
            _ownerStateTimer.Interval = TimeSpan.FromMilliseconds(700);
            _ownerStateTimer.Tick += OnOwnerStateTimerTick;
        }

        public void SetAllowedArea(Rect allowedArea)
        {
            _allowedArea = allowedArea;

            if (!_allowedArea.IsEmpty && IsVisible)
            {
                ClampDuckToAllowedArea();
            }
        }

        public void ShowDuck()
        {
            bool wasVisible = IsVisible;

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

            UpdatePoopTimerState();

            if (!_ownerStateTimer.IsEnabled)
            {
                _ownerStateTimer.Start();
            }

            if (!wasVisible)
            {
                SayNextMessage();
            }
        }

        public void SetAnimationMode(SyncReminderAnimationMode animationMode)
        {
            bool modeChanged = _animationMode != animationMode;
            _animationMode = animationMode;
            UpdatePoopTimerState();

            if (_animationMode != SyncReminderAnimationMode.DuckWithPoop)
            {
                ClosePoopDrops();
            }

            if (modeChanged)
            {
                _currentMessageIndex = -1;

                if (IsVisible)
                {
                    SayNextMessage();
                }
            }
        }

        public void HideDuck()
        {
            _moveTimer.Stop();
            _frameTimer.Stop();
            _messageTimer.Stop();
            _poopTimer.Stop();
            _ownerStateTimer.Stop();
            ClosePoopDrops();

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
            _poopTimer.Stop();
            _ownerStateTimer.Stop();
            ClosePoopDrops();
            Close();
        }

        private UIElement CreateDuckContent()
        {
            _canvas = new Canvas();
            _canvas.Width = Width;
            _canvas.Height = Height;

            _bubbleBorder = new Border();
            _bubbleBorder.MinWidth = 220;
            _bubbleBorder.MaxWidth = 394;
            _bubbleBorder.MinHeight = 40;
            _bubbleBorder.MaxHeight = 122;
            _bubbleBorder.CornerRadius = new CornerRadius(12);
            _bubbleBorder.Padding = new Thickness(12, 7, 12, 7);
            _bubbleBorder.Background = new SolidColorBrush(Color.FromArgb(238, 255, 255, 255));
            _bubbleBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(200, 120, 148, 165));
            _bubbleBorder.BorderThickness = new Thickness(1);
            _bubbleBorder.ClipToBounds = true;

            _bubbleText = new TextBlock();
            _bubbleText.Text = "Кря-Кря!\nДавай синхронизируйся!";
            _bubbleText.FontFamily = new FontFamily("Segoe UI");
            _bubbleText.FontSize = 14;
            _bubbleText.FontWeight = FontWeights.SemiBold;
            _bubbleText.Foreground = new SolidColorBrush(Color.FromRgb(33, 43, 52));
            _bubbleText.TextWrapping = TextWrapping.Wrap;
            _bubbleText.TextTrimming = TextTrimming.CharacterEllipsis;
            _bubbleText.LineHeight = 18;
            _bubbleText.MaxHeight = 108;
            _bubbleText.VerticalAlignment = VerticalAlignment.Center;
            _bubbleBorder.Child = _bubbleText;

            Canvas.SetLeft(_bubbleBorder, 12);
            Canvas.SetTop(_bubbleBorder, 4);
            _canvas.Children.Add(_bubbleBorder);

            _thoughtDots = CreateThoughtDots();
            for (int i = 0; i < _thoughtDots.Length; i++)
            {
                _canvas.Children.Add(_thoughtDots[i]);
            }

            _shadow = new Ellipse();
            _shadow.Width = 208;
            _shadow.Height = 28;
            _shadow.Fill = new SolidColorBrush(Color.FromArgb(70, 50, 66, 72));
            Canvas.SetLeft(_shadow, 68);
            Canvas.SetTop(_shadow, 334);
            _canvas.Children.Add(_shadow);

            _duckImage = new Image();
            _duckImage.Width = 208;
            _duckImage.Height = 208;
            _duckImage.Stretch = Stretch.Uniform;
            _duckImage.Source = _runningFrames.Length > 0 ? _runningFrames[0] : null;
            RenderOptions.SetBitmapScalingMode(_duckImage, BitmapScalingMode.NearestNeighbor);
            Canvas.SetLeft(_duckImage, 68);
            Canvas.SetTop(_duckImage, 132);
            _canvas.Children.Add(_duckImage);

            UpdateBubbleLayout();

            return _canvas;
        }

        private Ellipse[] CreateThoughtDots()
        {
            Ellipse[] dots = new Ellipse[3];
            double[] sizes = new[] { 5.0, 7.0, 10.0 };

            for (int i = 0; i < dots.Length; i++)
            {
                Ellipse dot = new Ellipse();
                dot.Width = sizes[i];
                dot.Height = sizes[i];
                dot.Fill = _bubbleBorder.Background;
                dot.Stroke = _bubbleBorder.BorderBrush;
                dot.StrokeThickness = 1;
                dots[i] = dot;
            }

            return dots;
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
            double maxLeft = Math.Max(_allowedArea.Left, _allowedArea.Right - Width);
            double minTop = _allowedArea.Top;
            double maxTop = Math.Max(_allowedArea.Top, _allowedArea.Bottom - Height);

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

        private void OnPoopTimerTick(object sender, EventArgs e)
        {
            if (_animationMode != SyncReminderAnimationMode.DuckWithPoop || !IsVisible || _duckImage == null)
            {
                return;
            }

            Point dropPoint = GetPoopDropScreenPoint();
            PoopDropWindow poopDropWindow = new PoopDropWindow(_ownerHandle, dropPoint.X, dropPoint.Y);
            poopDropWindow.Closed += OnPoopDropWindowClosed;
            _poopDropWindows.Add(poopDropWindow);
            poopDropWindow.ShowDrop();
        }

        private void OnOwnerStateTimerTick(object sender, EventArgs e)
        {
            if (!SyncReminderWindowUtils.CanShowOverlayForRevit(_ownerHandle))
            {
                HideDuck();
            }
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

            _velocityX = directionX * (4.2 + _random.NextDouble() * 2.7);
            _velocityY = directionY * (2.7 + _random.NextDouble() * 2.1);

            double maxLeft = Math.Max(_allowedArea.Left, _allowedArea.Right - Width);
            double maxTop = Math.Max(_allowedArea.Top, _allowedArea.Bottom - Height);

            Left = Clamp(Left + directionX * 95, _allowedArea.Left, maxLeft);
            Top = Clamp(Top + directionY * 60, _allowedArea.Top, maxTop);
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

        private void ClampDuckToAllowedArea()
        {
            if (_allowedArea.IsEmpty)
            {
                return;
            }

            double maxLeft = Math.Max(_allowedArea.Left, _allowedArea.Right - Width);
            double maxTop = Math.Max(_allowedArea.Top, _allowedArea.Bottom - Height);

            Left = Clamp(Left, _allowedArea.Left, maxLeft);
            Top = Clamp(Top, _allowedArea.Top, maxTop);
        }

        private void Say(string text)
        {
            if (_bubbleText == null)
            {
                return;
            }

            _bubbleText.Text = text;
            UpdateBubbleLayout();
        }

        private void SayNextMessage()
        {
            string[] messages = GetMessagesForCurrentMode();
            if (messages == null || messages.Length == 0)
            {
                Say("Кря-Кря!\nДавай синхронизируйся!");
                return;
            }

            _currentMessageIndex++;
            if (_currentMessageIndex >= messages.Length)
            {
                _currentMessageIndex = 0;
            }

            Say(messages[_currentMessageIndex]);
        }

        private string[] GetMessagesForCurrentMode()
        {
            if (_animationMode == SyncReminderAnimationMode.DuckWithPoop)
            {
                return _duckWithPoopMessages;
            }

            return _duckOnlyMessages;
        }

        private Point GetPoopDropScreenPoint()
        {
            double duckLeft = Canvas.GetLeft(_duckImage);
            double duckTop = Canvas.GetTop(_duckImage);

            if (double.IsNaN(duckLeft))
            {
                duckLeft = 0;
            }

            if (double.IsNaN(duckTop))
            {
                duckTop = 0;
            }

            double dropLocalX = _velocityX >= 0
                ? duckLeft + 46
                : duckLeft + 146;

            double dropLocalY = duckTop + 168;

            return new Point(Left + dropLocalX, Top + dropLocalY);
        }

        private void UpdatePoopTimerState()
        {
            if (_animationMode == SyncReminderAnimationMode.DuckWithPoop && IsVisible)
            {
                if (!_poopTimer.IsEnabled)
                {
                    _poopTimer.Start();
                }

                return;
            }

            if (_poopTimer.IsEnabled)
            {
                _poopTimer.Stop();
            }
        }

        private void ClosePoopDrops()
        {
            for (int i = _poopDropWindows.Count - 1; i >= 0; i--)
            {
                PoopDropWindow poopDropWindow = _poopDropWindows[i];
                if (poopDropWindow != null)
                {
                    poopDropWindow.Closed -= OnPoopDropWindowClosed;
                    poopDropWindow.Close();
                }
            }

            _poopDropWindows.Clear();
        }

        private void OnPoopDropWindowClosed(object sender, EventArgs e)
        {
            PoopDropWindow poopDropWindow = sender as PoopDropWindow;
            if (poopDropWindow != null)
            {
                poopDropWindow.Closed -= OnPoopDropWindowClosed;
                _poopDropWindows.Remove(poopDropWindow);
            }
        }

        private static string[] CreateDuckOnlyMessages()
        {
            return new[]
            {
                "Кря-Кря!\nДавай синхронизируйся!",
                "Кря-Кря!\nСинхронься, а то сейчас Ревит тебе закрою!",
                "Кря-Кря!\nКто не синхронизирует модель - работает по ночам",
                "Кря-Кря!\nНапомнить где кнопка синронизации?",
                "Кря-Кря!\nЛибо лови меня, либо синхронизируйся",
                "Кря-Кря!\nСинхронься уже, я устала бегать тут!",
                "Кря-Кря!\nДа не бегай ты за мной, а просто синхронизируйся!",
                "Кря-Кря!\nВот это у тебя чертеж!\nДавай отсинхронизируемся, чтобы коллеги увидели!",
                "Кря-Кря!\nЕсли я тебя уже достала, то просто синхронизируйся!",
                "Кря-Кря, Кхе-Кхе, Чык-Чырык, а вот и я",
                "Кря-Кря!\nДавай работай уже, хватит меня ловить",
                "Кря-Кря!\nЕсли будешь только ловить меня, а не синхронизироваться и работать - то будешь голубей на ужин ловить"
            };
        }

        private static string[] CreateDuckWithPoopMessages()
        {
            return new[]
            {
                "Кря-Кря!\nЯ тут наследила... почистить можно, нажав синхронизацию",
                "Кря-Кря!\nЯ перестану, если синхронизируешься",
                "Кря-Кря!\nЧто-то у тебя на экране такое, как это убрать?\nМожет синхронизироваться?",
                "Кря-Кря!\nПрощу прощения, сегодня зерна с глютеном, а у меня непереносимость...\nОчень поможешь, если отсинхронишься",
                "Кря-Кря!\nПротирать бессмысленно, я приберу за собой как только отсинхронишься"
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

        private void UpdateBubbleLayout()
        {
            if (_bubbleBorder == null || _bubbleText == null || _thoughtDots == null || _duckImage == null || _shadow == null)
            {
                return;
            }

            // Block responsible for linking the bubble, thought dots and visible duck sprite into one compact group.
            const double bubbleLeft = 12.0;
            const double bubbleTop = 4.0;
            const double minimumBubbleWidth = 190.0;
            const double maximumBubbleWidth = 394.0;
            const double minimumBubbleHeight = 40.0;
            const double maximumBubbleHeight = 122.0;
            const double duckLeft = 68.0;
            const double visibleDuckTopOffset = 98.0;
            const double visibleDuckTopGap = 22.0;

            Thickness padding = _bubbleBorder.Padding;
            double maximumTextWidth = maximumBubbleWidth - padding.Left - padding.Right;
            double measuredTextWidth = MeasureWidestTextLine(_bubbleText.Text, maximumTextWidth);
            double bubbleWidth = Math.Ceiling(measuredTextWidth + padding.Left + padding.Right);
            bubbleWidth = Clamp(bubbleWidth, minimumBubbleWidth, maximumBubbleWidth);

            double availableTextWidth = Math.Max(1.0, bubbleWidth - padding.Left - padding.Right);
            _bubbleText.Width = availableTextWidth;
            _bubbleText.Height = double.NaN;
            _bubbleText.Measure(new Size(availableTextWidth, double.PositiveInfinity));

            double textHeight = Math.Min(_bubbleText.DesiredSize.Height, _bubbleText.MaxHeight);
            double bubbleHeight = Math.Ceiling(textHeight + padding.Top + padding.Bottom);
            bubbleHeight = Clamp(bubbleHeight, minimumBubbleHeight, maximumBubbleHeight);

            _bubbleBorder.Width = bubbleWidth;
            _bubbleBorder.Height = bubbleHeight;
            _bubbleText.Height = Math.Max(0, bubbleHeight - padding.Top - padding.Bottom);

            Canvas.SetLeft(_bubbleBorder, bubbleLeft);
            Canvas.SetTop(_bubbleBorder, bubbleTop);

            double bubbleBottom = bubbleTop + bubbleHeight;
            double visibleDuckTop = bubbleBottom + visibleDuckTopGap;
            double duckTop = visibleDuckTop - visibleDuckTopOffset;
            double shadowTop = duckTop + 196.0;

            Canvas.SetLeft(_duckImage, duckLeft);
            Canvas.SetTop(_duckImage, duckTop);
            Canvas.SetLeft(_shadow, duckLeft);
            Canvas.SetTop(_shadow, shadowTop);

            UpdateThoughtDots(bubbleLeft, bubbleWidth, bubbleBottom, duckLeft, visibleDuckTop);

            Width = Math.Ceiling(Math.Max(bubbleLeft + bubbleWidth + 12.0, duckLeft + _duckImage.Width + 12.0));
            Height = Math.Ceiling(shadowTop + _shadow.Height + 8.0);
            if (_canvas != null)
            {
                _canvas.Width = Width;
                _canvas.Height = Height;
            }

            if (!_allowedArea.IsEmpty)
            {
                ClampDuckToAllowedArea();
            }
        }

        private void UpdateThoughtDots(double bubbleLeft, double bubbleWidth, double bubbleBottom, double duckLeft, double visibleDuckTop)
        {
            if (_thoughtDots == null || _thoughtDots.Length == 0)
            {
                return;
            }

            double startX = bubbleLeft + Clamp(bubbleWidth * 0.28, 42.0, Math.Max(42.0, bubbleWidth - 34.0));
            double startY = bubbleBottom + 3.0;
            double endX = duckLeft + 88.0;
            double endY = visibleDuckTop + 2.0;

            for (int i = 0; i < _thoughtDots.Length; i++)
            {
                Ellipse dot = _thoughtDots[i];
                if (dot == null)
                {
                    continue;
                }

                double t = (i + 1.0) / (_thoughtDots.Length + 1.0);
                double left = startX + (endX - startX) * t - dot.Width / 2.0;
                double top = startY + (endY - startY) * t - dot.Height / 2.0;

                Canvas.SetLeft(dot, left);
                Canvas.SetTop(dot, top);
            }
        }

        private double MeasureWidestTextLine(string text, double maximumTextWidth)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 1.0;
            }

            Typeface typeface = new Typeface(
                _bubbleText.FontFamily,
                _bubbleText.FontStyle,
                _bubbleText.FontWeight,
                _bubbleText.FontStretch);
            double pixelsPerDip = VisualTreeHelper.GetDpi(_bubbleText).PixelsPerDip;

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            double widestLine = 1.0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                FormattedText formattedText = new FormattedText(
                    line,
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    _bubbleText.FontSize,
                    _bubbleText.Foreground,
                    pixelsPerDip);

                widestLine = Math.Max(widestLine, formattedText.WidthIncludingTrailingWhitespace);
            }

            return Clamp(Math.Ceiling(widestLine + 4.0), 1.0, maximumTextWidth);
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

        private sealed class PoopDropWindow : Window
        {
            public PoopDropWindow(IntPtr ownerHandle, double left, double top)
            {
                Width = 46;
                Height = 38;
                Left = left;
                Top = top;
                WindowStyle = WindowStyle.None;
                ResizeMode = ResizeMode.NoResize;
                ShowInTaskbar = false;
                ShowActivated = false;
                Topmost = false;
                AllowsTransparency = true;
                Background = Brushes.Transparent;
                Content = CreatePoopContent();

                WindowInteropHelper helper = new WindowInteropHelper(this);
                helper.Owner = ownerHandle;

                SourceInitialized += OnSourceInitialized;
                Closed += OnClosed;
            }

            public void ShowDrop()
            {
                Show();
            }

            private UIElement CreatePoopContent()
            {
                Canvas canvas = new Canvas();
                canvas.Width = Width;
                canvas.Height = Height;

                Ellipse shadow = new Ellipse();
                shadow.Width = 34;
                shadow.Height = 8;
                shadow.Fill = new SolidColorBrush(Color.FromArgb(70, 60, 38, 24));
                Canvas.SetLeft(shadow, 6);
                Canvas.SetTop(shadow, 28);
                canvas.Children.Add(shadow);

                SolidColorBrush mainBrush = new SolidColorBrush(Color.FromRgb(116, 65, 32));
                SolidColorBrush middleBrush = new SolidColorBrush(Color.FromRgb(139, 78, 38));
                SolidColorBrush highlightBrush = new SolidColorBrush(Color.FromRgb(168, 103, 55));
                SolidColorBrush strokeBrush = new SolidColorBrush(Color.FromRgb(82, 45, 22));

                AddPoopEllipse(canvas, 6, 20, 34, 12, mainBrush, strokeBrush);
                AddPoopEllipse(canvas, 10, 13, 27, 12, middleBrush, strokeBrush);
                AddPoopEllipse(canvas, 15, 7, 18, 10, middleBrush, strokeBrush);
                AddPoopEllipse(canvas, 21, 3, 8, 8, highlightBrush, strokeBrush);

                return canvas;
            }

            private static void AddPoopEllipse(
                Canvas canvas,
                double left,
                double top,
                double width,
                double height,
                Brush fill,
                Brush stroke)
            {
                Ellipse ellipse = new Ellipse();
                ellipse.Width = width;
                ellipse.Height = height;
                ellipse.Fill = fill;
                ellipse.Stroke = stroke;
                ellipse.StrokeThickness = 1;
                Canvas.SetLeft(ellipse, left);
                Canvas.SetTop(ellipse, top);
                canvas.Children.Add(ellipse);
            }

            private void OnSourceInitialized(object sender, EventArgs e)
            {
                SyncReminderWindowUtils.MakeWindowNoActivate(this);
            }

            private void OnClosed(object sender, EventArgs e)
            {
                SourceInitialized -= OnSourceInitialized;
                Closed -= OnClosed;
            }
        }
    }
}
