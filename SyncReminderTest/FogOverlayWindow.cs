using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SyncReminderTest
{
    internal class FogOverlayWindow : Window
    {
        private readonly FogVisual _fogVisual;

        public FogOverlayWindow(IntPtr ownerHandle)
        {
            Width = 600;
            Height = 160;
            Left = 0;
            Top = 0;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = false;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Opacity = 0;

            _fogVisual = new FogVisual();
            Content = _fogVisual;

            WindowInteropHelper helper = new WindowInteropHelper(this);
            helper.Owner = ownerHandle;

            SourceInitialized += OnSourceInitialized;
        }

        public void SetBounds(Rect bounds)
        {
            if (bounds.IsEmpty)
            {
                return;
            }

            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;
        }

        public void ShowFog()
        {
            if (!IsVisible)
            {
                Show();
            }

            _fogVisual.Start();
            BeginOpacityAnimation(0.78, 1400);
        }

        public void HideFog()
        {
            if (!IsVisible)
            {
                return;
            }

            DoubleAnimation animation = new DoubleAnimation();
            animation.To = 0;
            animation.Duration = TimeSpan.FromMilliseconds(700);
            animation.Completed += OnHideAnimationCompleted;
            BeginAnimation(OpacityProperty, animation);
        }

        public void CloseFog()
        {
            _fogVisual.Stop();
            Close();
        }

        private void BeginOpacityAnimation(double opacity, int milliseconds)
        {
            DoubleAnimation animation = new DoubleAnimation();
            animation.To = opacity;
            animation.Duration = TimeSpan.FromMilliseconds(milliseconds);
            BeginAnimation(OpacityProperty, animation);
        }

        private void OnHideAnimationCompleted(object sender, EventArgs e)
        {
            _fogVisual.Stop();
            Hide();
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            RevitWindowUtils.MakeWindowClickThrough(this);
        }

        private class FogVisual : FrameworkElement
        {
            private readonly List<FogSpot> _spots;
            private readonly List<FogDrop> _drops;
            private DateTime _startedAt;
            private bool _isRunning;

            public FogVisual()
            {
                _spots = CreateSpots();
                _drops = CreateDrops();
            }

            public void Start()
            {
                if (_isRunning)
                {
                    return;
                }

                _isRunning = true;
                _startedAt = DateTime.Now;
                CompositionTarget.Rendering += OnRendering;
            }

            public void Stop()
            {
                if (!_isRunning)
                {
                    return;
                }

                _isRunning = false;
                CompositionTarget.Rendering -= OnRendering;
            }

            protected override void OnRender(DrawingContext drawingContext)
            {
                base.OnRender(drawingContext);

                double width = ActualWidth;
                double height = ActualHeight;
                if (width <= 1 || height <= 1)
                {
                    return;
                }

                double seconds = (DateTime.Now - _startedAt).TotalSeconds;

                // Block responsible for the base mist opacity and color.
                LinearGradientBrush baseBrush = new LinearGradientBrush();
                baseBrush.StartPoint = new Point(0, 0);
                baseBrush.EndPoint = new Point(0, 1);
                baseBrush.GradientStops.Add(new GradientStop(Color.FromArgb(185, 246, 249, 250), 0));
                baseBrush.GradientStops.Add(new GradientStop(Color.FromArgb(120, 213, 224, 229), 0.48));
                baseBrush.GradientStops.Add(new GradientStop(Color.FromArgb(90, 245, 248, 249), 1));
                drawingContext.DrawRectangle(baseBrush, null, new Rect(0, 0, width, height));

                // Block responsible for soft moving fog spots.
                for (int i = 0; i < _spots.Count; i++)
                {
                    FogSpot spot = _spots[i];
                    double x = width * spot.X + Math.Sin(seconds * spot.Speed + spot.Phase) * spot.Drift;
                    double y = height * spot.Y + Math.Cos(seconds * spot.Speed * 0.7 + spot.Phase) * spot.Drift * 0.35;
                    double radius = spot.Radius * Math.Max(width, height);
                    byte alpha = (byte)(spot.Alpha + Math.Sin(seconds * 0.8 + spot.Phase) * 18);

                    RadialGradientBrush spotBrush = new RadialGradientBrush();
                    spotBrush.GradientOrigin = new Point(0.5, 0.5);
                    spotBrush.Center = new Point(0.5, 0.5);
                    spotBrush.RadiusX = 0.62;
                    spotBrush.RadiusY = 0.62;
                    spotBrush.GradientStops.Add(new GradientStop(Color.FromArgb(alpha, 255, 255, 255), 0));
                    spotBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));

                    drawingContext.DrawEllipse(spotBrush, null, new Point(x, y), radius, radius * 0.42);
                }

                // Block responsible for condensation drops and streaks.
                Pen dropPen = new Pen(new SolidColorBrush(Color.FromArgb(118, 255, 255, 255)), 1.2);
                Pen shadowPen = new Pen(new SolidColorBrush(Color.FromArgb(46, 120, 150, 160)), 0.8);

                for (int i = 0; i < _drops.Count; i++)
                {
                    FogDrop drop = _drops[i];
                    double wave = (seconds * drop.Speed + drop.Phase) % 1;
                    double x = width * drop.X + Math.Sin(seconds * drop.Speed + drop.Phase) * 5;
                    double y = height * ((drop.Y + wave * 0.18) % 1.0);
                    double length = height * drop.Length;

                    drawingContext.DrawLine(shadowPen, new Point(x + 1, y + 1), new Point(x + 1, y + length + 1));
                    drawingContext.DrawLine(dropPen, new Point(x, y), new Point(x, y + length));
                    drawingContext.DrawEllipse(Brushes.White, null, new Point(x, y), 2.2, 3.2);
                }
            }

            private void OnRendering(object sender, EventArgs e)
            {
                InvalidateVisual();
            }

            private static List<FogSpot> CreateSpots()
            {
                List<FogSpot> spots = new List<FogSpot>();
                Random random = new Random(202501);

                for (int i = 0; i < 34; i++)
                {
                    FogSpot spot = new FogSpot();
                    spot.X = random.NextDouble();
                    spot.Y = random.NextDouble();
                    spot.Radius = 0.06 + random.NextDouble() * 0.16;
                    spot.Alpha = 35 + random.Next(55);
                    spot.Speed = 0.25 + random.NextDouble() * 0.5;
                    spot.Phase = random.NextDouble() * Math.PI * 2;
                    spot.Drift = 8 + random.NextDouble() * 34;
                    spots.Add(spot);
                }

                return spots;
            }

            private static List<FogDrop> CreateDrops()
            {
                List<FogDrop> drops = new List<FogDrop>();
                Random random = new Random(202502);

                for (int i = 0; i < 16; i++)
                {
                    FogDrop drop = new FogDrop();
                    drop.X = random.NextDouble();
                    drop.Y = random.NextDouble();
                    drop.Length = 0.05 + random.NextDouble() * 0.22;
                    drop.Speed = 0.025 + random.NextDouble() * 0.06;
                    drop.Phase = random.NextDouble();
                    drops.Add(drop);
                }

                return drops;
            }

            private class FogSpot
            {
                public double X;
                public double Y;
                public double Radius;
                public int Alpha;
                public double Speed;
                public double Phase;
                public double Drift;
            }

            private class FogDrop
            {
                public double X;
                public double Y;
                public double Length;
                public double Speed;
                public double Phase;
            }
        }
    }
}
