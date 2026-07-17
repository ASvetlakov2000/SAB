using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using SAB.UI;
using SAB.ViewTemplateGraphics.Models;
using SAB.ViewTemplateGraphics.ViewModels;
using WinFormsColorDialog = System.Windows.Forms.ColorDialog;
using WinFormsDialogResult = System.Windows.Forms.DialogResult;
using DrawingColor = System.Drawing.Color;

namespace SAB.ViewTemplateGraphics.Views
{
    public partial class GraphicOverrideEditorWindow : Window
    {
        private readonly GraphicOverrideEditorViewModel _viewModel;

        public GraphicOverrideEditorWindow(GraphicOverrideEditorViewModel viewModel)
        {
            _viewModel = viewModel ?? throw new ArgumentNullException("viewModel");

            InitializeWindowFromXamlFile();
            DataContext = _viewModel;

            AddHandler(Button.ClickEvent, new RoutedEventHandler(Button_Click));
            AddHandler(UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(Window_PreviewMouseLeftButtonDown));
            Loaded += GraphicOverrideEditorWindow_Loaded;
            SourceInitialized += Window_SourceInitialized;
        }

        public GraphicOverrideData EditedData
        {
            get { return _viewModel.Data; }
        }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(GraphicOverrideEditorWindow).Assembly.Location);
            string xamlPath = Path.Combine(
                assemblyDirectory,
                "Cls_ViewTemplateGraphics",
                "Views",
                "GraphicOverrideEditorWindow.xaml");

            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл редактора графики не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                ParserContext parserContext = new ParserContext();
                parserContext.BaseUri = new Uri(xamlPath, UriKind.Absolute);

                Window loadedWindow = XamlReader.Load(stream, parserContext) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось загрузить GraphicOverrideEditorWindow.xaml.");
                }

                CopyWindowProperties(loadedWindow);
            }
        }

        private void CopyWindowProperties(Window loadedWindow)
        {
            Title = loadedWindow.Title;
            Width = loadedWindow.Width;
            Height = loadedWindow.Height;
            MinWidth = loadedWindow.MinWidth;
            MinHeight = loadedWindow.MinHeight;
            WindowStartupLocation = loadedWindow.WindowStartupLocation;
            ResizeMode = loadedWindow.ResizeMode;
            ShowInTaskbar = loadedWindow.ShowInTaskbar;
            Background = loadedWindow.Background;
            FontFamily = loadedWindow.FontFamily;
            FontSize = loadedWindow.FontSize;
            Resources = loadedWindow.Resources;
            Content = loadedWindow.Content;
        }

        private void GraphicOverrideEditorWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetPanelVisibility("ProjectionLinesPanel", _viewModel.ShowProjectionLines);
            SetPanelVisibility(
                "SurfacePanel",
                _viewModel.ShowSurfacePatterns && _viewModel.SupportsSurfacePatterns);
            SetPanelVisibility(
                "TransparencyPanel",
                _viewModel.ShowTransparency && _viewModel.SupportsTransparency);

            bool showCutLines = _viewModel.ShowCutLines && _viewModel.SupportsCut;
            bool showCutPatterns = _viewModel.ShowCutPatterns && _viewModel.SupportsCut;
            SetPanelVisibility("CutPanel", showCutLines || showCutPatterns);
            SetPanelVisibility("CutLineWeightPanel", showCutLines);
            SetPanelVisibility("CutLineColorPanel", showCutLines);
            SetPanelVisibility("CutLinePatternPanel", showCutLines);
            SetPanelVisibility("CutForegroundPatternPanel", showCutPatterns);
            SetPanelVisibility("CutBackgroundPatternPanel", showCutPatterns);

            SetPanelVisibility("AdditionalPanel", _viewModel.ShowAdditional);
            SetPanelVisibility(
                "DetailLevelPanel",
                _viewModel.ShowAdditional && _viewModel.SupportsDetailLevel);

            // Each graphics editor remembers its own dimensions because line and pattern panels
            // need substantially different working heights.
            Height = GetDefaultWindowHeight();
            WindowSizeSettingsService.Apply(this, GetWindowSizeSettingsKey());
        }

        private string GetWindowSizeSettingsKey()
        {
            return "ViewTemplateGraphics.GraphicOverrideEditor." + _viewModel.Section;
        }

        private double GetDefaultWindowHeight()
        {
            switch (_viewModel.Section)
            {
                case GraphicOverrideEditorSection.ProjectionLines:
                case GraphicOverrideEditorSection.CutLines:
                    return 460.0;
                case GraphicOverrideEditorSection.Transparency:
                    return 380.0;
                case GraphicOverrideEditorSection.SurfacePatterns:
                case GraphicOverrideEditorSection.CutPatterns:
                    return 700.0;
                default:
                    return 720.0;
            }
        }

        private void SetPanelVisibility(string panelName, bool isVisible)
        {
            FrameworkElement panel = FindVisualChildByName<FrameworkElement>(this, panelName);
            if (panel != null)
            {
                panel.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button button = FindParent<Button>(e.OriginalSource as DependencyObject);
            if (button == null)
            {
                return;
            }

            if (string.Equals(button.Name, "ApplyButton", StringComparison.Ordinal))
            {
                DialogResult = true;
                Close();
                e.Handled = true;
                return;
            }

            if (string.Equals(button.Name, "CancelButton", StringComparison.Ordinal))
            {
                DialogResult = false;
                Close();
                e.Handled = true;
                return;
            }

            string action = button.Tag as string;
            string propertyName = button.CommandParameter as string;
            if (string.IsNullOrWhiteSpace(action) || string.IsNullOrWhiteSpace(propertyName))
            {
                return;
            }

            if (string.Equals(action, "PickColor", StringComparison.Ordinal))
            {
                PickColor(propertyName);
                e.Handled = true;
            }
            else if (string.Equals(action, "ClearColor", StringComparison.Ordinal))
            {
                SetColorValue(propertyName, GraphicOverrideData.NoColorValue);
                e.Handled = true;
            }
        }

        private void PickColor(string propertyName)
        {
            int currentColorValue = GetColorValue(propertyName);
            using (WinFormsColorDialog dialog = new WinFormsColorDialog())
            {
                dialog.FullOpen = true;
                dialog.AnyColor = true;

                if (currentColorValue != GraphicOverrideData.NoColorValue)
                {
                    dialog.Color = DrawingColor.FromArgb(
                        (currentColorValue >> 16) & 255,
                        (currentColorValue >> 8) & 255,
                        currentColorValue & 255);
                }

                if (dialog.ShowDialog() != WinFormsDialogResult.OK)
                {
                    return;
                }

                int colorValue = (dialog.Color.R << 16) | (dialog.Color.G << 8) | dialog.Color.B;
                SetColorValue(propertyName, colorValue);
            }
        }

        private int GetColorValue(string propertyName)
        {
            GraphicOverrideData data = _viewModel.Data;
            switch (propertyName)
            {
                case "ProjectionLineColorValue":
                    return data.ProjectionLineColorValue;
                case "SurfaceForegroundPatternColorValue":
                    return data.SurfaceForegroundPatternColorValue;
                case "SurfaceBackgroundPatternColorValue":
                    return data.SurfaceBackgroundPatternColorValue;
                case "CutLineColorValue":
                    return data.CutLineColorValue;
                case "CutForegroundPatternColorValue":
                    return data.CutForegroundPatternColorValue;
                case "CutBackgroundPatternColorValue":
                    return data.CutBackgroundPatternColorValue;
                default:
                    throw new InvalidOperationException("Неизвестное свойство цвета: " + propertyName);
            }
        }

        private void SetColorValue(string propertyName, int colorValue)
        {
            GraphicOverrideData data = _viewModel.Data;
            switch (propertyName)
            {
                case "ProjectionLineColorValue":
                    data.ProjectionLineColorValue = colorValue;
                    break;
                case "SurfaceForegroundPatternColorValue":
                    data.SurfaceForegroundPatternColorValue = colorValue;
                    break;
                case "SurfaceBackgroundPatternColorValue":
                    data.SurfaceBackgroundPatternColorValue = colorValue;
                    break;
                case "CutLineColorValue":
                    data.CutLineColorValue = colorValue;
                    break;
                case "CutForegroundPatternColorValue":
                    data.CutForegroundPatternColorValue = colorValue;
                    break;
                case "CutBackgroundPatternColorValue":
                    data.CutBackgroundPatternColorValue = colorValue;
                    break;
                default:
                    throw new InvalidOperationException("Неизвестное свойство цвета: " + propertyName);
            }
        }

        private void Window_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            CheckBox checkBox = FindParent<CheckBox>(e.OriginalSource as DependencyObject);
            if (checkBox == null || !checkBox.IsThreeState)
            {
                return;
            }

            bool nextValue = checkBox.IsChecked != true;
            checkBox.SetCurrentValue(ToggleButton.IsCheckedProperty, (bool?)nextValue);
            BindingExpression bindingExpression = checkBox.GetBindingExpression(ToggleButton.IsCheckedProperty);
            if (bindingExpression != null)
            {
                bindingExpression.UpdateSource();
            }

            e.Handled = true;
        }

        private void Window_SourceInitialized(object sender, EventArgs e)
        {
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            if (source != null && source.CompositionTarget != null)
            {
                source.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
            }

            SourceInitialized -= Window_SourceInitialized;
        }

        private static T FindVisualChildByName<T>(DependencyObject parent, string name)
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

        private static T FindParent<T>(DependencyObject child)
            where T : DependencyObject
        {
            DependencyObject current = child;
            while (current != null)
            {
                T typedParent = current as T;
                if (typedParent != null)
                {
                    return typedParent;
                }

                current = GetParentObject(current);
            }

            return null;
        }

        private static DependencyObject GetParentObject(DependencyObject child)
        {
            ContentElement contentElement = child as ContentElement;
            if (contentElement != null)
            {
                DependencyObject contentParent = ContentOperations.GetParent(contentElement);
                if (contentParent != null)
                {
                    return contentParent;
                }

                FrameworkContentElement frameworkContentElement = contentElement as FrameworkContentElement;
                return frameworkContentElement != null ? frameworkContentElement.Parent : null;
            }

            return child != null ? VisualTreeHelper.GetParent(child) : null;
        }
    }
}
