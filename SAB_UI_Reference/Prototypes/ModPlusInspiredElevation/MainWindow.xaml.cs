using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace SAB.ModPlusInspiredElevationPrototype;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ContentRendered += MainWindow_ContentRendered;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        WindowRoot.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = easing
        });

        if (WindowRoot.RenderTransform is TranslateTransform translate)
        {
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = easing
            });
        }
    }

    private async void MainWindow_ContentRendered(object? sender, EventArgs e)
    {
        string? renderArgument = Environment.GetCommandLineArgs()
            .FirstOrDefault(argument => argument.StartsWith("--render=", StringComparison.OrdinalIgnoreCase));

        if (renderArgument == null)
        {
            return;
        }

        string outputPath = renderArgument["--render=".Length..].Trim('"');
        await Task.Delay(400);
        await Dispatcher.InvokeAsync(() =>
        {
            RenderWindow(outputPath);
            Close();
        }, DispatcherPriority.ApplicationIdle);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void StepButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string tag)
        {
            return;
        }

        string[] parts = tag.Split('|');
        if (parts.Length != 2 || FindName(parts[0]) is not TextBox textBox)
        {
            return;
        }

        if (!double.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
            !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double step))
        {
            return;
        }

        textBox.Text = Math.Max(0, value + step).ToString("0.##", CultureInfo.InvariantCulture);
    }

    private void SelectionButton_Click(object sender, RoutedEventArgs e)
    {
        SelectionPopup.IsOpen = !SelectionPopup.IsOpen;
    }

    private void SelectionPopup_Opened(object? sender, EventArgs e)
    {
        var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        SelectionPopupCard.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = easing
        });

        if (SelectionPopupCard.RenderTransform is TranslateTransform translate)
        {
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(7, 0, TimeSpan.FromMilliseconds(160))
            {
                EasingFunction = easing
            });
        }
    }

    private void SelectionOption_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string status)
        {
            SelectionStatusText.Text = status + ". Выбрано линий: 4; помещение: 101.";
            PrototypeStatusText.Text = "Выбор подготовлен · можно проверить остальные настройки";
            StatusDot.Fill = (Brush)FindResource("SabBrush.Success");
        }

        SelectionPopup.IsOpen = false;
    }

    private void ClearSelectionButton_Click(object sender, RoutedEventArgs e)
    {
        SelectionStatusText.Text = "Выберите линии детализации и помещение в активном плане.";
        PrototypeStatusText.Text = "Прототип: данные Revit не изменяются";
        StatusDot.Fill = (Brush)FindResource("SabBrush.Warning");
    }

    private void SettingsTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || SettingsTabs.SelectedContent is not FrameworkElement content)
        {
            return;
        }

        var translate = new TranslateTransform(0, 5);
        content.RenderTransform = translate;
        content.Opacity = 0;

        var easing = new QuadraticEase { EasingMode = EasingMode.EaseOut };
        content.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = easing
        });
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(5, 0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = easing
        });
    }

    private void CreatePrototypeButton_Click(object sender, RoutedEventArgs e)
    {
        PrototypeStatusText.Text = "Прототип проверен · команда создания намеренно не запускается";
        StatusDot.Fill = (Brush)FindResource("SabBrush.Success");
    }

    private void RenderWindow(string outputPath)
    {
        string? directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        const double dpi = 144;
        double scale = dpi / 96.0;
        int pixelWidth = Math.Max(1, (int)Math.Ceiling(ActualWidth * scale));
        int pixelHeight = Math.Max(1, (int)Math.Ceiling(ActualHeight * scale));

        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, dpi, dpi, PixelFormats.Pbgra32);
        bitmap.Render(this);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(outputPath);
        encoder.Save(stream);
    }
}
