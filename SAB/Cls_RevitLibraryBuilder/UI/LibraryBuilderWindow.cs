using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Win32;
using RevitLibraryBuilder.Models;
using SAB.BimDashboard.Models;
using SAB.UI;
using Forms = System.Windows.Forms;

namespace SAB.Cls_RevitLibraryBuilder.UI
{
    /// <summary>
    /// Unified entry window for Revit Library Builder operations.
    /// The window only creates a request; Revit API work begins after it closes.
    /// </summary>
    public class LibraryBuilderWindow : Window
    {
        private const string WindowSettingsKey = "RevitLibraryBuilder.LibraryBuilderWindow";

        private readonly string _documentTitle;
        private readonly string _activeViewName;
        private readonly int _selectedElementsCount;
        private readonly bool _isLegendView;
        private readonly bool _isDraftingView;
        private readonly Dictionary<string, Button> _modeButtons;
        private readonly Dictionary<string, FrameworkElement> _modePanels;
        private readonly Dictionary<string, string> _defaultOperationButtonNames;
        private readonly Dictionary<Button, LibraryOperationInfo> _operationsByButton;
        private readonly List<Button> _operationButtons;
        private readonly List<string> _viewerCsvFilePaths;

        private Button _selectedModeButton;
        private Button _selectedOperationButton;
        private LibraryOperationInfo _selectedOperation;
        private bool _isViewerModeSelected;
        private string _systemImagesFolder;
        private string _loadableImagesFolder;
        private string _lineImagesFolder;
        private string _fillImagesFolder;
        private TextBlock _documentContextTextBlock;
        private TextBlock _activeViewContextTextBlock;
        private TextBlock _selectionContextTextBlock;
        private TextBlock _selectedOperationRequirementTextBlock;
        private TextBlock _viewerCsvFilesTextBlock;
        private TextBlock _systemImagesFolderTextBlock;
        private TextBlock _loadableImagesFolderTextBlock;
        private TextBlock _lineImagesFolderTextBlock;
        private TextBlock _fillImagesFolderTextBlock;
        private TextBlock _viewerCompletenessTextBlock;
        private TextBlock _viewerStatusTextBlock;
        private ProgressBar _viewerCompletenessProgressBar;
        private Button _closeButton;
        private Button _runButton;
        private Button _selectViewerCsvFilesButton;
        private Button _clearViewerCsvFilesButton;
        private Button _selectSystemImagesFolderButton;
        private Button _selectLoadableImagesFolderButton;
        private Button _selectLineImagesFolderButton;
        private Button _selectFillImagesFolderButton;
        private Button _clearViewerImageFoldersButton;

        public LibraryBuilderWindow(
            string documentTitle,
            string activeViewName,
            int selectedElementsCount,
            bool isLegendView,
            bool isDraftingView)
        {
            _documentTitle = string.IsNullOrWhiteSpace(documentTitle) ? "Без имени" : documentTitle.Trim();
            _activeViewName = string.IsNullOrWhiteSpace(activeViewName) ? "Вид недоступен" : activeViewName.Trim();
            _selectedElementsCount = Math.Max(0, selectedElementsCount);
            _isLegendView = isLegendView;
            _isDraftingView = isDraftingView;
            _modeButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
            _modePanels = new Dictionary<string, FrameworkElement>(StringComparer.Ordinal);
            _defaultOperationButtonNames = BuildDefaultOperationButtonNames();
            _operationsByButton = new Dictionary<Button, LibraryOperationInfo>();
            _operationButtons = new List<Button>();
            _viewerCsvFilePaths = new List<string>();
            _systemImagesFolder = string.Empty;
            _loadableImagesFolder = string.Empty;
            _lineImagesFolder = string.Empty;
            _fillImagesFolder = string.Empty;

            InitializeWindowFromXamlFile();

            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            SourceInitialized += LibraryBuilderWindow_SourceInitialized;
            Loaded += LibraryBuilderWindow_Loaded;
            Closed += LibraryBuilderWindow_Closed;

            WindowSizeSettingsService.Apply(this, WindowSettingsKey);
        }

        public LibraryToolId? SelectedToolId { get; private set; }

        public DashboardLaunchRequest DashboardRequest { get; private set; }

        private void LibraryBuilderWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ResolveControls();
            FillRevitContext();
            AttachModeHandlers();
            AttachOperationHandlers();
            AttachViewerHandlers();
            UpdateViewerState();
            SelectMode("Catalogs");
        }

        private void LibraryBuilderWindow_Closed(object sender, EventArgs e)
        {
            DetachHandlers();
            SourceInitialized -= LibraryBuilderWindow_SourceInitialized;
            Loaded -= LibraryBuilderWindow_Loaded;
            Closed -= LibraryBuilderWindow_Closed;
        }

        // Block responsible for stable WPF rendering inside Revit.
        private void LibraryBuilderWindow_SourceInitialized(object sender, EventArgs e)
        {
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;

            if (source != null && source.CompositionTarget != null)
            {
                source.CompositionTarget.RenderMode = RenderMode.SoftwareOnly;
            }

            SourceInitialized -= LibraryBuilderWindow_SourceInitialized;
        }

        private void ResolveControls()
        {
            _documentContextTextBlock = GetRequiredControl<TextBlock>("DocumentContextTextBlock");
            _activeViewContextTextBlock = GetRequiredControl<TextBlock>("ActiveViewContextTextBlock");
            _selectionContextTextBlock = GetRequiredControl<TextBlock>("SelectionContextTextBlock");
            _selectedOperationRequirementTextBlock = GetRequiredControl<TextBlock>("SelectedOperationRequirementTextBlock");
            _closeButton = GetRequiredControl<Button>("CloseButton");
            _runButton = GetRequiredControl<Button>("RunButton");

            _modeButtons.Add("Catalogs", GetRequiredControl<Button>("CatalogsModeButton"));
            _modeButtons.Add("Naming", GetRequiredControl<Button>("NamingModeButton"));
            _modeButtons.Add("Graphics", GetRequiredControl<Button>("GraphicsModeButton"));
            _modeButtons.Add("Placement", GetRequiredControl<Button>("PlacementModeButton"));
            _modeButtons.Add("Viewer", GetRequiredControl<Button>("ViewerModeButton"));

            _modePanels.Add("Catalogs", GetRequiredControl<FrameworkElement>("CatalogsModePanel"));
            _modePanels.Add("Naming", GetRequiredControl<FrameworkElement>("NamingModePanel"));
            _modePanels.Add("Graphics", GetRequiredControl<FrameworkElement>("GraphicsModePanel"));
            _modePanels.Add("Placement", GetRequiredControl<FrameworkElement>("PlacementModePanel"));
            _modePanels.Add("Viewer", GetRequiredControl<FrameworkElement>("ViewerModePanel"));

            _selectViewerCsvFilesButton = GetRequiredControl<Button>("SelectViewerCsvFilesButton");
            _clearViewerCsvFilesButton = GetRequiredControl<Button>("ClearViewerCsvFilesButton");
            _selectSystemImagesFolderButton = GetRequiredControl<Button>("SelectSystemImagesFolderButton");
            _selectLoadableImagesFolderButton = GetRequiredControl<Button>("SelectLoadableImagesFolderButton");
            _selectLineImagesFolderButton = GetRequiredControl<Button>("SelectLineImagesFolderButton");
            _selectFillImagesFolderButton = GetRequiredControl<Button>("SelectFillImagesFolderButton");
            _clearViewerImageFoldersButton = GetRequiredControl<Button>("ClearViewerImageFoldersButton");
            _viewerCsvFilesTextBlock = GetRequiredControl<TextBlock>("ViewerCsvFilesTextBlock");
            _systemImagesFolderTextBlock = GetRequiredControl<TextBlock>("SystemImagesFolderTextBlock");
            _loadableImagesFolderTextBlock = GetRequiredControl<TextBlock>("LoadableImagesFolderTextBlock");
            _lineImagesFolderTextBlock = GetRequiredControl<TextBlock>("LineImagesFolderTextBlock");
            _fillImagesFolderTextBlock = GetRequiredControl<TextBlock>("FillImagesFolderTextBlock");
            _viewerCompletenessTextBlock = GetRequiredControl<TextBlock>("ViewerCompletenessTextBlock");
            _viewerStatusTextBlock = GetRequiredControl<TextBlock>("ViewerStatusTextBlock");
            _viewerCompletenessProgressBar = GetRequiredControl<ProgressBar>("ViewerCompletenessProgressBar");
        }

        private void FillRevitContext()
        {
            _documentContextTextBlock.Text = "Документ: " + _documentTitle;
            _activeViewContextTextBlock.Text = "Активный вид: " + _activeViewName;
            _selectionContextTextBlock.Text = "Выбрано элементов: " + _selectedElementsCount;
        }

        private void AttachModeHandlers()
        {
            foreach (KeyValuePair<string, Button> pair in _modeButtons)
            {
                pair.Value.Tag = pair.Key;
                pair.Value.Click += ModeButton_Click;
            }

            _closeButton.Click += CloseButton_Click;
            _runButton.Click += RunButton_Click;
        }

        private void AttachOperationHandlers()
        {
            List<LibraryOperationInfo> operationInfos = BuildOperationInfos();

            for (int i = 0; i < operationInfos.Count; i++)
            {
                LibraryOperationInfo info = operationInfos[i];
                Button button = GetRequiredControl<Button>(info.ButtonName);

                button.Click += OperationButton_Click;
                button.ToolTip = CreateInstructionToolTip(info);
                ToolTipService.SetInitialShowDelay(button, 280);
                ToolTipService.SetBetweenShowDelay(button, 80);
                ToolTipService.SetShowDuration(button, 30000);

                _operationsByButton.Add(button, info);
                _operationButtons.Add(button);
            }
        }

        private void AttachViewerHandlers()
        {
            _selectViewerCsvFilesButton.Click += SelectViewerCsvFilesButton_Click;
            _clearViewerCsvFilesButton.Click += ClearViewerCsvFilesButton_Click;
            _selectSystemImagesFolderButton.Click += SelectSystemImagesFolderButton_Click;
            _selectLoadableImagesFolderButton.Click += SelectLoadableImagesFolderButton_Click;
            _selectLineImagesFolderButton.Click += SelectLineImagesFolderButton_Click;
            _selectFillImagesFolderButton.Click += SelectFillImagesFolderButton_Click;
            _clearViewerImageFoldersButton.Click += ClearViewerImageFoldersButton_Click;
        }

        private void DetachHandlers()
        {
            foreach (KeyValuePair<string, Button> pair in _modeButtons)
            {
                pair.Value.Click -= ModeButton_Click;
            }

            for (int i = 0; i < _operationButtons.Count; i++)
            {
                _operationButtons[i].Click -= OperationButton_Click;
                _operationButtons[i].ToolTip = null;
            }

            if (_closeButton != null)
            {
                _closeButton.Click -= CloseButton_Click;
            }

            if (_runButton != null)
            {
                _runButton.Click -= RunButton_Click;
            }

            if (_selectViewerCsvFilesButton != null)
            {
                _selectViewerCsvFilesButton.Click -= SelectViewerCsvFilesButton_Click;
                _clearViewerCsvFilesButton.Click -= ClearViewerCsvFilesButton_Click;
                _selectSystemImagesFolderButton.Click -= SelectSystemImagesFolderButton_Click;
                _selectLoadableImagesFolderButton.Click -= SelectLoadableImagesFolderButton_Click;
                _selectLineImagesFolderButton.Click -= SelectLineImagesFolderButton_Click;
                _selectFillImagesFolderButton.Click -= SelectFillImagesFolderButton_Click;
                _clearViewerImageFoldersButton.Click -= ClearViewerImageFoldersButton_Click;
            }

            _modeButtons.Clear();
            _modePanels.Clear();
            _operationsByButton.Clear();
            _operationButtons.Clear();
        }

        private void ModeButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;
            string modeName = button != null ? button.Tag as string : null;

            if (!string.IsNullOrWhiteSpace(modeName))
            {
                SelectMode(modeName);
            }
        }

        private void OperationButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = sender as Button;

            if (button == null || !_operationsByButton.ContainsKey(button))
            {
                return;
            }

            SelectOperation(button, _operationsByButton[button]);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedToolId = null;
            DashboardRequest = null;
            DialogResult = false;
            Close();
        }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isViewerModeSelected)
            {
                SelectedToolId = LibraryToolId.GenerateDashboard;
                DashboardRequest = BuildDashboardRequest();
                DialogResult = true;
                Close();
                return;
            }

            if (_selectedOperation == null || !_selectedOperation.IsAvailable)
            {
                return;
            }

            SelectedToolId = _selectedOperation.ToolId;
            DashboardRequest = null;
            DialogResult = true;
            Close();
        }

        private void SelectMode(string modeName)
        {
            if (!_modeButtons.ContainsKey(modeName) || !_modePanels.ContainsKey(modeName))
            {
                return;
            }

            foreach (KeyValuePair<string, FrameworkElement> pair in _modePanels)
            {
                pair.Value.Visibility = string.Equals(pair.Key, modeName, StringComparison.Ordinal)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }

            ApplySelectedModeVisual(_modeButtons[modeName]);

            if (string.Equals(modeName, "Viewer", StringComparison.Ordinal))
            {
                SelectViewerMode();
                return;
            }

            string defaultButtonName;

            if (_defaultOperationButtonNames.TryGetValue(modeName, out defaultButtonName))
            {
                Button defaultButton = GetRequiredControl<Button>(defaultButtonName);

                if (_operationsByButton.ContainsKey(defaultButton))
                {
                    SelectOperation(defaultButton, _operationsByButton[defaultButton]);
                }
            }
        }

        private void ApplySelectedModeVisual(Button button)
        {
            if (_selectedModeButton != null)
            {
                _selectedModeButton.ClearValue(Control.BackgroundProperty);
                _selectedModeButton.ClearValue(Control.BorderBrushProperty);
            }

            _selectedModeButton = button;
            _selectedModeButton.Background = GetBrushResource("SabBrush.AccentLight");
            _selectedModeButton.BorderBrush = GetBrushResource("SabBrush.Accent");
        }

        private void SelectOperation(Button button, LibraryOperationInfo info)
        {
            _isViewerModeSelected = false;
            _runButton.Content = "Запустить";

            if (_selectedOperationButton != null)
            {
                _selectedOperationButton.ClearValue(Control.BackgroundProperty);
                _selectedOperationButton.ClearValue(Control.BorderBrushProperty);
                _selectedOperationButton.ClearValue(Control.BorderThicknessProperty);
            }

            _selectedOperationButton = button;
            _selectedOperation = info;

            if (info.ToolId == LibraryToolId.DeleteSelectedTypesAndFamilies)
            {
                _selectedOperationButton.Background = new SolidColorBrush(Color.FromRgb(254, 243, 242));
                _selectedOperationButton.BorderBrush = GetBrushResource("SabBrush.Error");
            }
            else
            {
                _selectedOperationButton.Background = GetBrushResource("SabBrush.AccentLight");
                _selectedOperationButton.BorderBrush = GetBrushResource("SabBrush.Accent");
            }

            _selectedOperationButton.BorderThickness = new Thickness(2);

            if (info.IsAvailable)
            {
                _selectedOperationRequirementTextBlock.Text = "Перед запуском: " + info.Requirement;
                _selectedOperationRequirementTextBlock.Foreground = GetBrushResource("SabBrush.TextSecondary");
                _runButton.IsEnabled = true;
                _runButton.ToolTip = null;
            }
            else
            {
                _selectedOperationRequirementTextBlock.Text = "Сейчас недоступно: " + info.UnavailableReason;
                _selectedOperationRequirementTextBlock.Foreground = GetBrushResource("SabBrush.Error");
                _runButton.IsEnabled = false;
                _runButton.ToolTip = info.UnavailableReason;
            }
        }

        private void SelectViewerMode()
        {
            ClearSelectedOperationVisual();
            _isViewerModeSelected = true;
            _selectedOperation = null;
            _runButton.Content = "Открыть просмотрщик";
            _runButton.IsEnabled = true;
            _runButton.ToolTip = "Просмотрщик запускается при любом наборе CSV и PNG-папок.";
            UpdateViewerState();
        }

        private void ClearSelectedOperationVisual()
        {
            if (_selectedOperationButton == null)
            {
                return;
            }

            _selectedOperationButton.ClearValue(Control.BackgroundProperty);
            _selectedOperationButton.ClearValue(Control.BorderBrushProperty);
            _selectedOperationButton.ClearValue(Control.BorderThicknessProperty);
            _selectedOperationButton = null;
        }

        private void SelectViewerCsvFilesButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Title = "Выберите CSV-файлы для HTML-просмотрщика",
                Filter = "CSV (*.csv)|*.csv",
                Multiselect = true,
                CheckFileExists = true
            };

            if (dialog.ShowDialog(this) != true)
            {
                return;
            }

            for (int i = 0; i < dialog.FileNames.Length; i++)
            {
                AddUniqueViewerCsvPath(dialog.FileNames[i]);
            }

            UpdateViewerState();
        }

        private void ClearViewerCsvFilesButton_Click(object sender, RoutedEventArgs e)
        {
            _viewerCsvFilePaths.Clear();
            UpdateViewerState();
        }

        private void SelectSystemImagesFolderButton_Click(object sender, RoutedEventArgs e)
        {
            _systemImagesFolder = SelectFolder("Выберите папку PNG системных типов", _systemImagesFolder);
            UpdateViewerState();
        }

        private void SelectLoadableImagesFolderButton_Click(object sender, RoutedEventArgs e)
        {
            _loadableImagesFolder = SelectFolder("Выберите папку PNG загружаемых семейств", _loadableImagesFolder);
            UpdateViewerState();
        }

        private void SelectLineImagesFolderButton_Click(object sender, RoutedEventArgs e)
        {
            _lineImagesFolder = SelectFolder("Выберите папку PNG стилей линий", _lineImagesFolder);
            UpdateViewerState();
        }

        private void SelectFillImagesFolderButton_Click(object sender, RoutedEventArgs e)
        {
            _fillImagesFolder = SelectFolder("Выберите папку PNG штриховок", _fillImagesFolder);
            UpdateViewerState();
        }

        private void ClearViewerImageFoldersButton_Click(object sender, RoutedEventArgs e)
        {
            _systemImagesFolder = string.Empty;
            _loadableImagesFolder = string.Empty;
            _lineImagesFolder = string.Empty;
            _fillImagesFolder = string.Empty;
            UpdateViewerState();
        }

        private void AddUniqueViewerCsvPath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return;
            }

            string fullPath;

            try
            {
                fullPath = Path.GetFullPath(filePath);
            }
            catch
            {
                return;
            }

            for (int i = 0; i < _viewerCsvFilePaths.Count; i++)
            {
                if (string.Equals(_viewerCsvFilePaths[i], fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            _viewerCsvFilePaths.Add(fullPath);
        }

        private string SelectFolder(string title, string currentFolder)
        {
            using (Forms.FolderBrowserDialog dialog = new Forms.FolderBrowserDialog())
            {
                dialog.Description = title;
                dialog.ShowNewFolderButton = false;

                if (!string.IsNullOrWhiteSpace(currentFolder) && Directory.Exists(currentFolder))
                {
                    dialog.SelectedPath = currentFolder;
                }

                IntPtr ownerHandle = new WindowInteropHelper(this).Handle;
                Forms.DialogResult result = ownerHandle == IntPtr.Zero
                    ? dialog.ShowDialog()
                    : dialog.ShowDialog(new Win32Window(ownerHandle));

                if (result != Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    return currentFolder ?? string.Empty;
                }

                return dialog.SelectedPath;
            }
        }

        // Block responsible for progress and warnings while the viewer input is assembled.
        private void UpdateViewerState()
        {
            if (_viewerCompletenessProgressBar == null)
            {
                return;
            }

            _viewerCsvFilesTextBlock.Text = BuildViewerCsvSummary();
            _systemImagesFolderTextBlock.Text = BuildFolderDisplayText(_systemImagesFolder);
            _loadableImagesFolderTextBlock.Text = BuildFolderDisplayText(_loadableImagesFolder);
            _lineImagesFolderTextBlock.Text = BuildFolderDisplayText(_lineImagesFolder);
            _fillImagesFolderTextBlock.Text = BuildFolderDisplayText(_fillImagesFolder);

            const int expectedGroupsCount = 8;
            int loadedGroupsCount = GetLoadedViewerGroupsCount();
            int completenessPercent = Convert.ToInt32(
                Math.Round(loadedGroupsCount * 100.0 / expectedGroupsCount, MidpointRounding.AwayFromZero));
            _viewerCompletenessProgressBar.Value = completenessPercent;
            _viewerCompletenessTextBlock.Text = completenessPercent + "% — загружено групп: " + loadedGroupsCount + " из " + expectedGroupsCount;

            string statusText;
            Brush statusBrush;

            if (loadedGroupsCount == 0)
            {
                statusText = "Файлы не выбраны. Просмотрщик всё равно откроется и покажет страницу состояния.";
                statusBrush = GetBrushResource("SabBrush.Warning");
            }
            else if (loadedGroupsCount < expectedGroupsCount)
            {
                statusText = "Набор частичный. Просмотрщик запустится, но часть таблиц или изображений может отсутствовать.";
                statusBrush = GetBrushResource("SabBrush.Accent");
            }
            else
            {
                statusText = "Полный набор: четыре профиля CSV и четыре группы изображений загружены.";
                statusBrush = GetBrushResource("SabBrush.Success");
            }

            _viewerStatusTextBlock.Text = statusText;
            _viewerStatusTextBlock.Foreground = statusBrush;

            if (_isViewerModeSelected)
            {
                _selectedOperationRequirementTextBlock.Text = statusText;
                _selectedOperationRequirementTextBlock.Foreground = statusBrush;
                _runButton.IsEnabled = true;
            }
        }

        private string BuildViewerCsvSummary()
        {
            if (_viewerCsvFilePaths.Count == 0)
            {
                return "CSV-файлы не выбраны";
            }

            List<string> fileNames = new List<string>();

            for (int i = 0; i < _viewerCsvFilePaths.Count; i++)
            {
                fileNames.Add(Path.GetFileName(_viewerCsvFilePaths[i]));
            }

            int recognizedProfilesCount = GetSelectedViewerCsvProfilesCount();
            return "Выбрано CSV: " + _viewerCsvFilePaths.Count +
                   ". Распознано профилей: " + recognizedProfilesCount + " из 4\n" +
                   string.Join("; ", fileNames);
        }

        private static string BuildFolderDisplayText(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return "Папка не выбрана";
            }

            int pngCount = 0;

            try
            {
                pngCount = Directory.GetFiles(folderPath, "*.png", SearchOption.AllDirectories).Length;
            }
            catch
            {
                pngCount = 0;
            }

            return folderPath + "\nPNG-файлов: " + pngCount;
        }

        private int GetLoadedViewerGroupsCount()
        {
            int count = GetSelectedViewerCsvProfilesCount();
            count += Directory.Exists(_systemImagesFolder) ? 1 : 0;
            count += Directory.Exists(_loadableImagesFolder) ? 1 : 0;
            count += Directory.Exists(_lineImagesFolder) ? 1 : 0;
            count += Directory.Exists(_fillImagesFolder) ? 1 : 0;
            return count;
        }

        private int GetSelectedViewerCsvProfilesCount()
        {
            List<DashboardProfileType> profiles = new List<DashboardProfileType>();

            for (int i = 0; i < _viewerCsvFilePaths.Count; i++)
            {
                DashboardProfileType profile;

                if (!TryGetViewerCsvProfile(_viewerCsvFilePaths[i], out profile) || profiles.Contains(profile))
                {
                    continue;
                }

                profiles.Add(profile);
            }

            return profiles.Count;
        }

        // Block responsible for matching selected CSV files to the four supported viewer profiles.
        private static bool TryGetViewerCsvProfile(string filePath, out DashboardProfileType profile)
        {
            profile = DashboardProfileType.SystemFamilies;
            string fileName = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;

            if (fileName.IndexOf("Системные семейства", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                profile = DashboardProfileType.SystemFamilies;
                return true;
            }

            if (fileName.IndexOf("Загружаемые семейства", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                profile = DashboardProfileType.LoadableFamilies;
                return true;
            }

            if (fileName.IndexOf("Линии", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileName.IndexOf("LineStyles", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                profile = DashboardProfileType.Lines;
                return true;
            }

            if (fileName.IndexOf("Штриховки", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileName.IndexOf("FillPatterns", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                profile = DashboardProfileType.FillPatterns;
                return true;
            }

            return false;
        }

        private DashboardLaunchRequest BuildDashboardRequest()
        {
            DashboardLaunchRequest request = new DashboardLaunchRequest();

            for (int i = 0; i < _viewerCsvFilePaths.Count; i++)
            {
                request.CsvFilePaths.Add(_viewerCsvFilePaths[i]);
            }

            request.SystemFamilyImagesFolder = _systemImagesFolder;
            request.LoadableFamilyImagesFolder = _loadableImagesFolder;
            request.LineImagesFolder = _lineImagesFolder;
            request.FillImagesFolder = _fillImagesFolder;
            return request;
        }

        // Block responsible for the expanded hover instruction based on the plugin HTML manual.
        private ToolTip CreateInstructionToolTip(LibraryOperationInfo info)
        {
            StackPanel contentPanel = new StackPanel();

            TextBlock titleTextBlock = new TextBlock
            {
                Text = info.Title,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrushResource("SabBrush.Text"),
                TextWrapping = TextWrapping.Wrap
            };
            contentPanel.Children.Add(titleTextBlock);

            AddToolTipSection(contentPanel, "Для чего используется", info.Purpose, 8);
            AddToolTipSection(contentPanel, "Логика работы", info.Logic, 9);
            AddToolTipSection(contentPanel, "Как использовать", info.Instruction, 9);

            if (!info.IsAvailable)
            {
                TextBlock unavailableTextBlock = new TextBlock
                {
                    Text = "Сейчас недоступно: " + info.UnavailableReason,
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = GetBrushResource("SabBrush.Error"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 8, 0, 0)
                };
                contentPanel.Children.Add(unavailableTextBlock);
            }

            Border border = new Border
            {
                Background = GetBrushResource("SabBrush.PanelBackground"),
                BorderBrush = GetBrushResource("SabBrush.Border"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12),
                Child = contentPanel
            };

            return new ToolTip
            {
                Content = border,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0),
                MaxWidth = 460,
                Placement = PlacementMode.MousePoint,
                HorizontalOffset = 14,
                VerticalOffset = 12
            };
        }

        private void AddToolTipSection(StackPanel panel, string title, string text, double topMargin)
        {
            if (panel == null)
            {
                return;
            }

            TextBlock captionTextBlock = new TextBlock
            {
                Text = title,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = GetBrushResource("SabBrush.Accent"),
                Margin = new Thickness(0, topMargin, 0, 3)
            };
            panel.Children.Add(captionTextBlock);

            TextBlock contentTextBlock = new TextBlock
            {
                Text = text,
                FontSize = 12,
                Foreground = GetBrushResource("SabBrush.TextSecondary"),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 18
            };
            panel.Children.Add(contentTextBlock);
        }

        private Dictionary<string, string> BuildDefaultOperationButtonNames()
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.Ordinal);
            result.Add("Catalogs", "ExportSystemFamiliesButton");
            result.Add("Naming", "ExportTypeNamingButton");
            result.Add("Graphics", "ExportLineStylesButton");
            result.Add("Placement", "ImportByPointButton");
            return result;
        }

        private List<LibraryOperationInfo> BuildOperationInfos()
        {
            bool hasSelection = _selectedElementsCount > 0;

            return new List<LibraryOperationInfo>
            {
                CreateInfo(LibraryToolId.ExportSystemFamilies, "Catalogs", "ExportSystemFamiliesButton", "Экспорт системных семейств", "Создаёт CSV-каталоги системных типов по категориям.", "открыть проект и выбрать папку экспорта", "1. Откройте модель с нужными системными типами.\n2. Выберите корневую папку библиотеки.\n3. Проверьте CSV в ctg_system families."),
                CreateInfo(LibraryToolId.ExportLoadableFamilies, "Catalogs", "ExportLoadableFamiliesButton", "Экспорт загружаемых семейств", "Создаёт CSV-каталоги загружаемых семейств и типоразмеров.", "открыть проект и выбрать папку экспорта", "1. Откройте модель с нужными семействами.\n2. Выберите папку результата.\n3. Проверьте CSV в ctg_loadable families."),
                CreateInfo(LibraryToolId.ExportLoadableFamilyThumbnails, "Catalogs", "ExportLoadableThumbnailsButton", "PNG загружаемых семейств", "Выгружает изображения предпросмотра загружаемых семейств.", "открыть проект и выбрать папку экспорта", "1. Выгрузите CSV загружаемых семейств.\n2. Выберите папку для изображений.\n3. Проверьте, что имена PNG соответствуют строкам CSV."),

                CreateInfo(LibraryToolId.ExportTypeNaming, "Naming", "ExportTypeNamingButton", "Экспорт наименований типов", "Формирует XLSX для проверки и пакетного изменения имён типов и семейств.", "открыть проект и выбрать папку экспорта", "1. Выберите папку результата.\n2. Откройте XLSX в Excel или LibreOffice.\n3. Заполните новые имена, не меняя структуру таблицы."),
                CreateInfo(LibraryToolId.ImportTypeNaming, "Naming", "ImportTypeNamingButton", "Применить наименования типов", "Читает XLSX или CSV и применяет переименование, а также отмеченные удаления.", "подготовить и закрыть проверенный XLSX или CSV", "1. Проверьте новые имена и флаги удаления.\n2. Сохраните и закройте таблицу.\n3. Выберите файл и проверьте итоговый отчёт."),
                CreateInfo(LibraryToolId.ExportMaterialNaming, "Naming", "ExportMaterialNamingButton", "Экспорт наименований материалов", "Формирует XLSX со списком материалов и их описаниями.", "открыть проект и выбрать папку экспорта", "1. Выберите папку результата.\n2. Откройте таблицу материалов.\n3. Подготовьте новые имена, описания и служебные флаги."),
                CreateInfo(LibraryToolId.ImportMaterialNaming, "Naming", "ImportMaterialNamingButton", "Применить наименования материалов", "Обновляет имена и описания материалов, обрабатывает отмеченные удаления.", "подготовить и закрыть проверенный XLSX или CSV", "1. Проверьте имена, описания и флаги удаления.\n2. Сохраните и закройте таблицу.\n3. Выберите файл и проверьте отчёт проблемных строк."),

                CreateInfo(LibraryToolId.ExportLineStyles, "Graphics", "ExportLineStylesButton", "Экспорт CSV стилей линий", "Выгружает данные стилей линий для библиотечного каталога.", "открыть проект и выбрать папку экспорта", "1. Выберите корневую папку библиотеки.\n2. Дождитесь создания CSV в ctg_lines-patterns.\n3. Проверьте имена, цвета и веса линий."),
                CreateInfo(LibraryToolId.PlaceLineStyles, "Graphics", "PlaceLineStylesButton", "Разместить стили линий", "Создаёт или открывает вид «Библиотека_Стили линий» и размещает образцы.", "подготовить CSV стилей линий", "1. Подготовьте экспортированный CSV линий.\n2. Выберите файл.\n3. Проверьте образцы на виде Библиотека_Стили линий."),
                CreateInfo(LibraryToolId.ExportLineStylesPreviewPng, "Graphics", "ExportLineStylesPngButton", "Экспорт PNG стилей линий", "Создаёт изображения размещённых образцов линий.", "сделать активным чертёжный вид с размещёнными линиями", "1. Откройте чертёжный вид с образцами линий.\n2. Выберите корневую папку библиотеки.\n3. Проверьте изображения в PNG_Lines.", _isDraftingView, "активный вид должен быть чертёжным"),
                CreateInfo(LibraryToolId.ExportFillPatterns, "Graphics", "ExportFillPatternsButton", "Экспорт CSV штриховок", "Выгружает данные штриховок для библиотечного каталога.", "открыть проект и выбрать папку экспорта", "1. Выберите корневую папку библиотеки.\n2. Дождитесь создания CSV в ctg_lines-patterns.\n3. Проверьте имена и параметры штриховок."),
                CreateInfo(LibraryToolId.PlaceFillPatterns, "Graphics", "PlaceFillPatternsButton", "Разместить штриховки", "Создаёт или открывает вид «Библиотека_Штриховки» и размещает образцы.", "подготовить CSV штриховок", "1. Подготовьте экспортированный CSV штриховок.\n2. Выберите файл.\n3. Проверьте образцы на виде Библиотека_Штриховки."),
                CreateInfo(LibraryToolId.ExportFillPatternsPreviewPng, "Graphics", "ExportFillPatternsPngButton", "Экспорт PNG штриховок", "Создаёт изображения образцов с вида «Библиотека_Штриховки».", "убедиться, что библиотечный вид со штриховками создан", "1. Проверьте вид Библиотека_Штриховки.\n2. Выберите корневую папку библиотеки.\n3. Проверьте изображения в PNG_Fills."),
                CreateInfo(LibraryToolId.PlaceLegendComponentsByCategories, "Graphics", "PlaceLegendComponentsButton", "Разместить компоненты легенды", "Расставляет компоненты системных типов по поддерживаемым категориям.", "открыть активный вид типа «Легенда»", "1. Откройте вид категории Легенда.\n2. Запустите размещение компонентов.\n3. Проверьте, что компоненты читаются и не перекрываются.", _isLegendView, "активный вид должен иметь тип «Легенда»"),
                CreateInfo(LibraryToolId.ExportSystemFamilyThumbnailTemplate, "Graphics", "ExportSystemThumbnailsButton", "Экспорт PNG системных типов", "Выгружает изображения компонентов легенды для системных типов.", "открыть заполненный вид типа «Легенда» и выбрать папку", "1. Откройте подготовленную легенду.\n2. Выберите корневую папку библиотеки.\n3. Проверьте PNG_Pirogi: имена файлов должны совпадать с типами.", _isLegendView, "активный вид должен иметь тип «Легенда»"),
                CreateInfo(LibraryToolId.LoadSystemFamilyTypeImages, "Graphics", "LoadSystemImagesButton", "Загрузить изображения системных типов", "Назначает подготовленные PNG параметру изображения типоразмера.", "подготовить папку PNG с именами, совпадающими с типами", "1. Сначала синхронизируйте имена типов и PNG.\n2. Выберите папку PNG_Pirogi.\n3. Проверьте итог и параметр Изображение типоразмера."),

                CreateInfo(LibraryToolId.ImportByPoint, "Placement", "ImportByPointButton", "Размещение по точке", "Создаёт экземпляры точечных семейств из библиотечного CSV.", "подготовить CSV и убедиться, что в проекте есть уровень", "1. Подготовьте CSV нужной категории.\n2. Выберите файл.\n3. Проверьте размещённые экземпляры и созданный итоговый вид."),
                CreateInfo(LibraryToolId.ImportByLine, "Placement", "ImportByLineButton", "Размещение по линии", "Создаёт линейные экземпляры семейств из библиотечного CSV.", "подготовить CSV и убедиться, что в проекте есть уровень", "1. Подготовьте CSV линейной категории.\n2. Выберите файл.\n3. Проверьте элементы, размещённые вдоль линии."),
                CreateInfo(LibraryToolId.ImportByBoundary, "Placement", "ImportByBoundaryButton", "Размещение по границе", "Создаёт элементы по замкнутому контуру из библиотечного CSV.", "подготовить CSV и убедиться, что в проекте есть уровень", "1. Подготовьте CSV категории с контуром.\n2. Выберите файл.\n3. Проверьте созданные элементы и их границы."),
                CreateInfo(LibraryToolId.DeleteSelectedTypesAndFamilies, "Placement", "DeleteSelectedButton", "Удалить выбранные типы и семейства", "Удаляет выбранные экземпляры и связанные типы, семейства, линии или штриховки.", "выбрать элементы и проверить зависимости перед подтверждением", "1. Выберите только нужные экземпляры или образцы.\n2. Учтите связанные типы, семейства, линии и штриховки.\n3. Подтвердите удаление и проверьте итоговые ошибки.", hasSelection, "в активном виде нет выбранных элементов")
            };
        }

        private static LibraryOperationInfo CreateInfo(
            LibraryToolId toolId,
            string modeName,
            string buttonName,
            string title,
            string description,
            string requirement,
            string instruction)
        {
            return CreateInfo(toolId, modeName, buttonName, title, description, requirement, instruction, true, string.Empty);
        }

        private static LibraryOperationInfo CreateInfo(
            LibraryToolId toolId,
            string modeName,
            string buttonName,
            string title,
            string description,
            string requirement,
            string instruction,
            bool isAvailable,
            string unavailableReason)
        {
            return new LibraryOperationInfo(
                toolId,
                modeName,
                buttonName,
                title,
                description,
                requirement,
                instruction,
                isAvailable,
                unavailableReason);
        }

        // Block responsible for loading XAML in the classic project format.
        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(LibraryBuilderWindow).Assembly.Location);
            string xamlPath = Path.Combine(
                assemblyDirectory,
                "Cls_RevitLibraryBuilder",
                "UI",
                "LibraryBuilderWindow.xaml");

            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл окна библиотеки не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                ParserContext parserContext = new ParserContext();
                parserContext.BaseUri = new Uri(xamlPath, UriKind.Absolute);

                Window loadedWindow = XamlReader.Load(stream, parserContext) as Window;

                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось загрузить LibraryBuilderWindow.xaml.");
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
            }
        }

        private Brush GetBrushResource(string resourceKey)
        {
            Brush brush = FindResource(resourceKey) as Brush;
            return brush ?? Brushes.Transparent;
        }

        private T GetRequiredControl<T>(string controlName)
            where T : FrameworkElement
        {
            T control = FindVisualChildByName<T>(this, controlName);

            if (control == null)
            {
                throw new InvalidOperationException("В окне библиотеки не найден элемент: " + controlName);
            }

            return control;
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

        private static string BuildPurpose(LibraryToolId toolId, string baseDescription)
        {
            string safeBaseDescription = baseDescription ?? string.Empty;

            switch (toolId)
            {
                case LibraryToolId.ExportSystemFamilies:
                    return safeBaseDescription + " Используется для подготовки каталога стен, полов, кровель, потолков и других системных типов с их структурой и толщиной.";
                case LibraryToolId.ExportLoadableFamilies:
                    return safeBaseDescription + " Нужен для инвентаризации загружаемых семейств, последующего просмотра, переименования и размещения из библиотечных CSV.";
                case LibraryToolId.ExportLoadableFamilyThumbnails:
                    return safeBaseDescription + " Миниатюры связываются со строками CSV и делают карточки семейств наглядными в HTML-просмотрщике.";
                case LibraryToolId.ExportTypeNaming:
                    return safeBaseDescription + " Таблица позволяет проверить структуру имён вне Revit и подготовить пакетное переименование или удаление типов.";
                case LibraryToolId.ImportTypeNaming:
                    return safeBaseDescription + " Используется после ручной проверки экспортированной таблицы, чтобы массово синхронизировать имена в модели.";
                case LibraryToolId.ExportMaterialNaming:
                    return safeBaseDescription + " Таблица нужна для поиска дублей, проверки стандартов и подготовки описаний материалов вне Revit.";
                case LibraryToolId.ImportMaterialNaming:
                    return safeBaseDescription + " Команда возвращает подготовленные имена и описания в проект и обрабатывает служебные признаки удаления.";
                case LibraryToolId.ExportLineStyles:
                    return safeBaseDescription + " CSV используется для проверки параметров линий, размещения образцов и формирования профильного HTML-каталога.";
                case LibraryToolId.PlaceLineStyles:
                    return safeBaseDescription + " Наглядный вид позволяет визуально проверить цвет, вес и читаемость каждого стиля перед экспортом PNG.";
                case LibraryToolId.ExportLineStylesPreviewPng:
                    return safeBaseDescription + " PNG связываются с CSV линий и отображаются как визуальные образцы в просмотрщике.";
                case LibraryToolId.ExportFillPatterns:
                    return safeBaseDescription + " CSV используется для проверки имён и параметров штриховок, размещения образцов и HTML-каталога.";
                case LibraryToolId.PlaceFillPatterns:
                    return safeBaseDescription + " Вид с заполненными областями нужен для визуального контроля масштаба, плотности и читаемости штриховок.";
                case LibraryToolId.ExportFillPatternsPreviewPng:
                    return safeBaseDescription + " PNG добавляют визуальные превью к строкам штриховок в HTML-просмотрщике.";
                case LibraryToolId.PlaceLegendComponentsByCategories:
                    return safeBaseDescription + " Подготовленная легенда служит основой для получения изображений слоёв системных типов.";
                case LibraryToolId.ExportSystemFamilyThumbnailTemplate:
                    return safeBaseDescription + " Полученные «пироги» применяются в HTML-каталоге и могут быть записаны в параметр изображения типоразмера.";
                case LibraryToolId.LoadSystemFamilyTypeImages:
                    return safeBaseDescription + " Это завершает цепочку подготовки системных типов и делает изображения доступными непосредственно в Revit.";
                case LibraryToolId.ImportByPoint:
                    return safeBaseDescription + " Подходит для мебели, оборудования, дверей, окон и других объектов, размещаемых одной точкой.";
                case LibraryToolId.ImportByLine:
                    return safeBaseDescription + " Подходит для линейных семейств, длина и направление которых задаются библиотечными данными.";
                case LibraryToolId.ImportByBoundary:
                    return safeBaseDescription + " Подходит для элементов, геометрия которых определяется замкнутым контуром или набором границ.";
                case LibraryToolId.DeleteSelectedTypesAndFamilies:
                    return safeBaseDescription + " Используется для контролируемой очистки тестовых размещений и связанных библиотечных определений.";
                default:
                    return safeBaseDescription;
            }
        }

        private static string BuildLogic(LibraryToolId toolId)
        {
            switch (toolId)
            {
                case LibraryToolId.ExportSystemFamilies:
                case LibraryToolId.ExportLoadableFamilies:
                    return "Команда читает доступные типы текущего документа, группирует их по категориям и создаёт профильные CSV. Модель Revit при этом не изменяется.";
                case LibraryToolId.ExportLoadableFamilyThumbnails:
                    return "Для каждого поддерживаемого загружаемого типа формируется изображение предпросмотра. Путь к папке сохраняется в текущем сеансе, чтобы просмотрщик мог сопоставить PNG по имени семейства и типа.";
                case LibraryToolId.ExportTypeNaming:
                case LibraryToolId.ExportMaterialNaming:
                    return "Команда собирает текущие значения из документа и формирует XLSX с рабочими и служебными колонками. Изменения в Revit начинаются только при последующем импорте этой таблицы.";
                case LibraryToolId.ImportTypeNaming:
                    return "Строки таблицы сопоставляются с типами и семействами проекта. Каждая строка обрабатывается отдельной транзакцией: переименование или отмеченное удаление фиксируется независимо, а ошибки попадают в отчёт.";
                case LibraryToolId.ImportMaterialNaming:
                    return "Строки сопоставляются с материалами по исходным данным. Для каждой строки команда меняет имя, описание или выполняет отмеченное удаление и сохраняет проблемные позиции в отчёт.";
                case LibraryToolId.ExportLineStyles:
                case LibraryToolId.ExportFillPatterns:
                    return "Команда читает графические настройки проекта и записывает их в CSV папки ctg_lines-patterns. Файлы становятся источником для размещения образцов и HTML-просмотра.";
                case LibraryToolId.PlaceLineStyles:
                    return "Команда создаёт или повторно использует специальный чертёжный вид, переключает его активным и размещает по CSV подписанные образцы линий в одной транзакции.";
                case LibraryToolId.PlaceFillPatterns:
                    return "Команда создаёт или повторно использует специальный чертёжный вид, создаёт необходимые типы заполненных областей и размещает по CSV подписанные образцы штриховок.";
                case LibraryToolId.ExportLineStylesPreviewPng:
                    return "На активном чертёжном виде находятся размещённые линии. Каждый образец экспортируется в отдельный PNG, а папка регистрируется для автоматического поиска превью.";
                case LibraryToolId.ExportFillPatternsPreviewPng:
                    return "Команда находит вид Библиотека_Штриховки и экспортирует размещённые заполненные области в отдельные PNG. Папка регистрируется для HTML-просмотрщика.";
                case LibraryToolId.PlaceLegendComponentsByCategories:
                    return "На активной легенде собираются поддерживаемые системные типы и размещаются легенд-компоненты по категориям. Изменения выполняются внутри отдельной транзакции.";
                case LibraryToolId.ExportSystemFamilyThumbnailTemplate:
                    return "Команда обходит компоненты активной легенды, экспортирует каждый читаемый образец и формирует отчёт по проблемным именам. Для пропущенных типов может быть создана отдельная проверочная легенда.";
                case LibraryToolId.LoadSystemFamilyTypeImages:
                    return "PNG сопоставляются с типами по имени файла. Найденные изображения импортируются как ImageType и записываются в параметр Изображение типоразмера внутри транзакции.";
                case LibraryToolId.ImportByPoint:
                    return "CSV преобразуется в записи размещения и сопоставляется с первым подходящим уровнем. Двери и окна обрабатываются через поиск основы, остальные категории — через точечный сервис; после размещения предлагается итоговый вид.";
                case LibraryToolId.ImportByLine:
                    return "CSV преобразуется в линейные записи, после чего сервис создаёт экземпляры на найденном уровне. После выполнения можно создать итоговый вид с категорией размещения.";
                case LibraryToolId.ImportByBoundary:
                    return "CSV преобразуется в контуры, которые сервис использует для создания элементов на найденном уровне. После выполнения можно создать итоговый вид результата.";
                case LibraryToolId.DeleteSelectedTypesAndFamilies:
                    return "Команда анализирует текущее выделение, собирает экземпляры и связанные типы, семейства, стили линий и штриховки. После подтверждения группы удаляются отдельными контролируемыми транзакциями.";
                default:
                    return "Команда использует существующий сервис RevitLibraryBuilder и сохраняет его проверки и границы транзакций.";
            }
        }

        // Block responsible for keeping WinForms folder dialogs owned by the WPF/Revit window.
        private sealed class Win32Window : Forms.IWin32Window
        {
            public Win32Window(IntPtr handle)
            {
                Handle = handle;
            }

            public IntPtr Handle { get; private set; }
        }

        private class LibraryOperationInfo
        {
            public LibraryOperationInfo(
                LibraryToolId toolId,
                string modeName,
                string buttonName,
                string title,
                string description,
                string requirement,
                string instruction,
                bool isAvailable,
                string unavailableReason)
            {
                ToolId = toolId;
                ModeName = modeName;
                ButtonName = buttonName;
                Title = title;
                Purpose = BuildPurpose(toolId, description);
                Logic = BuildLogic(toolId);
                Requirement = requirement;
                Instruction = instruction;
                IsAvailable = isAvailable;
                UnavailableReason = unavailableReason;
            }

            public LibraryToolId ToolId { get; private set; }

            public string ModeName { get; private set; }

            public string ButtonName { get; private set; }

            public string Title { get; private set; }

            public string Purpose { get; private set; }

            public string Logic { get; private set; }

            public string Requirement { get; private set; }

            public string Instruction { get; private set; }

            public bool IsAvailable { get; private set; }

            public string UnavailableReason { get; private set; }
        }
    }
}
