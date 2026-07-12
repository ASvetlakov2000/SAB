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
        private const double AnimalWidth = 96.0;
        private const double AnimalHeight = 96.0;
        private const double AnimalSpeed = 4.8;
        private const double DefaultScenarioIntervalSeconds = 2.2;
        private const double SheepSpawnIntervalSeconds = 5.0;
        private const int MaximumSpawnedElements = 140;

        private readonly IntPtr _ownerHandle;
        private readonly Random _random;
        private readonly DispatcherTimer _moveTimer;
        private readonly DispatcherTimer _frameTimer;
        private readonly DispatcherTimer _scenarioTimer;
        private readonly DispatcherTimer _messageTimer;
        private readonly DispatcherTimer _ownerStateTimer;
        private readonly List<SpawnedElement> _spawnedElements;

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

        public AnimalReminderWindow(IntPtr ownerHandle)
        {
            _ownerHandle = ownerHandle;
            _random = new Random();
            _allowedArea = Rect.Empty;
            _spawnedElements = new List<SpawnedElement>();
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
        }

        private void OnMoveTimerTick(object sender, EventArgs e)
        {
            if (_canvas == null)
            {
                return;
            }

            if (_mode == SyncReminderAnimationMode.FoxWithSyncButton)
            {
                MoveAnimalToPrimaryTarget();
            }
            else if (_mode == SyncReminderAnimationMode.RoosterAlarm)
            {
                MoveRoosterAlarm();
            }
            else
            {
                MoveAnimalByVelocity();
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

            if (_mode == SyncReminderAnimationMode.RoosterAlarm)
            {
                Say("Ку-ка-ре-синх!\nПора нажать синхронизацию!");
                return;
            }

            if (_mode == SyncReminderAnimationMode.BoarSyncSigns)
            {
                AddSyncSign();
                return;
            }

            if (_mode == SyncReminderAnimationMode.DeerFootprints)
            {
                AddLeafTrace();
                return;
            }

            if (_mode == SyncReminderAnimationMode.PigMud)
            {
                AddMudSpot();
                return;
            }

            if (_mode == SyncReminderAnimationMode.SheepCounter)
            {
                AddSheepCounterItem();
            }
        }

        private void OnMessageTimerTick(object sender, EventArgs e)
        {
            if (_mode == SyncReminderAnimationMode.SheepCounter)
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
            else if (_mode == SyncReminderAnimationMode.BoarSyncSigns && _spawnedElements.Count == 0)
            {
                AddSyncSign();
            }
            else if (_mode == SyncReminderAnimationMode.DeerFootprints && _spawnedElements.Count == 0)
            {
                AddLeafTrace();
            }
            else if (_mode == SyncReminderAnimationMode.PigMud && _spawnedElements.Count == 0)
            {
                AddMudSpot();
            }
            else if (_mode == SyncReminderAnimationMode.SheepCounter && _spawnedElements.Count == 0)
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
            animalCanvas.Width = AnimalWidth;
            animalCanvas.Height = AnimalHeight;

            if (mode == SyncReminderAnimationMode.FoxWithSyncButton)
            {
                BuildFox(animalCanvas);
            }
            else if (mode == SyncReminderAnimationMode.RoosterAlarm)
            {
                BuildRooster(animalCanvas);
            }
            else if (mode == SyncReminderAnimationMode.BoarSyncSigns)
            {
                BuildBoar(animalCanvas);
            }
            else if (mode == SyncReminderAnimationMode.DeerFootprints)
            {
                BuildDeer(animalCanvas);
            }
            else if (mode == SyncReminderAnimationMode.PigMud)
            {
                BuildPig(animalCanvas);
            }
            else
            {
                BuildSheep(animalCanvas, 1.0);
            }

            return animalCanvas;
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

        private void BuildRooster(Canvas canvas)
        {
            Brush white = CreateBrush("#F8FAFC");
            Brush red = CreateBrush("#D92D20");
            Brush yellow = CreateBrush("#FACC15");
            Brush dark = CreateBrush("#1F2937");
            Brush brown = CreateBrush("#B46A2B");

            AddEllipse(canvas, 28, 34, 40, 38, white, CreateBrush("#D8DEE8"));
            AddEllipse(canvas, 50, 20, 30, 28, white, CreateBrush("#D8DEE8"));
            AddEllipse(canvas, 51, 10, 9, 13, red, null);
            AddEllipse(canvas, 60, 8, 9, 14, red, null);
            AddEllipse(canvas, 68, 12, 8, 12, red, null);
            AddPolygon(canvas, new Point[] { new Point(77, 32), new Point(92, 38), new Point(77, 43) }, yellow, null);
            AddEllipse(canvas, 67, 29, 4, 4, dark, null);
            AddPolygon(canvas, new Point[] { new Point(27, 42), new Point(7, 27), new Point(14, 56) }, brown, null);
            AddRectangle(canvas, 39, 70, 5, 14, yellow, 2);
            AddRectangle(canvas, 56, 70, 5, 14, yellow, 2);
        }

        private void BuildBoar(Canvas canvas)
        {
            Brush body = CreateBrush("#7A4A2A");
            Brush dark = CreateBrush("#3B2416");
            Brush tusk = CreateBrush("#FFF7E6");

            AddEllipse(canvas, 20, 38, 54, 34, body, null);
            AddEllipse(canvas, 56, 30, 32, 30, body, null);
            AddEllipse(canvas, 76, 42, 14, 10, dark, null);
            AddEllipse(canvas, 66, 38, 4, 4, CreateBrush("#111111"), null);
            AddPolygon(canvas, new Point[] { new Point(74, 52), new Point(88, 60), new Point(75, 58) }, tusk, null);
            AddPolygon(canvas, new Point[] { new Point(58, 31), new Point(60, 16), new Point(70, 32) }, dark, null);
            AddRectangle(canvas, 30, 66, 8, 18, dark, 3);
            AddRectangle(canvas, 58, 64, 8, 18, dark, 3);
        }

        private void BuildDeer(Canvas canvas)
        {
            Brush body = CreateBrush("#C98B49");
            Brush cream = CreateBrush("#FFE8BF");
            Brush dark = CreateBrush("#1F2937");

            AddEllipse(canvas, 24, 40, 48, 30, body, null);
            AddEllipse(canvas, 54, 24, 30, 28, body, null);
            AddEllipse(canvas, 60, 38, 18, 10, cream, null);
            AddEllipse(canvas, 70, 32, 4, 4, dark, null);
            AddLine(canvas, 61, 24, 54, 10, dark, 3);
            AddLine(canvas, 72, 24, 82, 10, dark, 3);
            AddLine(canvas, 54, 10, 48, 7, dark, 2);
            AddLine(canvas, 82, 10, 89, 7, dark, 2);
            AddEllipse(canvas, 31, 47, 4, 4, cream, null);
            AddEllipse(canvas, 44, 45, 4, 4, cream, null);
            AddRectangle(canvas, 32, 66, 6, 18, dark, 2);
            AddRectangle(canvas, 58, 64, 6, 18, dark, 2);
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
            canvas.Width = 62;
            canvas.Height = 52;
            BuildSheep(canvas, 0.62);
            return canvas;
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

            if (distance < 40.0)
            {
                MovePrimaryTargetToRandomPoint();
                Say("Я бы нажала синхронизацию,\nно кнопка вкусно выглядит.");
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

        private void MoveRoosterAlarm()
        {
            _animalLeft += Math.Abs(_velocityX) + 3.6;

            if (_animalLeft > Width + 30.0)
            {
                _animalLeft = -AnimalWidth - 20.0;
                _animalTop = 35.0 + _random.NextDouble() * Math.Max(40.0, Height - AnimalHeight - 90.0);
                Say("Ку-ка-ре-синх!\nЯ снова пришел!");
            }
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

        private void PrepareStartPosition()
        {
            if (_mode == SyncReminderAnimationMode.RoosterAlarm)
            {
                _animalLeft = -AnimalWidth;
                _animalTop = Height * 0.32;
                _velocityX = Math.Abs(_velocityX);
            }
            else
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
            _primaryTarget = AddSpawnedElement(syncButton, GetRandomLeft(96), GetRandomTop(46), 96, 46);
        }

        private FrameworkElement CreateSyncButtonVisual()
        {
            Border border = new Border();
            border.Width = 96;
            border.Height = 46;
            border.CornerRadius = new CornerRadius(8);
            border.Background = CreateBrush("#EAF3FF");
            border.BorderBrush = CreateBrush("#0F6CBD");
            border.BorderThickness = new Thickness(2);

            TextBlock text = new TextBlock();
            text.Text = "SYNC";
            text.FontFamily = new FontFamily("Segoe UI");
            text.FontSize = 16;
            text.FontWeight = FontWeights.Bold;
            text.Foreground = CreateBrush("#0F6CBD");
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.VerticalAlignment = VerticalAlignment.Center;
            border.Child = text;

            return border;
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

        private void AddSyncSign()
        {
            Border sign = new Border();
            sign.Width = 74;
            sign.Height = 32;
            sign.CornerRadius = new CornerRadius(5);
            sign.Background = CreateBrush("#0F6CBD");
            sign.BorderBrush = CreateBrush("#0B5EA8");
            sign.BorderThickness = new Thickness(1);

            TextBlock text = new TextBlock();
            text.Text = "SYNC";
            text.FontFamily = new FontFamily("Segoe UI");
            text.FontSize = 13;
            text.FontWeight = FontWeights.Bold;
            text.Foreground = Brushes.White;
            text.HorizontalAlignment = HorizontalAlignment.Center;
            text.VerticalAlignment = VerticalAlignment.Center;
            sign.Child = text;

            AddSpawnedElement(sign, _animalLeft + 12.0, _animalTop + AnimalHeight - 22.0, 74, 32);
        }

        private void AddLeafTrace()
        {
            Canvas leaf = new Canvas();
            leaf.Width = 30;
            leaf.Height = 18;
            AddEllipse(leaf, 3, 4, 24, 10, CreateBrush("#6A994E"), CreateBrush("#386641"));
            AddLine(leaf, 7, 9, 24, 9, CreateBrush("#386641"), 1);
            AddSpawnedElement(leaf, _animalLeft + 28.0, _animalTop + AnimalHeight - 14.0, 30, 18);
        }

        private void AddMudSpot()
        {
            Canvas mud = new Canvas();
            mud.Width = 52;
            mud.Height = 30;
            Brush main = CreateBrush("#7A4A2A");
            Brush light = CreateBrush("#9B6338");
            AddEllipse(mud, 4, 12, 38, 13, main, null);
            AddEllipse(mud, 18, 5, 22, 16, light, null);
            AddEllipse(mud, 34, 14, 12, 8, main, null);
            AddSpawnedElement(mud, _animalLeft + 18.0, _animalTop + AnimalHeight - 18.0, 52, 30);
        }

        private void AddSheepCounterItem()
        {
            _sheepCount++;
            FrameworkElement sheep = CreateSmallSheepVisual();
            AddSpawnedElement(sheep, GetRandomLeft(62), GetRandomTop(52), 62, 52);
            Say("Это уже " + _sheepCount + "-я овца с момента последней синхронизации.");
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

            SpawnedElement oldestElement = _spawnedElements[0];
            if (oldestElement != null && oldestElement.Element != null && _canvas != null)
            {
                _canvas.Children.Remove(oldestElement.Element);
            }

            if (oldestElement == _primaryTarget)
            {
                _primaryTarget = null;
            }

            _spawnedElements.RemoveAt(0);
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
            }

            _spawnedElements.Clear();
            _primaryTarget = null;
            _sheepCount = 0;
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
                    "Синхро-кнопка у меня.\nДогони или синхронизируйся.",
                    "Лиса сказала: модель пора делить с коллегами.",
                    "Я не ворую кнопку.\nЯ мотивирую."
                };
            }

            if (_mode == SyncReminderAnimationMode.RoosterAlarm)
            {
                return new[]
                {
                    "Ку-ка-ре-синх!",
                    "Пора синхронизироваться!",
                    "Я буду кричать,\nпока модель не увидят коллеги."
                };
            }

            if (_mode == SyncReminderAnimationMode.BoarSyncSigns)
            {
                return new[]
                {
                    "Кабан уже всё пометил табличками.",
                    "SYNC! SYNC! SYNC!\nДа, я настойчивый.",
                    "Синхронизация укротит кабана."
                };
            }

            if (_mode == SyncReminderAnimationMode.DeerFootprints)
            {
                return new[]
                {
                    "Я аккуратно наследил.\nСинхронизация всё уберёт.",
                    "Коллеги тоже хотят увидеть этот чертёж.",
                    "Тихо и вежливо напоминаю: синхронизируйся."
                };
            }

            if (_mode == SyncReminderAnimationMode.PigMud)
            {
                return new[]
                {
                    "Хрю! Тут немного грязно.\nСинхронизация поможет.",
                    "Я чистюля, честно.\nПросто давно не было синхры.",
                    "После синхронизации я приберусь."
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
            // SyncReminder\Assets\Animals\Fox, Rooster, Boar, Deer, Pig, Sheep.
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

            if (mode == SyncReminderAnimationMode.RoosterAlarm)
            {
                return "Rooster";
            }

            if (mode == SyncReminderAnimationMode.BoarSyncSigns)
            {
                return "Boar";
            }

            if (mode == SyncReminderAnimationMode.DeerFootprints)
            {
                return "Deer";
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
                   || mode == SyncReminderAnimationMode.RoosterAlarm
                   || mode == SyncReminderAnimationMode.BoarSyncSigns
                   || mode == SyncReminderAnimationMode.DeerFootprints
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
    }
}
