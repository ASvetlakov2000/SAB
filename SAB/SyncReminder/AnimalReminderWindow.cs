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
    internal class AnimalReminderWindow : Window
    {
        // Block responsible for animal size, speed and maximum clutter on the Revit workspace.
        private const double AnimalWidth = 192.0;
        private const double AnimalHeight = 192.0;
        private const double AnimalSpeed = 7.2;
        private const double BaseAnimalWidth = 96.0;
        private const double BaseAnimalHeight = 96.0;
        private const double FoxSyncButtonSize = 78.0;
        private const double PigPuddleWidth = 210.0;
        private const double PigPuddleHeight = 104.0;
        private const double MovingSheepWidth = 124.0;
        private const double MovingSheepHeight = 104.0;
        private const double DefaultScenarioIntervalSeconds = 2.2;
        private const double SheepSpawnIntervalSeconds = 5.0;
        private const int MaximumSpawnedElements = 220;
        private const int MaximumSheepCount = 10;

        private readonly IntPtr _ownerHandle;
        private readonly Random _random;
        private readonly DispatcherTimer _moveTimer;
        private readonly DispatcherTimer _frameTimer;
        private readonly DispatcherTimer _scenarioTimer;
        private readonly DispatcherTimer _messageTimer;
        private readonly DispatcherTimer _ownerStateTimer;
        private readonly DispatcherTimer _foxCatchTimer;
        private readonly List<SpawnedElement> _spawnedElements;
        private readonly List<MovingSheepSprite> _movingSheepSprites;

        private SyncReminderAnimationMode _mode;
        private Rect _allowedArea;
        private Canvas _canvas;
        private Ellipse _shadow;
        private Border _bubbleBorder;
        private TextBlock _bubbleText;
        private FrameworkElement _animalVisual;
        private Image _animalImage;
        private BitmapImage[] _animalFrames;
        private SpawnedElement _primaryTarget;
        private double _animalLeft;
        private double _animalTop;
        private double _velocityX;
        private double _velocityY;
        private int _currentFrameIndex;
        private int _currentMessageIndex;
        private int _sheepCount;
        private int _foxCatchStep;
        private int _pigJumpCooldownTicks;
        private int _currentPigMessageIndex;
        private double _foxTargetRotation;
        private bool _isFoxCatchAnimationPlaying;

        public AnimalReminderWindow(IntPtr ownerHandle)
        {
            _ownerHandle = ownerHandle;
            _random = new Random();
            _allowedArea = Rect.Empty;
            _spawnedElements = new List<SpawnedElement>();
            _movingSheepSprites = new List<MovingSheepSprite>();
            _animalFrames = new BitmapImage[0];
            _velocityX = 3.8;
            _velocityY = 2.4;
            _mode = SyncReminderAnimationMode.FoxWithSyncButton;

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

            _moveTimer = new DispatcherTimer();
            _moveTimer.Interval = TimeSpan.FromMilliseconds(28);
            _moveTimer.Tick += OnMoveTimerTick;

            _frameTimer = new DispatcherTimer();
            _frameTimer.Interval = TimeSpan.FromMilliseconds(125);
            _frameTimer.Tick += OnFrameTimerTick;

            _scenarioTimer = new DispatcherTimer();
            _scenarioTimer.Interval = TimeSpan.FromSeconds(DefaultScenarioIntervalSeconds);
            _scenarioTimer.Tick += OnScenarioTimerTick;

            _messageTimer = new DispatcherTimer();
            _messageTimer.Interval = TimeSpan.FromSeconds(5);
            _messageTimer.Tick += OnMessageTimerTick;

            _ownerStateTimer = new DispatcherTimer();
            _ownerStateTimer.Interval = TimeSpan.FromMilliseconds(700);
            _ownerStateTimer.Tick += OnOwnerStateTimerTick;

            _foxCatchTimer = new DispatcherTimer();
            _foxCatchTimer.Interval = TimeSpan.FromMilliseconds(42);
            _foxCatchTimer.Tick += OnFoxCatchTimerTick;

            SetAnimationMode(_mode);
        }

        public void SetAnimationMode(SyncReminderAnimationMode mode)
        {
            if (!IsAnimalMode(mode))
            {
                mode = SyncReminderAnimationMode.FoxWithSyncButton;
            }

            bool modeChanged = _mode != mode;
            _mode = mode;
            UpdateScenarioTimerInterval();

            if (modeChanged)
            {
                _currentMessageIndex = -1;
                _sheepCount = 0;
                ClearScenarioElements();
                RebuildAnimalVisual();
                SayNextMessage();
                PrepareStartPosition();
            }
            else if (_animalVisual == null)
            {
                RebuildAnimalVisual();
            }
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
            Width = Math.Max(AnimalWidth + 120.0, _allowedArea.Width);
            Height = Math.Max(AnimalHeight + 140.0, _allowedArea.Height);

            if (_canvas != null)
            {
                _canvas.Width = Width;
                _canvas.Height = Height;
            }

            ClampAnimalToWindow();
            ClampSpawnedElementsToWindow();
            UpdateAnimalPosition();
            UpdateBubblePosition();
        }

        public void ShowAnimal()
        {
            if (_allowedArea.IsEmpty)
            {
                return;
            }

            if (!IsVisible)
            {
                PrepareStartPosition();
                Show();
            }

            EnsureScenarioState();
            StartTimers();
        }

        public void HideAnimal()
        {
            StopTimers();
            ClearScenarioElements();

            if (IsVisible)
            {
                Hide();
            }
        }

        public void CloseAnimal()
        {
            StopTimers();
            ClearScenarioElements();
            Close();
        }

        public void PlayFoxCatchAnimationAndHide()
        {
            if (_mode != SyncReminderAnimationMode.FoxWithSyncButton || !IsVisible)
            {
                HideAnimal();
                return;
            }

            EnsureFoxSyncButton();
            if (_primaryTarget == null)
            {
                HideAnimal();
                return;
            }

            _isFoxCatchAnimationPlaying = true;
            _foxCatchStep = 0;
            _moveTimer.Stop();
            _scenarioTimer.Stop();
            _messageTimer.Stop();
            Say("Я ее поймала! Спасибо!");

            if (!_frameTimer.IsEnabled)
            {
                _frameTimer.Start();
            }

            if (!_ownerStateTimer.IsEnabled)
            {
                _ownerStateTimer.Start();
            }

            _foxCatchTimer.Start();
        }

        private UIElement CreateContent()
        {
            _canvas = new Canvas();
            _canvas.Width = Width;
            _canvas.Height = Height;

            _shadow = new Ellipse();
            _shadow.Width = 78;
            _shadow.Height = 16;
            _shadow.Fill = new SolidColorBrush(Color.FromArgb(70, 48, 58, 66));
            _canvas.Children.Add(_shadow);

            _bubbleText = new TextBlock();
            _bubbleText.FontFamily = new FontFamily("Segoe UI");
            _bubbleText.FontSize = 13;
            _bubbleText.FontWeight = FontWeights.SemiBold;
            _bubbleText.Foreground = new SolidColorBrush(Color.FromRgb(33, 43, 52));
            _bubbleText.TextWrapping = TextWrapping.Wrap;
            _bubbleText.LineHeight = 17;

            _bubbleBorder = new Border();
            _bubbleBorder.MinWidth = 210;
            _bubbleBorder.MaxWidth = 330;
            _bubbleBorder.Padding = new Thickness(11, 7, 11, 7);
            _bubbleBorder.CornerRadius = new CornerRadius(10);
            _bubbleBorder.Background = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255));
            _bubbleBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(210, 120, 148, 165));
            _bubbleBorder.BorderThickness = new Thickness(1);
            _bubbleBorder.Child = _bubbleText;
            _canvas.Children.Add(_bubbleBorder);

            return _canvas;
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            SyncReminderWindowUtils.MakeWindowNoActivateAndClickThrough(this);
        }

        private void StartTimers()
        {
            UpdateScenarioTimerInterval();

            if (!_moveTimer.IsEnabled)
            {
                _moveTimer.Start();
            }

            if (!_frameTimer.IsEnabled)
            {
                _frameTimer.Start();
            }

            if (!_scenarioTimer.IsEnabled)
            {
                _scenarioTimer.Start();
            }

            if (!_messageTimer.IsEnabled)
            {
                _messageTimer.Start();
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
            _scenarioTimer.Stop();
            _messageTimer.Stop();
            _ownerStateTimer.Stop();
            _foxCatchTimer.Stop();
            _isFoxCatchAnimationPlaying = false;
        }

        private void OnMoveTimerTick(object sender, EventArgs e)
        {
            if (_canvas == null)
            {
                return;
            }

            if (_isFoxCatchAnimationPlaying)
            {
                return;
            }

            if (_mode == SyncReminderAnimationMode.FoxWithSyncButton)
            {
                MoveAnimalToPrimaryTarget();
            }
            else if (_mode == SyncReminderAnimationMode.PigMud)
            {
                MovePigToPuddle();
            }
            else
            {
                MoveAnimalByVelocity();
            }

            if (_mode == SyncReminderAnimationMode.SheepCounter)
            {
                MoveSheepFriends();
            }

            UpdateAnimalPosition();
            UpdateBubblePosition();
        }

        private void OnFrameTimerTick(object sender, EventArgs e)
        {
            if (_animalImage == null || _animalFrames == null || _animalFrames.Length == 0)
            {
                return;
            }

            _currentFrameIndex++;
            if (_currentFrameIndex >= _animalFrames.Length)
            {
                _currentFrameIndex = 0;
            }

            _animalImage.Source = _animalFrames[_currentFrameIndex];
        }

        private void OnScenarioTimerTick(object sender, EventArgs e)
        {
            if (_mode == SyncReminderAnimationMode.FoxWithSyncButton)
            {
                EnsureFoxSyncButton();
                return;
            }

            if (_mode == SyncReminderAnimationMode.PigMud)
            {
                EnsurePigPuddle();
                return;
            }

            if (_mode == SyncReminderAnimationMode.SheepCounter)
            {
                AddSheepCounterItem();
            }
        }

        private void OnMessageTimerTick(object sender, EventArgs e)
        {
            if (_mode == SyncReminderAnimationMode.SheepCounter || _mode == SyncReminderAnimationMode.PigMud)
            {
                return;
            }

            SayNextMessage();
        }

        private void OnOwnerStateTimerTick(object sender, EventArgs e)
        {
            if (!SyncReminderWindowUtils.CanShowOverlayForRevit(_ownerHandle))
            {
                HideAnimal();
            }
        }

        private void EnsureScenarioState()
        {
            // Block responsible for the first visible object in modes that need targets or traces.
            if (_mode == SyncReminderAnimationMode.FoxWithSyncButton)
            {
                EnsureFoxSyncButton();
            }
            else if (_mode == SyncReminderAnimationMode.PigMud && _spawnedElements.Count == 0)
            {
                EnsurePigPuddle();
            }
            else if (_mode == SyncReminderAnimationMode.SheepCounter && _sheepCount == 0)
            {
                AddSheepCounterItem();
            }
        }

        private void RebuildAnimalVisual()
        {
            // Block responsible for switching between real PNG sprites and built-in WPF fallback animals.
            if (_canvas == null)
            {
                return;
            }

            if (_animalVisual != null)
            {
                _canvas.Children.Remove(_animalVisual);
                _animalVisual = null;
                _animalImage = null;
            }

            _animalFrames = LoadAnimalFrames(_mode);
            if (_animalFrames.Length > 0)
            {
                _animalImage = new Image();
                _animalImage.Width = AnimalWidth;
                _animalImage.Height = AnimalHeight;
                _animalImage.Stretch = Stretch.Uniform;
                _animalImage.Source = _animalFrames[0];
                _animalImage.RenderTransformOrigin = new Point(0.5, 0.5);
                RenderOptions.SetBitmapScalingMode(_animalImage, BitmapScalingMode.NearestNeighbor);
                _animalVisual = _animalImage;
            }
            else
            {
                _animalVisual = CreateFallbackAnimalVisual(_mode);
            }

            _canvas.Children.Add(_animalVisual);
            UpdateAnimalPosition();
        }

        private FrameworkElement CreateFallbackAnimalVisual(SyncReminderAnimationMode mode)
        {
            Canvas animalCanvas = new Canvas();
            animalCanvas.Width = BaseAnimalWidth;
            animalCanvas.Height = BaseAnimalHeight;

            if (mode == SyncReminderAnimationMode.FoxWithSyncButton)
            {
                BuildFox(animalCanvas);
            }
            else if (mode == SyncReminderAnimationMode.PigMud)
            {
                BuildPig(animalCanvas);
            }
            else
            {
                BuildSheep(animalCanvas, 1.0);
            }

            Viewbox viewbox = new Viewbox();
            viewbox.Width = AnimalWidth;
            viewbox.Height = AnimalHeight;
            viewbox.Stretch = Stretch.Uniform;
            viewbox.Child = animalCanvas;
            return viewbox;
        }

        private void BuildFox(Canvas canvas)
        {
            Brush orange = CreateBrush("#D96A23");
            Brush cream = CreateBrush("#FFF1D5");
            Brush dark = CreateBrush("#2D2D2D");

            AddEllipse(canvas, 26, 34, 46, 34, orange, null);
            AddEllipse(canvas, 48, 22, 32, 28, orange, null);
            AddPolygon(canvas, new Point[] { new Point(52, 24), new Point(58, 8), new Point(64, 25) }, orange, null);
            AddPolygon(canvas, new Point[] { new Point(66, 25), new Point(76, 11), new Point(75, 34) }, orange, null);
            AddEllipse(canvas, 56, 34, 20, 12, cream, null);
            AddEllipse(canvas, 69, 32, 4, 4, dark, null);
            AddPolygon(canvas, new Point[] { new Point(26, 50), new Point(4, 38), new Point(12, 70) }, orange, null);
            AddPolygon(canvas, new Point[] { new Point(8, 43), new Point(2, 39), new Point(6, 53) }, cream, null);
            AddRectangle(canvas, 32, 64, 8, 18, orange, 3);
            AddRectangle(canvas, 58, 62, 8, 18, orange, 3);
        }

        private void BuildPig(Canvas canvas)
        {
            Brush pink = CreateBrush("#F4A7B9");
            Brush darkPink = CreateBrush("#E46F8E");
            Brush dark = CreateBrush("#1F2937");

            AddEllipse(canvas, 24, 38, 48, 34, pink, null);
            AddEllipse(canvas, 54, 28, 32, 28, pink, null);
            AddEllipse(canvas, 74, 40, 16, 12, darkPink, null);
            AddEllipse(canvas, 78, 44, 3, 3, dark, null);
            AddEllipse(canvas, 84, 44, 3, 3, dark, null);
            AddEllipse(canvas, 66, 35, 4, 4, dark, null);
            AddPolygon(canvas, new Point[] { new Point(58, 30), new Point(60, 18), new Point(69, 31) }, darkPink, null);
            AddPolygon(canvas, new Point[] { new Point(73, 30), new Point(82, 19), new Point(81, 35) }, darkPink, null);
            AddRectangle(canvas, 34, 66, 7, 16, darkPink, 3);
            AddRectangle(canvas, 58, 64, 7, 16, darkPink, 3);
        }

        private FrameworkElement CreateSmallSheepVisual()
        {
            Canvas canvas = new Canvas();
            canvas.Width = BaseAnimalWidth;
            canvas.Height = BaseAnimalHeight;
            BuildSheep(canvas, 1.0);

            Viewbox viewbox = new Viewbox();
            viewbox.Width = MovingSheepWidth;
            viewbox.Height = MovingSheepHeight;
            viewbox.Stretch = Stretch.Uniform;
            viewbox.Child = canvas;
            return viewbox;
        }

        private void BuildSheep(Canvas canvas, double scale)
        {
            Brush wool = CreateBrush("#FFFFFF");
            Brush face = CreateBrush("#7A6A5F");
            Brush dark = CreateBrush("#1F2937");

            AddEllipse(canvas, 20 * scale, 34 * scale, 48 * scale, 34 * scale, wool, CreateBrush("#D8DEE8"));
            AddEllipse(canvas, 12 * scale, 38 * scale, 18 * scale, 18 * scale, wool, CreateBrush("#D8DEE8"));
            AddEllipse(canvas, 32 * scale, 28 * scale, 18 * scale, 18 * scale, wool, CreateBrush("#D8DEE8"));
            AddEllipse(canvas, 54 * scale, 34 * scale, 28 * scale, 24 * scale, face, null);
            AddEllipse(canvas, 67 * scale, 41 * scale, 4 * scale, 4 * scale, dark, null);
            AddRectangle(canvas, 32 * scale, 64 * scale, 6 * scale, 16 * scale, face, 2 * scale);
            AddRectangle(canvas, 56 * scale, 64 * scale, 6 * scale, 16 * scale, face, 2 * scale);
        }

        private void MoveAnimalToPrimaryTarget()
        {
            EnsureFoxSyncButton();

            if (_primaryTarget == null)
            {
                MoveAnimalByVelocity();
                return;
            }

            double targetCenterX = _primaryTarget.Left + _primaryTarget.Width / 2.0;
            double targetCenterY = _primaryTarget.Top + _primaryTarget.Height / 2.0;
            double animalCenterX = _animalLeft + AnimalWidth / 2.0;
            double animalCenterY = _animalTop + AnimalHeight / 2.0;
            double directionX = targetCenterX - animalCenterX;
            double directionY = targetCenterY - animalCenterY;
            double distance = Math.Sqrt(directionX * directionX + directionY * directionY);
            RotateFoxSyncButton(7.5);

            if (distance < AnimalWidth * 0.43)
            {
                RollFoxSyncButtonAway();
                SayNextMessage();
                return;
            }

            if (distance < 0.01)
            {
                return;
            }

            _animalLeft += directionX / distance * AnimalSpeed;
            _animalTop += directionY / distance * AnimalSpeed;
            _velocityX = directionX >= 0 ? Math.Abs(_velocityX) : -Math.Abs(_velocityX);
            ClampAnimalToWindow();
        }

        private void MoveAnimalByVelocity()
        {
            _animalLeft += _velocityX;
            _animalTop += _velocityY;

            double minLeft = 8.0;
            double minTop = 8.0;
            double maxLeft = Math.Max(minLeft, Width - AnimalWidth - 12.0);
            double maxTop = Math.Max(minTop, Height - AnimalHeight - 24.0);

            if (_animalLeft <= minLeft || _animalLeft >= maxLeft)
            {
                _velocityX = -_velocityX;
                _animalLeft = Clamp(_animalLeft, minLeft, maxLeft);
            }

            if (_animalTop <= minTop || _animalTop >= maxTop)
            {
                _velocityY = -_velocityY;
                _animalTop = Clamp(_animalTop, minTop, maxTop);
            }
        }

        private void MovePigToPuddle()
        {
            EnsurePigPuddle();

            if (_primaryTarget == null)
            {
                MoveAnimalByVelocity();
                return;
            }

            if (_pigJumpCooldownTicks > 0)
            {
                _pigJumpCooldownTicks--;
                return;
            }

            double targetCenterX = _primaryTarget.Left + _primaryTarget.Width / 2.0;
            double targetCenterY = _primaryTarget.Top + _primaryTarget.Height / 2.0;
            double animalCenterX = _animalLeft + AnimalWidth / 2.0;
            double animalCenterY = _animalTop + AnimalHeight / 2.0;
            double directionX = targetCenterX - animalCenterX;
            double directionY = targetCenterY - animalCenterY;
            double distance = Math.Sqrt(directionX * directionX + directionY * directionY);

            if (distance < AnimalWidth * 0.34)
            {
                SplashPigPuddle();
                MovePuddleToRandomPoint();
                SayNextPigMessage();
                _pigJumpCooldownTicks = 38;
                return;
            }

            if (distance < 0.01)
            {
                return;
            }

            _animalLeft += directionX / distance * (AnimalSpeed + 1.1);
            _animalTop += directionY / distance * (AnimalSpeed + 1.1);
            _velocityX = directionX >= 0 ? Math.Abs(_velocityX) : -Math.Abs(_velocityX);
            ClampAnimalToWindow();
        }

        private void PrepareStartPosition()
        {
            _animalLeft = Math.Max(20.0, Width * 0.18);
            _animalTop = Math.Max(20.0, Height * 0.42);
            if (Math.Abs(_velocityX) < 0.1)
            {
                _velocityX = 3.8;
            }

            if (Math.Abs(_velocityY) < 0.1)
            {
                _velocityY = 2.4;
            }

            ClampAnimalToWindow();
            UpdateAnimalPosition();
            UpdateBubblePosition();
        }

        private void EnsureFoxSyncButton()
        {
            if (_primaryTarget != null)
            {
                return;
            }

            FrameworkElement syncButton = CreateSyncButtonVisual();
            _primaryTarget = AddSpawnedElement(syncButton, GetRandomLeft(FoxSyncButtonSize), GetRandomTop(FoxSyncButtonSize), FoxSyncButtonSize, FoxSyncButtonSize);
        }

        private FrameworkElement CreateSyncButtonVisual()
        {
            Canvas button = new Canvas();
            button.Width = FoxSyncButtonSize;
            button.Height = FoxSyncButtonSize;
            button.RenderTransformOrigin = new Point(0.5, 0.5);

            AddEllipse(button, 2, 2, FoxSyncButtonSize - 4, FoxSyncButtonSize - 4, CreateBrush("#EAF3FF"), CreateBrush("#0F6CBD"));

            System.Windows.Shapes.Path topArrow = new System.Windows.Shapes.Path();
            topArrow.Data = Geometry.Parse("M 22,35 A 18,18 0 0 1 55,26");
            topArrow.Stroke = CreateBrush("#0F6CBD");
            topArrow.StrokeThickness = 5;
            topArrow.StrokeStartLineCap = PenLineCap.Round;
            topArrow.StrokeEndLineCap = PenLineCap.Round;
            button.Children.Add(topArrow);

            System.Windows.Shapes.Path bottomArrow = new System.Windows.Shapes.Path();
            bottomArrow.Data = Geometry.Parse("M 56,43 A 18,18 0 0 1 23,52");
            bottomArrow.Stroke = CreateBrush("#0F6CBD");
            bottomArrow.StrokeThickness = 5;
            bottomArrow.StrokeStartLineCap = PenLineCap.Round;
            bottomArrow.StrokeEndLineCap = PenLineCap.Round;
            button.Children.Add(bottomArrow);

            AddPolygon(button, new[] { new Point(54, 16), new Point(64, 26), new Point(50, 29) }, CreateBrush("#0F6CBD"), null);
            AddPolygon(button, new[] { new Point(24, 62), new Point(14, 52), new Point(28, 49) }, CreateBrush("#0F6CBD"), null);

            TextBlock text = new TextBlock();
            text.Text = "S";
            text.FontFamily = new FontFamily("Segoe UI");
            text.FontSize = 17;
            text.FontWeight = FontWeights.Bold;
            text.Foreground = CreateBrush("#0F6CBD");
            text.Width = FoxSyncButtonSize;
            text.TextAlignment = TextAlignment.Center;
            Canvas.SetTop(text, 28);
            button.Children.Add(text);

            return button;
        }

        private void MovePrimaryTargetToRandomPoint()
        {
            if (_primaryTarget == null)
            {
                return;
            }

            _primaryTarget.Left = GetRandomLeft(_primaryTarget.Width);
            _primaryTarget.Top = GetRandomTop(_primaryTarget.Height);
            Canvas.SetLeft(_primaryTarget.Element, _primaryTarget.Left);
            Canvas.SetTop(_primaryTarget.Element, _primaryTarget.Top);
        }

        private void RollFoxSyncButtonAway()
        {
            if (_primaryTarget == null)
            {
                return;
            }

            double foxCenterX = _animalLeft + AnimalWidth / 2.0;
            double foxCenterY = _animalTop + AnimalHeight / 2.0;
            double buttonCenterX = _primaryTarget.Left + _primaryTarget.Width / 2.0;
            double buttonCenterY = _primaryTarget.Top + _primaryTarget.Height / 2.0;
            double directionX = buttonCenterX - foxCenterX;
            double directionY = buttonCenterY - foxCenterY;
            double distance = Math.Sqrt(directionX * directionX + directionY * directionY);

            if (distance < 0.01)
            {
                directionX = _random.NextDouble() > 0.5 ? 1.0 : -1.0;
                directionY = _random.NextDouble() > 0.5 ? 1.0 : -1.0;
                distance = Math.Sqrt(directionX * directionX + directionY * directionY);
            }

            double escapeDistance = Math.Max(220.0, AnimalWidth * 1.35);
            double nextLeft = _primaryTarget.Left + directionX / distance * escapeDistance;
            double nextTop = _primaryTarget.Top + directionY / distance * escapeDistance;

            nextLeft += (_random.NextDouble() - 0.5) * 90.0;
            nextTop += (_random.NextDouble() - 0.5) * 90.0;

            _primaryTarget.Left = Clamp(nextLeft, 14.0, Math.Max(14.0, Width - _primaryTarget.Width - 14.0));
            _primaryTarget.Top = Clamp(nextTop, 20.0, Math.Max(20.0, Height - _primaryTarget.Height - 20.0));
            Canvas.SetLeft(_primaryTarget.Element, _primaryTarget.Left);
            Canvas.SetTop(_primaryTarget.Element, _primaryTarget.Top);
            RotateFoxSyncButton(95.0);
        }

        private void RotateFoxSyncButton(double angleStep)
        {
            if (_primaryTarget == null || _primaryTarget.Element == null)
            {
                return;
            }

            _foxTargetRotation += angleStep;
            if (_foxTargetRotation >= 360.0)
            {
                _foxTargetRotation -= 360.0;
            }

            _primaryTarget.Element.RenderTransformOrigin = new Point(0.5, 0.5);
            _primaryTarget.Element.RenderTransform = new RotateTransform(_foxTargetRotation);
        }

        private void OnFoxCatchTimerTick(object sender, EventArgs e)
        {
            if (!_isFoxCatchAnimationPlaying || _primaryTarget == null || _primaryTarget.Element == null)
            {
                HideAnimal();
                return;
            }

            _foxCatchStep++;

            double catchLeft = _animalLeft + AnimalWidth * 0.60 - _primaryTarget.Width / 2.0;
            double catchTop = _animalTop + AnimalHeight * 0.42 - _primaryTarget.Height / 2.0;

            _primaryTarget.Left = _primaryTarget.Left + (catchLeft - _primaryTarget.Left) * 0.18;
            _primaryTarget.Top = _primaryTarget.Top + (catchTop - _primaryTarget.Top) * 0.18;
            Canvas.SetLeft(_primaryTarget.Element, _primaryTarget.Left);
            Canvas.SetTop(_primaryTarget.Element, _primaryTarget.Top);
            RotateFoxSyncButton(16.0);

            if (_foxCatchStep > 24)
            {
                _primaryTarget.Element.Opacity = Math.Max(0.25, _primaryTarget.Element.Opacity - 0.035);
            }

            if (_foxCatchStep > 58)
            {
                HideAnimal();
            }
        }

        private void EnsurePigPuddle()
        {
            if (_primaryTarget != null)
            {
                return;
            }

            FrameworkElement puddle = CreatePigPuddleVisual();
            _primaryTarget = AddSpawnedElement(puddle, GetRandomLeft(PigPuddleWidth), GetRandomTop(PigPuddleHeight), PigPuddleWidth, PigPuddleHeight);
            SayNextPigMessage();
        }

        private FrameworkElement CreatePigPuddleVisual()
        {
            Canvas puddle = new Canvas();
            puddle.Width = PigPuddleWidth;
            puddle.Height = PigPuddleHeight;

            Brush dark = CreateBrush("#5A321F");
            Brush middle = CreateBrush("#7A4A2A");
            Brush light = CreateBrush("#9B6338");

            AddEllipse(puddle, 10, 34, 176, 48, middle, null);
            AddEllipse(puddle, 44, 18, 126, 54, light, null);
            AddEllipse(puddle, 122, 42, 68, 32, dark, null);
            AddEllipse(puddle, 28, 50, 70, 30, dark, null);
            AddEllipse(puddle, 72, 31, 40, 16, CreateBrush("#B47A49"), null);

            return puddle;
        }

        private void MovePuddleToRandomPoint()
        {
            if (_primaryTarget == null)
            {
                return;
            }

            _primaryTarget.Left = GetRandomLeft(_primaryTarget.Width);
            _primaryTarget.Top = GetRandomTop(_primaryTarget.Height);
            Canvas.SetLeft(_primaryTarget.Element, _primaryTarget.Left);
            Canvas.SetTop(_primaryTarget.Element, _primaryTarget.Top);
        }

        private void SplashPigPuddle()
        {
            for (int i = 0; i < 26; i++)
            {
                double width = 18.0 + _random.NextDouble() * 42.0;
                double height = 10.0 + _random.NextDouble() * 26.0;
                Canvas splash = new Canvas();
                splash.Width = width;
                splash.Height = height;

                Brush fill = i % 2 == 0 ? CreateBrush("#7A4A2A") : CreateBrush("#9B6338");
                AddEllipse(splash, 0, 0, width, height, fill, null);

                AddSpawnedElement(splash, GetRandomLeft(width), GetRandomTop(height), width, height);
            }
        }

        private void SayNextPigMessage()
        {
            string[] messages =
            {
                "Вот это лужа! Пойду прыгну, пока ты не нажал синхронизацию",
                "Ого, эта лужа еще больше! Только не нажимай синхронизацию, а то прыгать будет некуда"
            };

            Say(messages[_currentPigMessageIndex]);
            _currentPigMessageIndex++;
            if (_currentPigMessageIndex >= messages.Length)
            {
                _currentPigMessageIndex = 0;
            }
        }

        private void AddSheepCounterItem()
        {
            if (_sheepCount >= MaximumSheepCount)
            {
                return;
            }

            _sheepCount++;
            if (_sheepCount == 1)
            {
                Say("Привет! Я первая овечка.\nНе видел моих друзей?");
                return;
            }

            Say("Не нажимай синхронизацию!\nПомоги найти всех моих друзей");
            AddMovingSheepFriend(_sheepCount);
        }

        private void AddMovingSheepFriend(int sheepNumber)
        {
            if (_canvas == null)
            {
                return;
            }

            FrameworkElement sheepVisual = CreateSmallSheepVisual();
            Border bubble = CreateSheepBubble(GetSheepFriendMessage(sheepNumber));
            MovingSheepSprite sheep = new MovingSheepSprite();
            sheep.Visual = sheepVisual;
            sheep.Bubble = bubble;
            sheep.Left = GetRandomLeft(MovingSheepWidth);
            sheep.Top = GetRandomTop(MovingSheepHeight);
            sheep.VelocityX = (_random.NextDouble() > 0.5 ? 1.0 : -1.0) * (2.4 + _random.NextDouble() * 2.4);
            sheep.VelocityY = (_random.NextDouble() > 0.5 ? 1.0 : -1.0) * (1.7 + _random.NextDouble() * 2.0);
            sheep.Width = MovingSheepWidth;
            sheep.Height = MovingSheepHeight;

            Canvas.SetLeft(sheep.Visual, sheep.Left);
            Canvas.SetTop(sheep.Visual, sheep.Top);
            _canvas.Children.Insert(0, sheep.Visual);
            _canvas.Children.Add(sheep.Bubble);
            _movingSheepSprites.Add(sheep);
            UpdateMovingSheepPosition(sheep);
        }

        private Border CreateSheepBubble(string text)
        {
            TextBlock textBlock = new TextBlock();
            textBlock.Text = text;
            textBlock.FontFamily = new FontFamily("Segoe UI");
            textBlock.FontSize = 12.5;
            textBlock.FontWeight = FontWeights.SemiBold;
            textBlock.Foreground = CreateBrush("#1F2937");
            textBlock.TextWrapping = TextWrapping.Wrap;
            textBlock.Width = 220;

            Border bubble = new Border();
            bubble.MinWidth = 230;
            bubble.MaxWidth = 260;
            bubble.Padding = new Thickness(10, 7, 10, 7);
            bubble.CornerRadius = new CornerRadius(10);
            bubble.Background = new SolidColorBrush(Color.FromArgb(238, 255, 255, 255));
            bubble.BorderBrush = CreateBrush("#B8C2CC");
            bubble.BorderThickness = new Thickness(1);
            bubble.Child = textBlock;
            return bubble;
        }

        private void MoveSheepFriends()
        {
            for (int i = 0; i < _movingSheepSprites.Count; i++)
            {
                MovingSheepSprite sheep = _movingSheepSprites[i];
                if (sheep == null)
                {
                    continue;
                }

                sheep.Left += sheep.VelocityX;
                sheep.Top += sheep.VelocityY;

                double minLeft = 8.0;
                double minTop = 8.0;
                double maxLeft = Math.Max(minLeft, Width - sheep.Width - 12.0);
                double maxTop = Math.Max(minTop, Height - sheep.Height - 24.0);

                if (sheep.Left <= minLeft || sheep.Left >= maxLeft)
                {
                    sheep.VelocityX = -sheep.VelocityX;
                    sheep.Left = Clamp(sheep.Left, minLeft, maxLeft);
                }

                if (sheep.Top <= minTop || sheep.Top >= maxTop)
                {
                    sheep.VelocityY = -sheep.VelocityY;
                    sheep.Top = Clamp(sheep.Top, minTop, maxTop);
                }

                UpdateMovingSheepPosition(sheep);
            }
        }

        private void UpdateMovingSheepPosition(MovingSheepSprite sheep)
        {
            if (sheep == null || sheep.Visual == null)
            {
                return;
            }

            Canvas.SetLeft(sheep.Visual, sheep.Left);
            Canvas.SetTop(sheep.Visual, sheep.Top);

            sheep.Visual.RenderTransformOrigin = new Point(0.5, 0.5);
            sheep.Visual.RenderTransform = sheep.VelocityX < 0.0
                ? new ScaleTransform(-1.0, 1.0)
                : new ScaleTransform(1.0, 1.0);

            if (sheep.Bubble == null)
            {
                return;
            }

            sheep.Bubble.Measure(new Size(260, double.PositiveInfinity));
            double bubbleWidth = Math.Max(230.0, sheep.Bubble.DesiredSize.Width);
            double bubbleHeight = Math.Max(44.0, sheep.Bubble.DesiredSize.Height);
            double left = Clamp(sheep.Left + sheep.Width * 0.42, 8.0, Math.Max(8.0, Width - bubbleWidth - 8.0));
            double top = Clamp(sheep.Top - bubbleHeight + 18.0, 8.0, Math.Max(8.0, Height - bubbleHeight - 8.0));
            Canvas.SetLeft(sheep.Bubble, left);
            Canvas.SetTop(sheep.Bubble, top);
        }

        private string GetSheepFriendMessage(int sheepNumber)
        {
            string orderText = GetSheepOrderText(sheepNumber);
            return "Привет! Я " + orderText + " овечка!\nА где остальные наши друзья?";
        }

        private static string GetSheepOrderText(int sheepNumber)
        {
            switch (sheepNumber)
            {
                case 1:
                    return "первая";
                case 2:
                    return "вторая";
                case 3:
                    return "третья";
                case 4:
                    return "четвертая";
                case 5:
                    return "пятая";
                case 6:
                    return "шестая";
                case 7:
                    return "седьмая";
                case 8:
                    return "восьмая";
                case 9:
                    return "девятая";
                case 10:
                    return "десятая";
                default:
                    return sheepNumber + "-я";
            }
        }

        private void UpdateScenarioTimerInterval()
        {
            if (_scenarioTimer == null)
            {
                return;
            }

            // Block responsible for how often scenario objects appear on the Revit workspace.
            if (_mode == SyncReminderAnimationMode.SheepCounter)
            {
                _scenarioTimer.Interval = TimeSpan.FromSeconds(SheepSpawnIntervalSeconds);
                return;
            }

            _scenarioTimer.Interval = TimeSpan.FromSeconds(DefaultScenarioIntervalSeconds);
        }

        private SpawnedElement AddSpawnedElement(FrameworkElement element, double left, double top, double width, double height)
        {
            if (_canvas == null || element == null)
            {
                return null;
            }

            if (_spawnedElements.Count >= MaximumSpawnedElements)
            {
                RemoveOldestSpawnedElement();
            }

            Canvas.SetLeft(element, left);
            Canvas.SetTop(element, top);
            _canvas.Children.Insert(0, element);

            SpawnedElement spawnedElement = new SpawnedElement();
            spawnedElement.Element = element;
            spawnedElement.Left = left;
            spawnedElement.Top = top;
            spawnedElement.Width = width;
            spawnedElement.Height = height;
            _spawnedElements.Add(spawnedElement);
            return spawnedElement;
        }

        private void RemoveOldestSpawnedElement()
        {
            if (_spawnedElements.Count == 0)
            {
                return;
            }

            int removeIndex = 0;
            if (_spawnedElements.Count > 1 && _spawnedElements[0] == _primaryTarget)
            {
                removeIndex = 1;
            }

            SpawnedElement oldestElement = _spawnedElements[removeIndex];
            if (oldestElement != null && oldestElement.Element != null && _canvas != null)
            {
                _canvas.Children.Remove(oldestElement.Element);
            }

            if (oldestElement == _primaryTarget)
            {
                _primaryTarget = null;
            }

            _spawnedElements.RemoveAt(removeIndex);
        }

        private void ClearScenarioElements()
        {
            if (_canvas != null)
            {
                for (int i = 0; i < _spawnedElements.Count; i++)
                {
                    SpawnedElement element = _spawnedElements[i];
                    if (element != null && element.Element != null)
                    {
                        _canvas.Children.Remove(element.Element);
                    }
                }

                for (int i = 0; i < _movingSheepSprites.Count; i++)
                {
                    MovingSheepSprite sheep = _movingSheepSprites[i];
                    if (sheep == null)
                    {
                        continue;
                    }

                    if (sheep.Visual != null)
                    {
                        _canvas.Children.Remove(sheep.Visual);
                    }

                    if (sheep.Bubble != null)
                    {
                        _canvas.Children.Remove(sheep.Bubble);
                    }
                }
            }

            _spawnedElements.Clear();
            _movingSheepSprites.Clear();
            _primaryTarget = null;
            _sheepCount = 0;
            _pigJumpCooldownTicks = 0;
            _currentPigMessageIndex = 0;
            _foxTargetRotation = 0.0;
        }

        private void ClampAnimalToWindow()
        {
            _animalLeft = Clamp(_animalLeft, -AnimalWidth - 20.0, Math.Max(8.0, Width - AnimalWidth - 12.0));
            _animalTop = Clamp(_animalTop, 8.0, Math.Max(8.0, Height - AnimalHeight - 24.0));
        }

        private void ClampSpawnedElementsToWindow()
        {
            for (int i = 0; i < _spawnedElements.Count; i++)
            {
                SpawnedElement element = _spawnedElements[i];
                if (element == null)
                {
                    continue;
                }

                element.Left = Clamp(element.Left, 4.0, Math.Max(4.0, Width - element.Width - 4.0));
                element.Top = Clamp(element.Top, 4.0, Math.Max(4.0, Height - element.Height - 4.0));

                if (element.Element != null)
                {
                    Canvas.SetLeft(element.Element, element.Left);
                    Canvas.SetTop(element.Element, element.Top);
                }
            }

            for (int i = 0; i < _movingSheepSprites.Count; i++)
            {
                MovingSheepSprite sheep = _movingSheepSprites[i];
                if (sheep == null)
                {
                    continue;
                }

                sheep.Left = Clamp(sheep.Left, 4.0, Math.Max(4.0, Width - sheep.Width - 4.0));
                sheep.Top = Clamp(sheep.Top, 4.0, Math.Max(4.0, Height - sheep.Height - 4.0));
                UpdateMovingSheepPosition(sheep);
            }
        }

        private void UpdateAnimalPosition()
        {
            if (_animalVisual == null || _shadow == null)
            {
                return;
            }

            Canvas.SetLeft(_animalVisual, _animalLeft);
            Canvas.SetTop(_animalVisual, _animalTop);
            Canvas.SetLeft(_shadow, _animalLeft + 12.0);
            Canvas.SetTop(_shadow, _animalTop + AnimalHeight - 14.0);

            if (_velocityX < 0.0)
            {
                _animalVisual.RenderTransformOrigin = new Point(0.5, 0.5);
                _animalVisual.RenderTransform = new ScaleTransform(-1.0, 1.0);
            }
            else
            {
                _animalVisual.RenderTransform = new ScaleTransform(1.0, 1.0);
            }
        }

        private void UpdateBubblePosition()
        {
            if (_bubbleBorder == null || _bubbleText == null)
            {
                return;
            }

            double bubbleWidth = Math.Min(330.0, Math.Max(210.0, MeasureTextWidth(_bubbleText.Text) + 28.0));
            _bubbleText.Width = bubbleWidth - 22.0;
            _bubbleText.Measure(new Size(_bubbleText.Width, double.PositiveInfinity));
            _bubbleBorder.Width = bubbleWidth;
            _bubbleBorder.Height = Math.Max(42.0, Math.Min(98.0, _bubbleText.DesiredSize.Height + 16.0));

            double left = Clamp(_animalLeft + AnimalWidth * 0.42, 8.0, Math.Max(8.0, Width - _bubbleBorder.Width - 8.0));
            double top = Clamp(_animalTop - _bubbleBorder.Height + 18.0, 8.0, Math.Max(8.0, Height - _bubbleBorder.Height - 8.0));
            Canvas.SetLeft(_bubbleBorder, left);
            Canvas.SetTop(_bubbleBorder, top);
        }

        private void SayNextMessage()
        {
            string[] messages = GetMessagesForCurrentMode();
            if (messages.Length == 0)
            {
                return;
            }

            _currentMessageIndex++;
            if (_currentMessageIndex >= messages.Length)
            {
                _currentMessageIndex = 0;
            }

            Say(messages[_currentMessageIndex]);
        }

        private void Say(string text)
        {
            if (_bubbleText == null)
            {
                return;
            }

            _bubbleText.Text = text;
            UpdateBubblePosition();
        }

        private string[] GetMessagesForCurrentMode()
        {
            if (_mode == SyncReminderAnimationMode.FoxWithSyncButton)
            {
                return new[]
                {
                    "Помоги поймать кнопку синхронизации!",
                    "Она опять укатилась!\nНажми синхронизацию и помоги мне ее поймать.",
                    "Я почти догнала кнопку.\nСинхронизируйся, пока она рядом!",
                    "Кнопка быстрая, но мы быстрее.\nСинхронизируй модель!"
                };
            }

            if (_mode == SyncReminderAnimationMode.PigMud)
            {
                return new[]
                {
                    "Вот это лужа! Пойду прыгну, пока ты не нажал синхронизацию",
                    "Ого, эта лужа еще больше! Только не нажимай синхронизацию, а то прыгать будет некуда"
                };
            }

            return new[]
            {
                "Считаю овец без синхронизации.",
                "Каждая овца - ещё один повод нажать Sync.",
                "Если уснул, синхронизируйся и просыпайся."
            };
        }

        private double GetRandomLeft(double elementWidth)
        {
            return 24.0 + _random.NextDouble() * Math.Max(1.0, Width - elementWidth - 48.0);
        }

        private double GetRandomTop(double elementHeight)
        {
            return 38.0 + _random.NextDouble() * Math.Max(1.0, Height - elementHeight - 76.0);
        }

        private static BitmapImage[] LoadAnimalFrames(SyncReminderAnimationMode mode)
        {
            // Block responsible for optional downloaded sprites:
            // SyncReminder\Assets\Animals\Fox, Pig, Sheep.
            string folderName = GetAssetFolderName(mode);
            if (string.IsNullOrWhiteSpace(folderName))
            {
                return new BitmapImage[0];
            }

            string assemblyFolder = System.IO.Path.GetDirectoryName(typeof(AnimalReminderWindow).Assembly.Location);
            if (string.IsNullOrWhiteSpace(assemblyFolder))
            {
                return new BitmapImage[0];
            }

            string folderPath = System.IO.Path.Combine(assemblyFolder, "SyncReminder", "Assets", "Animals", folderName);
            if (!Directory.Exists(folderPath))
            {
                return new BitmapImage[0];
            }

            string[] filePaths = Directory.GetFiles(folderPath, "*.png");
            Array.Sort(filePaths, StringComparer.OrdinalIgnoreCase);

            List<BitmapImage> frames = new List<BitmapImage>();
            for (int i = 0; i < filePaths.Length; i++)
            {
                BitmapImage image = LoadImage(filePaths[i]);
                if (image != null)
                {
                    frames.Add(image);
                }
            }

            return frames.ToArray();
        }

        private static BitmapImage LoadImage(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
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

        private static string GetAssetFolderName(SyncReminderAnimationMode mode)
        {
            if (mode == SyncReminderAnimationMode.FoxWithSyncButton)
            {
                return "Fox";
            }

            if (mode == SyncReminderAnimationMode.PigMud)
            {
                return "Pig";
            }

            if (mode == SyncReminderAnimationMode.SheepCounter)
            {
                return "Sheep";
            }

            return string.Empty;
        }

        private static bool IsAnimalMode(SyncReminderAnimationMode mode)
        {
            return mode == SyncReminderAnimationMode.FoxWithSyncButton
                   || mode == SyncReminderAnimationMode.PigMud
                   || mode == SyncReminderAnimationMode.SheepCounter;
        }

        private double MeasureTextWidth(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 1.0;
            }

            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            double maxWidth = 1.0;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Length > 0)
                {
                    maxWidth = Math.Max(maxWidth, line.Length * 7.8);
                }
            }

            return maxWidth;
        }

        private static void AddEllipse(Canvas canvas, double left, double top, double width, double height, Brush fill, Brush stroke)
        {
            Ellipse ellipse = new Ellipse();
            ellipse.Width = width;
            ellipse.Height = height;
            ellipse.Fill = fill;
            ellipse.Stroke = stroke;
            ellipse.StrokeThickness = stroke == null ? 0 : 1;
            Canvas.SetLeft(ellipse, left);
            Canvas.SetTop(ellipse, top);
            canvas.Children.Add(ellipse);
        }

        private static void AddRectangle(Canvas canvas, double left, double top, double width, double height, Brush fill, double radius)
        {
            Border border = new Border();
            border.Width = width;
            border.Height = height;
            border.Background = fill;
            border.CornerRadius = new CornerRadius(radius);
            Canvas.SetLeft(border, left);
            Canvas.SetTop(border, top);
            canvas.Children.Add(border);
        }

        private static void AddPolygon(Canvas canvas, Point[] points, Brush fill, Brush stroke)
        {
            Polygon polygon = new Polygon();
            polygon.Points = new PointCollection(points);
            polygon.Fill = fill;
            polygon.Stroke = stroke;
            polygon.StrokeThickness = stroke == null ? 0 : 1;
            canvas.Children.Add(polygon);
        }

        private static void AddLine(Canvas canvas, double x1, double y1, double x2, double y2, Brush stroke, double thickness)
        {
            Line line = new Line();
            line.X1 = x1;
            line.Y1 = y1;
            line.X2 = x2;
            line.Y2 = y2;
            line.Stroke = stroke;
            line.StrokeThickness = thickness;
            line.StrokeStartLineCap = PenLineCap.Round;
            line.StrokeEndLineCap = PenLineCap.Round;
            canvas.Children.Add(line);
        }

        private static SolidColorBrush CreateBrush(string color)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
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

        private sealed class SpawnedElement
        {
            public FrameworkElement Element { get; set; }

            public double Left { get; set; }

            public double Top { get; set; }

            public double Width { get; set; }

            public double Height { get; set; }
        }

        private sealed class MovingSheepSprite
        {
            public FrameworkElement Visual { get; set; }

            public Border Bubble { get; set; }

            public double Left { get; set; }

            public double Top { get; set; }

            public double Width { get; set; }

            public double Height { get; set; }

            public double VelocityX { get; set; }

            public double VelocityY { get; set; }
        }
    }
}
