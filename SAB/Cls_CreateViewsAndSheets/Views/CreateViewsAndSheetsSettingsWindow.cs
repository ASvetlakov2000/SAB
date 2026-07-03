using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using SAB.CreateViewsAndSheets.ViewModels;
using SAB.UI;

namespace SAB.CreateViewsAndSheets.Views
{
    public class CreateViewsAndSheetsSettingsWindow : Window
    {
        private readonly CreateViewsAndSheetsViewModel _viewModel;
        private FrameworkElement _manualViewportPlacementPanel;
        private ButtonBase _sourceSheetPlacementToggle;
        private ButtonBase _pointPlacementToggle;

        public CreateViewsAndSheetsSettingsWindow(CreateViewsAndSheetsViewModel viewModel)
        {
            if (viewModel == null)
            {
                throw new ArgumentNullException(nameof(viewModel));
            }

            _viewModel = viewModel;
            InitializeWindowFromXamlFile();
            DataContext = viewModel;
            _viewModel.RequestPointSelection += ViewModel_RequestPointSelection;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            Loaded += CreateViewsAndSheetsSettingsWindow_Loaded;
            Closed += CreateViewsAndSheetsSettingsWindow_Closed;
        }

        public PlacementPointSelectionRequestEventArgs PendingPointSelectionRequest { get; private set; }

        private void ViewModel_RequestPointSelection(object sender, PlacementPointSelectionRequestEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            PendingPointSelectionRequest = e;
            DialogResult = false;
            Close();
        }

        private void CreateViewsAndSheetsSettingsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            AttachPlacementModeAnimationTargets();
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e == null)
            {
                return;
            }

            if (string.Equals(e.PropertyName, "UseSourceSheetViewportPlacement", StringComparison.Ordinal))
            {
                ApplyPlacementModeAnimation(true);
            }
        }

        private void CreateViewsAndSheetsSettingsWindow_Closed(object sender, EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.RequestPointSelection -= ViewModel_RequestPointSelection;
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            Loaded -= CreateViewsAndSheetsSettingsWindow_Loaded;
            Closed -= CreateViewsAndSheetsSettingsWindow_Closed;
            _manualViewportPlacementPanel = null;
            _sourceSheetPlacementToggle = null;
            _pointPlacementToggle = null;
        }

        private void AttachPlacementModeAnimationTargets()
        {
            _manualViewportPlacementPanel = FindVisualChildByName<FrameworkElement>(this, "ManualViewportPlacementPanel");
            _sourceSheetPlacementToggle = FindVisualChildByName<ButtonBase>(this, "SourceSheetPlacementToggle");
            _pointPlacementToggle = FindVisualChildByName<ButtonBase>(this, "PointPlacementToggle");
            ApplyPlacementModeAnimation(false);
        }

        private void ApplyPlacementModeAnimation(bool animate)
        {
            if (_viewModel == null)
            {
                return;
            }

            SabWindowAnimationService.AnimatePlacementModePanel(
                _manualViewportPlacementPanel,
                _viewModel.UseSourceSheetViewportPlacement,
                animate);

            if (animate)
            {
                ButtonBase activePlacementToggle = _viewModel.UseSourceSheetViewportPlacement
                    ? _sourceSheetPlacementToggle
                    : _pointPlacementToggle;
                SabWindowAnimationService.PulseButton(activePlacementToggle);
            }
        }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(CreateViewsAndSheetsSettingsWindow).Assembly.Location);
            string xamlPath = Path.Combine(assemblyDirectory, "Cls_CreateViewsAndSheets", "Views", "CreateViewsAndSheetsSettingsWindow.xaml");

            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл окна настроек создания видов и листов не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                ParserContext parserContext = new ParserContext();
                parserContext.BaseUri = new Uri(xamlPath, UriKind.Absolute);

                Window loadedWindow = XamlReader.Load(stream, parserContext) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось загрузить CreateViewsAndSheetsSettingsWindow.xaml.");
                }

                Title = loadedWindow.Title;
                Width = loadedWindow.Width;
                Height = loadedWindow.Height;
                MinWidth = loadedWindow.MinWidth;
                MinHeight = loadedWindow.MinHeight;
                WindowStartupLocation = loadedWindow.WindowStartupLocation;
                ResizeMode = loadedWindow.ResizeMode;
                Style = loadedWindow.Style;
                Background = loadedWindow.Background;
                FontFamily = loadedWindow.FontFamily;
                FontSize = loadedWindow.FontSize;
                FontWeight = loadedWindow.FontWeight;
                Resources = loadedWindow.Resources;
                Content = loadedWindow.Content;

                WindowSizeSettingsService.Apply(this, "CreateViewsAndSheets.SettingsWindow");
            }
        }

        private T FindVisualChildByName<T>(DependencyObject parent, string name)
            where T : FrameworkElement
        {
            if (parent == null)
            {
                return null;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                T typedChild = child as T;
                if (typedChild != null && string.Equals(typedChild.Name, name, StringComparison.Ordinal))
                {
                    return typedChild;
                }

                T nestedChild = FindVisualChildByName<T>(child, name);
                if (nestedChild != null)
                {
                    return nestedChild;
                }
            }

            return null;
        }
    }
}
