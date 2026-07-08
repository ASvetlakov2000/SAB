using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Threading;
using SAB.CreateViewsAndSheets.Models;
using SAB.UI;

namespace SAB.CreateViewsAndSheets.Views
{
    public class CreateViewsAndSheetsProgressWindow : Window, IProgress<CreateViewsAndSheetsProgressInfo>
    {
        private readonly List<string> _rotatingMessages;

        private TextBlock _stepTextBlock;
        private TextBlock _percentTextBlock;
        private TextBlock _rotatingMessageTextBlock;
        private Border _rotatingMessageBorder;
        private ProgressBar _mainProgressBar;
        private DispatcherTimer _rotatingMessageTimer;
        private int _rotatingMessageIndex;
        private bool _allowClose;

        public CreateViewsAndSheetsProgressWindow()
            : this(null)
        {
        }

        public CreateViewsAndSheetsProgressWindow(IList<string> rotatingMessages)
        {
            _rotatingMessages = BuildRotatingMessages(rotatingMessages);

            InitializeWindowFromXamlFile();
            Closing += CreateViewsAndSheetsProgressWindow_Closing;
            Closed += CreateViewsAndSheetsProgressWindow_Closed;
            Loaded += CreateViewsAndSheetsProgressWindow_Loaded;
        }

        public void Report(CreateViewsAndSheetsProgressInfo value)
        {
            if (value == null)
            {
                return;
            }

            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(
                    new Action(delegate
                    {
                        ApplyProgress(value);
                        UpdateLayout();
                    }),
                    DispatcherPriority.Send);
                return;
            }

            ApplyProgress(value);
            UpdateLayout();
            RefreshWindow();
        }

        public void AllowCloseAndClose()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(new Action(AllowCloseAndClose));
                return;
            }

            _allowClose = true;
            StopRotatingMessages();
            Close();
        }

        private List<string> BuildRotatingMessages(IList<string> rotatingMessages)
        {
            List<string> messages = new List<string>();
            if (rotatingMessages != null)
            {
                for (int i = 0; i < rotatingMessages.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(rotatingMessages[i]))
                    {
                        messages.Add(rotatingMessages[i]);
                    }
                }
            }

            return messages;
        }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(CreateViewsAndSheetsProgressWindow).Assembly.Location);
            string xamlPath = Path.Combine(assemblyDirectory, "Cls_CreateViewsAndSheets", "Views", "CreateViewsAndSheetsProgressWindow.xaml");

            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл окна прогресса не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                ParserContext parserContext = new ParserContext();
                parserContext.BaseUri = new Uri(xamlPath, UriKind.Absolute);

                Window loadedWindow = XamlReader.Load(stream, parserContext) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось загрузить CreateViewsAndSheetsProgressWindow.xaml.");
                }

                _stepTextBlock = loadedWindow.FindName("StepTextBlock") as TextBlock;
                _percentTextBlock = loadedWindow.FindName("PercentTextBlock") as TextBlock;
                _rotatingMessageTextBlock = loadedWindow.FindName("RotatingMessageTextBlock") as TextBlock;
                _rotatingMessageBorder = loadedWindow.FindName("RotatingMessageBorder") as Border;
                _mainProgressBar = loadedWindow.FindName("MainProgressBar") as ProgressBar;

                Title = loadedWindow.Title;
                Width = loadedWindow.Width;
                Height = loadedWindow.Height;
                MinWidth = loadedWindow.MinWidth;
                MinHeight = loadedWindow.MinHeight;
                SizeToContent = loadedWindow.SizeToContent;
                WindowStartupLocation = loadedWindow.WindowStartupLocation;
                ResizeMode = loadedWindow.ResizeMode;
                Style = loadedWindow.Style;
                Background = loadedWindow.Background;
                FontFamily = loadedWindow.FontFamily;
                FontSize = loadedWindow.FontSize;
                FontWeight = loadedWindow.FontWeight;
                Resources = loadedWindow.Resources;
                Content = loadedWindow.Content;
            }

            FindControlsIfNeeded();
            ApplyStaticVisualState();
        }

        private void FindControlsIfNeeded()
        {
            DependencyObject root = Content as DependencyObject;
            if (_stepTextBlock == null)
            {
                _stepTextBlock = FindElementByName<TextBlock>(root, "StepTextBlock");
            }

            if (_percentTextBlock == null)
            {
                _percentTextBlock = FindElementByName<TextBlock>(root, "PercentTextBlock");
            }

            if (_rotatingMessageTextBlock == null)
            {
                _rotatingMessageTextBlock = FindElementByName<TextBlock>(root, "RotatingMessageTextBlock");
            }

            if (_rotatingMessageBorder == null)
            {
                _rotatingMessageBorder = FindElementByName<Border>(root, "RotatingMessageBorder");
            }

            if (_mainProgressBar == null)
            {
                _mainProgressBar = FindElementByName<ProgressBar>(root, "MainProgressBar");
            }
        }

        private void ApplyStaticVisualState()
        {
            if (_rotatingMessageBorder != null)
            {
                _rotatingMessageBorder.Visibility = _rotatingMessages.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            ApplyRotatingMessage();
        }

        private void ApplyProgress(CreateViewsAndSheetsProgressInfo progressInfo)
        {
            int totalSteps = progressInfo.TotalSteps > 0 ? progressInfo.TotalSteps : 1;
            int currentStep = Clamp(progressInfo.CurrentStep, 0, totalSteps);
            double percent = totalSteps > 0 ? (double)currentStep / totalSteps * 100.0 : 0.0;

            if (_mainProgressBar != null)
            {
                _mainProgressBar.Value = percent;
            }

            if (_stepTextBlock != null)
            {
                _stepTextBlock.Text = "Шаг " + currentStep + " из " + totalSteps;
            }

            if (_percentTextBlock != null)
            {
                _percentTextBlock.Text = Math.Round(percent).ToString("0") + "%";
            }
        }

        private void StartRotatingMessages()
        {
            ApplyRotatingMessage();
            if (_rotatingMessages.Count <= 1 || _rotatingMessageTimer != null)
            {
                return;
            }

            _rotatingMessageTimer = new DispatcherTimer();
            _rotatingMessageTimer.Interval = TimeSpan.FromSeconds(3);
            _rotatingMessageTimer.Tick += RotatingMessageTimer_Tick;
            _rotatingMessageTimer.Start();
        }

        private void StopRotatingMessages()
        {
            if (_rotatingMessageTimer == null)
            {
                return;
            }

            _rotatingMessageTimer.Stop();
            _rotatingMessageTimer.Tick -= RotatingMessageTimer_Tick;
            _rotatingMessageTimer = null;
        }

        private void RotatingMessageTimer_Tick(object sender, EventArgs e)
        {
            if (_rotatingMessages.Count == 0)
            {
                return;
            }

            _rotatingMessageIndex++;
            if (_rotatingMessageIndex >= _rotatingMessages.Count)
            {
                _rotatingMessageIndex = 0;
            }

            ApplyRotatingMessage();
        }

        private void ApplyRotatingMessage()
        {
            if (_rotatingMessageTextBlock == null || _rotatingMessages.Count == 0)
            {
                return;
            }

            if (_rotatingMessageIndex < 0 || _rotatingMessageIndex >= _rotatingMessages.Count)
            {
                _rotatingMessageIndex = 0;
            }

            _rotatingMessageTextBlock.Text = _rotatingMessages[_rotatingMessageIndex];
        }

        private int Clamp(int value, int minimum, int maximum)
        {
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

        private void RefreshWindow()
        {
            Dispatcher.Invoke(
                DispatcherPriority.Background,
                new Action(delegate { }));
        }

        private void CreateViewsAndSheetsProgressWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SabWindowBehaviorService.ApplyLoadedBehavior(this);
            StartRotatingMessages();
        }

        private void CreateViewsAndSheetsProgressWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_allowClose)
            {
                e.Cancel = true;
            }
        }

        private void CreateViewsAndSheetsProgressWindow_Closed(object sender, EventArgs e)
        {
            StopRotatingMessages();
            Closing -= CreateViewsAndSheetsProgressWindow_Closing;
            Closed -= CreateViewsAndSheetsProgressWindow_Closed;
            Loaded -= CreateViewsAndSheetsProgressWindow_Loaded;
        }

        private T FindElementByName<T>(DependencyObject root, string name)
            where T : FrameworkElement
        {
            if (root == null)
            {
                return null;
            }

            FrameworkElement frameworkElement = root as FrameworkElement;
            if (frameworkElement != null && string.Equals(frameworkElement.Name, name, StringComparison.Ordinal))
            {
                return frameworkElement as T;
            }

            foreach (object childObject in LogicalTreeHelper.GetChildren(root))
            {
                DependencyObject childDependencyObject = childObject as DependencyObject;
                if (childDependencyObject == null)
                {
                    continue;
                }

                T nestedChild = FindElementByName<T>(childDependencyObject, name);
                if (nestedChild != null)
                {
                    return nestedChild;
                }
            }

            return null;
        }
    }
}
