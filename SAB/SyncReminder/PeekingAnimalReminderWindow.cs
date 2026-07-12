using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SAB.SyncReminder
{
    internal class PeekingAnimalReminderWindow : Window
    {
        private const double VisibleSeconds = 3.0;
        private const double SlideSeconds = 0.65;
        private const double BubbleMaxWidth = 360.0;

        private readonly IntPtr _ownerHandle;
        private readonly Random _random;
        private readonly DispatcherTimer _animationTimer;
        private readonly DispatcherTimer _ownerStateTimer;
        private readonly string[] _messages;

        private SyncReminderAnimationMode _mode;
        private Rect _allowedArea;
        private Canvas _canvas;
        private Border _animalHost;
        private Image _animalImage;
        private Border _bubbleBorder;
        private TextBlock _bubbleText;
        private BitmapImage _animalSource;
        private PeekingEdge _edge;
        private DateTime _stateStartedAt;
        private AnimationState _animationState;
        private double _animalWidth;
        private double _animalHeight;
        private double _visibleLeft;
        private double _visibleTop;
        private double _hiddenLeft;
        private double _hiddenTop;
        private int _currentMessageIndex;

        public PeekingAnimalReminderWindow(IntPtr ownerHandle)
        {
            _ownerHandle = ownerHandle;
            _random = new Random();
            _allowedArea = Rect.Empty;
            _mode = SyncReminderAnimationMode.PeekingScottishFold;
            _messages = CreateMessages();
            _currentMessageIndex = -1;

            Width = 900;
            Height = 560;
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

            _animationTimer = new DispatcherTimer();
            _animationTimer.Interval = TimeSpan.FromMilliseconds(16);
            _animationTimer.Tick += OnAnimationTimerTick;

            _ownerStateTimer = new DispatcherTimer();
            _ownerStateTimer.Interval = TimeSpan.FromMilliseconds(700);
            _ownerStateTimer.Tick += OnOwnerStateTimerTick;

            SetAnimationMode(_mode);
        }

        public void SetAnimationMode(SyncReminderAnimationMode mode)
        {
            if (!IsPeekingMode(mode))
            {
                mode = SyncReminderAnimationMode.PeekingScottishFold;
            }

            if (_mode == mode && _animalSource != null)
            {
                return;
            }

            _mode = mode;
            _animalSource = LoadAnimalImage(_mode);
            if (_animalImage != null)
            {
                _animalImage.Source = _animalSource;
            }

            PrepareNextAppearance();
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
            Width = Math.Max(420.0, _allowedArea.Width);
            Height = Math.Max(320.0, _allowedArea.Height);

            if (_canvas != null)
            {
                _canvas.Width = Width;
                _canvas.Height = Height;
            }

            PrepareNextAppearance();
        }

        public void ShowAnimal()
        {
            if (_allowedArea.IsEmpty)
            {
                return;
            }

            if (!IsVisible)
            {
                PrepareNextAppearance();
                Show();
            }

            StartTimers();
        }

        public void HideAnimal()
        {
            StopTimers();

            if (IsVisible)
            {
                Hide();
            }
        }

        public void CloseAnimal()
        {
            StopTimers();
            Close();
        }

        private UIElement CreateContent()
        {
            _canvas = new Canvas();
            _canvas.Width = Width;
            _canvas.Height = Height;

            _animalImage = new Image();
            _animalImage.Stretch = Stretch.Uniform;
            _animalImage.RenderTransformOrigin = new Point(0.5, 0.5);
            RenderOptions.SetBitmapScalingMode(_animalImage, BitmapScalingMode.HighQuality);

            _animalHost = new Border();
            _animalHost.Background = Brushes.Transparent;
            _animalHost.Child = _animalImage;
            _animalHost.Cursor = Cursors.Hand;
            _animalHost.MouseLeftButtonDown += OnAnimalMouseLeftButtonDown;
            _canvas.Children.Add(_animalHost);

            _bubbleText = new TextBlock();
            _bubbleText.FontFamily = new FontFamily("Segoe UI");
            _bubbleText.FontSize = 14;
            _bubbleText.FontWeight = FontWeights.SemiBold;
            _bubbleText.Foreground = CreateBrush("#1F2937");
            _bubbleText.TextWrapping = TextWrapping.Wrap;
            _bubbleText.LineHeight = 18;
            _bubbleText.Width = 310;

            _bubbleBorder = new Border();
            _bubbleBorder.MaxWidth = BubbleMaxWidth;
            _bubbleBorder.Padding = new Thickness(12, 9, 12, 9);
            _bubbleBorder.CornerRadius = new CornerRadius(10);
            _bubbleBorder.Background = new SolidColorBrush(Color.FromArgb(244, 255, 255, 255));
            _bubbleBorder.BorderBrush = CreateBrush("#B8C2CC");
            _bubbleBorder.BorderThickness = new Thickness(1);
            _bubbleBorder.Child = _bubbleText;
            _canvas.Children.Add(_bubbleBorder);

            return _canvas;
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            SyncReminderWindowUtils.MakeWindowNoActivate(this);
        }

        private void StartTimers()
        {
            if (!_animationTimer.IsEnabled)
            {
                _animationTimer.Start();
            }

            if (!_ownerStateTimer.IsEnabled)
            {
                _ownerStateTimer.Start();
            }
        }

        private void StopTimers()
        {
            _animationTimer.Stop();
            _ownerStateTimer.Stop();
        }

        private void OnAnimationTimerTick(object sender, EventArgs e)
        {
            if (_animalHost == null)
            {
                return;
            }

            double elapsedSeconds = (DateTime.Now - _stateStartedAt).TotalSeconds;

            if (_animationState == AnimationState.SlidingIn)
            {
                double progress = Clamp(elapsedSeconds / SlideSeconds, 0.0, 1.0);
                ApplyAnimalPosition(progress);

                if (progress >= 1.0)
                {
                    _animationState = AnimationState.Visible;
                    _stateStartedAt = DateTime.Now;
                    ApplyAnimalPosition(1.0);
                }

                return;
            }

            if (_animationState == AnimationState.Visible)
            {
                ApplyAnimalPosition(1.0);
                if (elapsedSeconds >= VisibleSeconds)
                {
                    _animationState = AnimationState.SlidingOut;
                    _stateStartedAt = DateTime.Now;
                }

                return;
            }

            if (_animationState == AnimationState.SlidingOut)
            {
                double progress = Clamp(elapsedSeconds / SlideSeconds, 0.0, 1.0);
                ApplyAnimalPosition(1.0 - progress);

                if (progress >= 1.0)
                {
                    PrepareNextAppearance();
                }
            }
        }

        private void OnOwnerStateTimerTick(object sender, EventArgs e)
        {
            if (!SyncReminderWindowUtils.CanShowOverlayForRevit(_ownerHandle))
            {
                HideAnimal();
            }
        }

        private void OnAnimalMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            PrepareNextAppearance();
        }

        private void PrepareNextAppearance()
        {
            if (_canvas == null || Width <= 0.0 || Height <= 0.0)
            {
                return;
            }

            CalculateAnimalSize();
            _edge = GetRandomEdge();
            CalculatePositionsForEdge();
            SayNextMessage();
            ApplyRotationForEdge();

            _animationState = AnimationState.SlidingIn;
            _stateStartedAt = DateTime.Now;
            ApplyAnimalPosition(0.0);
        }

        private void CalculateAnimalSize()
        {
            double targetWidth = Math.Max(220.0, Width / 4.0);
            double aspectRatio = 0.55;

            if (_animalSource != null && _animalSource.PixelWidth > 0)
            {
                aspectRatio = (double)_animalSource.PixelHeight / _animalSource.PixelWidth;
            }

            _animalWidth = targetWidth;
            _animalHeight = Math.Max(120.0, targetWidth * aspectRatio);

            _animalHost.Width = _animalWidth;
            _animalHost.Height = _animalHeight;
            _animalImage.Width = _animalWidth;
            _animalImage.Height = _animalHeight;
        }

        private PeekingEdge GetRandomEdge()
        {
            int value = _random.Next(0, 4);
            if (value == 0)
            {
                return PeekingEdge.Bottom;
            }

            if (value == 1)
            {
                return PeekingEdge.Left;
            }

            if (value == 2)
            {
                return PeekingEdge.Right;
            }

            return PeekingEdge.Top;
        }

        private void CalculatePositionsForEdge()
        {
            double maxHorizontal = Math.Max(8.0, Width - _animalWidth - 8.0);
            double maxVertical = Math.Max(8.0, Height - _animalHeight - 8.0);

            if (_edge == PeekingEdge.Bottom)
            {
                _visibleLeft = 8.0 + _random.NextDouble() * Math.Max(1.0, maxHorizontal - 8.0);
                _visibleTop = Height - _animalHeight + 8.0;
                _hiddenLeft = _visibleLeft;
                _hiddenTop = Height + 18.0;
                return;
            }

            if (_edge == PeekingEdge.Top)
            {
                _visibleLeft = 8.0 + _random.NextDouble() * Math.Max(1.0, maxHorizontal - 8.0);
                _visibleTop = -_animalHeight + Math.Min(118.0, _animalHeight * 0.72);
                _hiddenLeft = _visibleLeft;
                _hiddenTop = -_animalHeight - 18.0;
                return;
            }

            if (_edge == PeekingEdge.Left)
            {
                _visibleLeft = -_animalWidth + Math.Min(128.0, _animalWidth * 0.70);
                _visibleTop = 20.0 + _random.NextDouble() * Math.Max(1.0, maxVertical - 20.0);
                _hiddenLeft = -_animalWidth - 18.0;
                _hiddenTop = _visibleTop;
                return;
            }

            _visibleLeft = Width - Math.Min(128.0, _animalWidth * 0.70);
            _visibleTop = 20.0 + _random.NextDouble() * Math.Max(1.0, maxVertical - 20.0);
            _hiddenLeft = Width + 18.0;
            _hiddenTop = _visibleTop;
        }

        private void ApplyRotationForEdge()
        {
            double angle = 0.0;
            if (_edge == PeekingEdge.Top)
            {
                angle = 180.0;
            }
            else if (_edge == PeekingEdge.Left)
            {
                angle = 90.0;
            }
            else if (_edge == PeekingEdge.Right)
            {
                angle = -90.0;
            }

            _animalImage.RenderTransform = new RotateTransform(angle);
        }

        private void ApplyAnimalPosition(double visibleProgress)
        {
            double progress = EaseOutCubic(visibleProgress);
            double left = _hiddenLeft + (_visibleLeft - _hiddenLeft) * progress;
            double top = _hiddenTop + (_visibleTop - _hiddenTop) * progress;

            Canvas.SetLeft(_animalHost, left);
            Canvas.SetTop(_animalHost, top);
            _animalHost.Opacity = Clamp(visibleProgress, 0.0, 1.0);
            _bubbleBorder.Opacity = Clamp(visibleProgress, 0.0, 1.0);
            UpdateBubblePosition(left, top);
        }

        private void UpdateBubblePosition(double animalLeft, double animalTop)
        {
            if (_bubbleBorder == null || _bubbleText == null)
            {
                return;
            }

            _bubbleText.Measure(new Size(_bubbleText.Width, double.PositiveInfinity));
            double bubbleWidth = Math.Min(BubbleMaxWidth, Math.Max(250.0, _bubbleText.DesiredSize.Width + 30.0));
            double bubbleHeight = Math.Max(54.0, _bubbleText.DesiredSize.Height + 22.0);
            _bubbleBorder.Width = bubbleWidth;
            _bubbleBorder.Height = bubbleHeight;

            double left = animalLeft + _animalWidth * 0.5 - bubbleWidth * 0.5;
            double top = animalTop - bubbleHeight - 10.0;

            if (_edge == PeekingEdge.Top)
            {
                top = animalTop + _animalHeight + 10.0;
            }
            else if (_edge == PeekingEdge.Left)
            {
                left = animalLeft + _animalWidth + 12.0;
                top = animalTop + _animalHeight * 0.36;
            }
            else if (_edge == PeekingEdge.Right)
            {
                left = animalLeft - bubbleWidth - 12.0;
                top = animalTop + _animalHeight * 0.36;
            }

            left = Clamp(left, 8.0, Math.Max(8.0, Width - bubbleWidth - 8.0));
            top = Clamp(top, 8.0, Math.Max(8.0, Height - bubbleHeight - 8.0));

            Canvas.SetLeft(_bubbleBorder, left);
            Canvas.SetTop(_bubbleBorder, top);
        }

        private void SayNextMessage()
        {
            _currentMessageIndex++;
            if (_currentMessageIndex >= _messages.Length)
            {
                _currentMessageIndex = 0;
            }

            _bubbleText.Text = _messages[_currentMessageIndex];
        }

        private static BitmapImage LoadAnimalImage(SyncReminderAnimationMode mode)
        {
            string fileName = mode == SyncReminderAnimationMode.PeekingBear
                ? "BearPeeking.png"
                : "ScottishFoldPeeking.png";

            string assemblyFolder = Path.GetDirectoryName(typeof(PeekingAnimalReminderWindow).Assembly.Location);
            if (string.IsNullOrWhiteSpace(assemblyFolder))
            {
                return null;
            }

            string filePath = Path.Combine(assemblyFolder, "SyncReminder", "Assets", "PeekingAnimal", fileName);
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

        private static bool IsPeekingMode(SyncReminderAnimationMode mode)
        {
            return mode == SyncReminderAnimationMode.PeekingScottishFold
                   || mode == SyncReminderAnimationMode.PeekingBear;
        }

        private static string[] CreateMessages()
        {
            return new[]
            {
                "Пссс, синхронизироваться не хочешь?",
                "Какой красивый чертеж у тебя! Давай коллегам покажем!",
                "Такую красоту жалко потерять, если Revit вылетит",
                "Напомнить где кнопка синхронизации?",
                "Думаешь я так просто перестану выглядывать?",
                "Ку-ку! Не забывай синхронизироваться"
            };
        }

        private static double EaseOutCubic(double value)
        {
            double normalizedValue = Clamp(value, 0.0, 1.0);
            double invertedValue = 1.0 - normalizedValue;
            return 1.0 - invertedValue * invertedValue * invertedValue;
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

        private static SolidColorBrush CreateBrush(string color)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }

        private enum PeekingEdge
        {
            Bottom,
            Top,
            Left,
            Right
        }

        private enum AnimationState
        {
            SlidingIn,
            Visible,
            SlidingOut
        }
    }
}
