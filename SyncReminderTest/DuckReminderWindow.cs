using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SyncReminderTest
{
    internal class DuckReminderWindow : Window
    {
        private readonly DispatcherTimer _moveTimer;
        private readonly DispatcherTimer _messageTimer;
        private readonly Random _random;
        private Rect _allowedArea;
        private double _velocityX;
        private double _velocityY;
        private TextBlock _bubbleText;

        public DuckReminderWindow(IntPtr ownerHandle)
        {
            Width = 286;
            Height = 176;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = false;
            AllowsTransparency = true;
            Background = Brushes.Transparent;

            _random = new Random();
            _velocityX = 1.4;
            _velocityY = 0.9;
            _allowedArea = Rect.Empty;

            Content = CreateDuckContent();

            WindowInteropHelper helper = new WindowInteropHelper(this);
            helper.Owner = ownerHandle;

            SourceInitialized += OnSourceInitialized;
            MouseEnter += OnMouseEnter;

            _moveTimer = new DispatcherTimer();
            _moveTimer.Interval = TimeSpan.FromMilliseconds(28);
            _moveTimer.Tick += OnMoveTimerTick;

            _messageTimer = new DispatcherTimer();
            _messageTimer.Interval = TimeSpan.FromSeconds(7);
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

            if (!_messageTimer.IsEnabled)
            {
                _messageTimer.Start();
            }

            Say("Кря! Синхронизируйся");
        }

        public void HideDuck()
        {
            _moveTimer.Stop();
            _messageTimer.Stop();

            if (IsVisible)
            {
                Hide();
            }
        }

        public void CloseDuck()
        {
            _moveTimer.Stop();
            _messageTimer.Stop();
            Close();
        }

        private UIElement CreateDuckContent()
        {
            Canvas canvas = new Canvas();
            canvas.Width = Width;
            canvas.Height = Height;

            // Block responsible for the speech bubble text. Change the phrase here if needed.
            Border bubble = new Border();
            bubble.Width = 254;
            bubble.Height = 68;
            bubble.CornerRadius = new CornerRadius(12);
            bubble.Padding = new Thickness(14, 8, 14, 8);
            bubble.Background = new SolidColorBrush(Color.FromArgb(238, 255, 255, 255));
            bubble.BorderBrush = new SolidColorBrush(Color.FromArgb(200, 120, 148, 165));
            bubble.BorderThickness = new Thickness(1);
            bubble.ClipToBounds = true;

            _bubbleText = new TextBlock();
            _bubbleText.Text = "Синхронизируйся";
            _bubbleText.FontFamily = new FontFamily("Segoe UI");
            _bubbleText.FontSize = 15;
            _bubbleText.FontWeight = FontWeights.SemiBold;
            _bubbleText.Foreground = new SolidColorBrush(Color.FromRgb(33, 43, 52));
            _bubbleText.TextWrapping = TextWrapping.Wrap;
            _bubbleText.TextTrimming = TextTrimming.CharacterEllipsis;
            _bubbleText.LineHeight = 18;
            _bubbleText.Height = 50;
            _bubbleText.MaxHeight = 50;
            _bubbleText.VerticalAlignment = VerticalAlignment.Center;
            bubble.Child = _bubbleText;

            Canvas.SetLeft(bubble, 12);
            Canvas.SetTop(bubble, 4);
            canvas.Children.Add(bubble);

            Polygon bubbleTail = new Polygon();
            bubbleTail.Points = new PointCollection
            {
                new Point(84, 67),
                new Point(104, 67),
                new Point(96, 82)
            };
            bubbleTail.Fill = bubble.Background;
            bubbleTail.Stroke = bubble.BorderBrush;
            bubbleTail.StrokeThickness = 1;
            canvas.Children.Add(bubbleTail);

            // Block responsible for the duck vector drawing.
            Ellipse shadow = new Ellipse();
            shadow.Width = 106;
            shadow.Height = 18;
            shadow.Fill = new SolidColorBrush(Color.FromArgb(70, 50, 66, 72));
            Canvas.SetLeft(shadow, 62);
            Canvas.SetTop(shadow, 154);
            canvas.Children.Add(shadow);

            Ellipse body = new Ellipse();
            body.Width = 92;
            body.Height = 55;
            body.Fill = new SolidColorBrush(Color.FromRgb(247, 197, 57));
            body.Stroke = new SolidColorBrush(Color.FromRgb(203, 151, 27));
            body.StrokeThickness = 2;
            Canvas.SetLeft(body, 68);
            Canvas.SetTop(body, 110);
            canvas.Children.Add(body);

            Ellipse wing = new Ellipse();
            wing.Width = 40;
            wing.Height = 25;
            wing.Fill = new SolidColorBrush(Color.FromRgb(232, 172, 35));
            Canvas.SetLeft(wing, 96);
            Canvas.SetTop(wing, 126);
            canvas.Children.Add(wing);

            Ellipse head = new Ellipse();
            head.Width = 48;
            head.Height = 45;
            head.Fill = new SolidColorBrush(Color.FromRgb(250, 207, 71));
            head.Stroke = new SolidColorBrush(Color.FromRgb(203, 151, 27));
            head.StrokeThickness = 2;
            Canvas.SetLeft(head, 46);
            Canvas.SetTop(head, 95);
            canvas.Children.Add(head);

            Polygon beak = new Polygon();
            beak.Points = new PointCollection
            {
                new Point(43, 115),
                new Point(14, 124),
                new Point(43, 132)
            };
            beak.Fill = new SolidColorBrush(Color.FromRgb(238, 127, 38));
            beak.Stroke = new SolidColorBrush(Color.FromRgb(181, 79, 24));
            beak.StrokeThickness = 1.5;
            canvas.Children.Add(beak);

            Ellipse eyeWhite = new Ellipse();
            eyeWhite.Width = 10;
            eyeWhite.Height = 10;
            eyeWhite.Fill = Brushes.White;
            Canvas.SetLeft(eyeWhite, 65);
            Canvas.SetTop(eyeWhite, 107);
            canvas.Children.Add(eyeWhite);

            Ellipse eye = new Ellipse();
            eye.Width = 5;
            eye.Height = 5;
            eye.Fill = new SolidColorBrush(Color.FromRgb(24, 28, 30));
            Canvas.SetLeft(eye, 68);
            Canvas.SetTop(eye, 110);
            canvas.Children.Add(eye);

            Rectangle legOne = new Rectangle();
            legOne.Width = 5;
            legOne.Height = 13;
            legOne.RadiusX = 2;
            legOne.RadiusY = 2;
            legOne.Fill = new SolidColorBrush(Color.FromRgb(221, 109, 33));
            Canvas.SetLeft(legOne, 100);
            Canvas.SetTop(legOne, 160);
            canvas.Children.Add(legOne);

            Rectangle legTwo = new Rectangle();
            legTwo.Width = 5;
            legTwo.Height = 13;
            legTwo.RadiusX = 2;
            legTwo.RadiusY = 2;
            legTwo.Fill = new SolidColorBrush(Color.FromRgb(221, 109, 33));
            Canvas.SetLeft(legTwo, 128);
            Canvas.SetTop(legTwo, 159);
            canvas.Children.Add(legTwo);

            return canvas;
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            RevitWindowUtils.MakeWindowNoActivate(this);
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
                Left = Math.Max(minLeft, Math.Min(maxLeft, Left));
            }

            if (Top <= minTop || Top >= maxTop)
            {
                _velocityY = -_velocityY;
                Top = Math.Max(minTop, Math.Min(maxTop, Top));
            }
        }

        private void OnMouseEnter(object sender, MouseEventArgs e)
        {
            RunAwayFromMouse(e.GetPosition(this));
        }

        private void OnMessageTimerTick(object sender, EventArgs e)
        {
            Say("Кря! Синхронизируйся");
        }

        private void RunAwayFromMouse(Point mousePosition)
        {
            double duckCenterX = Width / 2;
            double duckCenterY = Height / 2;
            double vectorX = duckCenterX - mousePosition.X;
            double vectorY = duckCenterY - mousePosition.Y;

            if (Math.Abs(vectorX) < 1)
            {
                vectorX = _random.Next(0, 2) == 0 ? -1 : 1;
            }

            if (Math.Abs(vectorY) < 1)
            {
                vectorY = _random.Next(0, 2) == 0 ? -1 : 1;
            }

            double length = Math.Sqrt(vectorX * vectorX + vectorY * vectorY);
            _velocityX = vectorX / length * 7.5;
            _velocityY = vectorY / length * 5.5;

            Left += _velocityX * 10;
            Top += _velocityY * 10;
            KeepInsideAllowedArea();
            Say("Не поймаешь. Сначала синхронизация");
        }

        private void Say(string text)
        {
            if (_bubbleText != null)
            {
                _bubbleText.Text = text;
            }
        }

        private void PutDuckInsideAllowedArea()
        {
            if (_allowedArea.IsEmpty)
            {
                return;
            }

            double maxLeft = Math.Max(_allowedArea.Left, _allowedArea.Right - Width);
            double maxTop = Math.Max(_allowedArea.Top, _allowedArea.Bottom - Height);

            Left = _allowedArea.Left + _random.NextDouble() * Math.Max(1, maxLeft - _allowedArea.Left);
            Top = _allowedArea.Top + _random.NextDouble() * Math.Max(1, maxTop - _allowedArea.Top);
        }

        private void KeepInsideAllowedArea()
        {
            if (_allowedArea.IsEmpty)
            {
                return;
            }

            double maxLeft = _allowedArea.Right - Width;
            double maxTop = _allowedArea.Bottom - Height;

            Left = Math.Max(_allowedArea.Left, Math.Min(maxLeft, Left));
            Top = Math.Max(_allowedArea.Top, Math.Min(maxTop, Top));
        }
    }
}
