using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using Autodesk.Revit.DB;
using SAB.DoorWindowExplanations.Models;
using SAB.DoorWindowExplanations.Services;
using SAB.UI;

namespace SAB.DoorWindowExplanations.Views
{
    public partial class DoorWindowViewsWindow : Window
    {
        private readonly IList<DoorWindowSelectionData> _selections;
        private readonly IList<DoorWindowViewTemplateItem> _templates;
        private readonly IList<string> _parameterNames;
        private readonly IList<DoorWindowNamedElementItem> _titleBlockTypes;
        private readonly IList<DoorWindowNamedElementItem> _viewportTypes;
        private readonly IList<DoorWindowNamedElementItem> _dimensionTypes;

        private RadioButton _fullExplicationRadioButton;
        private RadioButton _imageOnlyRadioButton;
        private ComboBox _singleViewKindComboBox;
        private ComboBox _curtainWallFrontSideModeComboBox;
        private FrameworkElement _topViewSettingsPanel;
        private FrameworkElement _frontViewSettingsPanel;
        private FrameworkElement _sectionViewSettingsPanel;
        private FrameworkElement _sheetSettingsPanel;
        private TextBlock _elementSummaryTextBlock;
        private RadioButton _automaticBoundsRadioButton;
        private RadioButton _manualBoundsRadioButton;
        private RadioButton _frontElevationSideRadioButton;
        private RadioButton _backElevationSideRadioButton;
        private TextBox _manualPlanDepthTextBox;
        private ComboBox _positionParameterComboBox;
        private TextBox _topNameFormulaTextBox;
        private TextBox _frontNameFormulaTextBox;
        private TextBox _sectionNameFormulaTextBox;
        private ComboBox _topTemplateComboBox;
        private ComboBox _frontTemplateComboBox;
        private ComboBox _sectionTemplateComboBox;
        private TextBox _topScaleTextBox;
        private TextBox _frontScaleTextBox;
        private TextBox _sectionScaleTextBox;
        private TextBox _topProjectionDepthTextBox;
        private TextBox _frontProjectionDepthTextBox;
        private TextBox _topCutHeightPercentTextBox;
        private TextBox _widthOffsetTextBox;
        private TextBox _planDepthOffsetTextBox;
        private TextBox _frontVerticalOffsetTextBox;
        private TextBox _sectionOffsetTextBox;
        private TextBox _sectionProjectionDepthTextBox;
        private TextBox _sectionLineOverhangTextBox;
        private ComboBox _titleBlockTypeComboBox;
        private ComboBox _viewportTypeComboBox;
        private TextBox _sheetNumberTextBox;
        private TextBox _sheetNameTextBox;
        private TextBox _viewHorizontalStepTextBox;
        private TextBox _elementVerticalStepTextBox;
        private ComboBox _viewTitleAnchorComboBox;
        private TextBox _viewTitleOffsetXTextBox;
        private TextBox _viewTitleOffsetYTextBox;
        private ToggleButton _isolateSelectedElementToggleButton;
        private CheckBox _createFrontDimensionsCheckBox;
        private ComboBox _dimensionTypeComboBox;
        private TextBox _detailedDimensionOffsetTextBox;
        private TextBox _overallDimensionOffsetTextBox;
        private TextBox _sideDetailedDimensionOffsetTextBox;
        private TextBox _sideOverallDimensionOffsetTextBox;
        private ComboBox _horizontalDimensionSideComboBox;
        private ComboBox _verticalDimensionSideComboBox;
        private TextBox _dimensionTextHeightTextBox;
        private CheckBox _createElementImagesCheckBox;
        private TextBox _curtainWallImageParameterTextBox;
        private TextBox _doorWindowTypeImageParameterTextBox;
        private TextBox _imageHeightPaperMmTextBox;
        private CheckBox _saveSettingsCheckBox;
        private TextBlock _validationTextBlock;
        private Button _createButton;
        private Button _cancelButton;

        public DoorWindowViewsWindow(
            IList<DoorWindowSelectionData> selections,
            DoorWindowViewSettings settings,
            IList<DoorWindowViewTemplateItem> templates,
            IList<string> parameterNames,
            IList<DoorWindowNamedElementItem> titleBlockTypes,
            IList<DoorWindowNamedElementItem> viewportTypes,
            IList<DoorWindowNamedElementItem> dimensionTypes)
        {
            _selections = selections ?? throw new ArgumentNullException(nameof(selections));
            if (_selections.Count == 0)
            {
                throw new ArgumentException("Список дверей, окон и витражей пуст.", nameof(selections));
            }

            _templates = templates ?? new List<DoorWindowViewTemplateItem>();
            _parameterNames = parameterNames ?? new List<string>();
            _titleBlockTypes = titleBlockTypes ?? new List<DoorWindowNamedElementItem>();
            _viewportTypes = viewportTypes ?? new List<DoorWindowNamedElementItem>();
            _dimensionTypes = dimensionTypes ?? new List<DoorWindowNamedElementItem>();

            InitializeWindowFromXamlFile();
            BindControls();
            Populate(settings ?? new DoorWindowViewSettings());
            AttachHandlers();
            Loaded += DoorWindowViewsWindow_Loaded;
        }

        public DoorWindowViewSettings SelectedSettings { get; private set; }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(DoorWindowViewsWindow).Assembly.Location);
            string xamlPath = Path.Combine(
                assemblyDirectory,
                "Cls_DoorWindowExplanations",
                "Views",
                "DoorWindowViewsWindow.xaml");

            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл окна настроек не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                ParserContext context = new ParserContext();
                context.BaseUri = new Uri(xamlPath, UriKind.Absolute);
                Window loadedWindow = XamlReader.Load(stream, context) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось загрузить DoorWindowViewsWindow.xaml.");
                }

                _fullExplicationRadioButton = loadedWindow.FindName("FullExplicationRadioButton") as RadioButton;
                _imageOnlyRadioButton = loadedWindow.FindName("ImageOnlyRadioButton") as RadioButton;
                _singleViewKindComboBox = loadedWindow.FindName("SingleViewKindComboBox") as ComboBox;
                _curtainWallFrontSideModeComboBox = loadedWindow.FindName("CurtainWallFrontSideModeComboBox") as ComboBox;
                _topViewSettingsPanel = loadedWindow.FindName("TopViewSettingsPanel") as FrameworkElement;
                _frontViewSettingsPanel = loadedWindow.FindName("FrontViewSettingsPanel") as FrameworkElement;
                _sectionViewSettingsPanel = loadedWindow.FindName("SectionViewSettingsPanel") as FrameworkElement;
                _sheetSettingsPanel = loadedWindow.FindName("SheetSettingsPanel") as FrameworkElement;
                _elementSummaryTextBlock = loadedWindow.FindName("ElementSummaryTextBlock") as TextBlock;
                _automaticBoundsRadioButton = loadedWindow.FindName("AutomaticBoundsRadioButton") as RadioButton;
                _manualBoundsRadioButton = loadedWindow.FindName("ManualBoundsRadioButton") as RadioButton;
                _frontElevationSideRadioButton = loadedWindow.FindName("FrontElevationSideRadioButton") as RadioButton;
                _backElevationSideRadioButton = loadedWindow.FindName("BackElevationSideRadioButton") as RadioButton;
                _manualPlanDepthTextBox = loadedWindow.FindName("ManualPlanDepthTextBox") as TextBox;
                _positionParameterComboBox = loadedWindow.FindName("PositionParameterComboBox") as ComboBox;
                _topNameFormulaTextBox = loadedWindow.FindName("TopNameFormulaTextBox") as TextBox;
                _frontNameFormulaTextBox = loadedWindow.FindName("FrontNameFormulaTextBox") as TextBox;
                _sectionNameFormulaTextBox = loadedWindow.FindName("SectionNameFormulaTextBox") as TextBox;
                _topTemplateComboBox = loadedWindow.FindName("TopTemplateComboBox") as ComboBox;
                _frontTemplateComboBox = loadedWindow.FindName("FrontTemplateComboBox") as ComboBox;
                _sectionTemplateComboBox = loadedWindow.FindName("SectionTemplateComboBox") as ComboBox;
                _topScaleTextBox = loadedWindow.FindName("TopScaleTextBox") as TextBox;
                _frontScaleTextBox = loadedWindow.FindName("FrontScaleTextBox") as TextBox;
                _sectionScaleTextBox = loadedWindow.FindName("SectionScaleTextBox") as TextBox;
                _topProjectionDepthTextBox = loadedWindow.FindName("TopProjectionDepthTextBox") as TextBox;
                _frontProjectionDepthTextBox = loadedWindow.FindName("FrontProjectionDepthTextBox") as TextBox;
                _topCutHeightPercentTextBox = loadedWindow.FindName("TopCutHeightPercentTextBox") as TextBox;
                _widthOffsetTextBox = loadedWindow.FindName("WidthOffsetTextBox") as TextBox;
                _planDepthOffsetTextBox = loadedWindow.FindName("PlanDepthOffsetTextBox") as TextBox;
                _frontVerticalOffsetTextBox = loadedWindow.FindName("FrontVerticalOffsetTextBox") as TextBox;
                _sectionOffsetTextBox = loadedWindow.FindName("SectionOffsetTextBox") as TextBox;
                _sectionProjectionDepthTextBox = loadedWindow.FindName("SectionProjectionDepthTextBox") as TextBox;
                _sectionLineOverhangTextBox = loadedWindow.FindName("SectionLineOverhangTextBox") as TextBox;
                _titleBlockTypeComboBox = loadedWindow.FindName("TitleBlockTypeComboBox") as ComboBox;
                _viewportTypeComboBox = loadedWindow.FindName("ViewportTypeComboBox") as ComboBox;
                _sheetNumberTextBox = loadedWindow.FindName("SheetNumberTextBox") as TextBox;
                _sheetNameTextBox = loadedWindow.FindName("SheetNameTextBox") as TextBox;
                _viewHorizontalStepTextBox = loadedWindow.FindName("ViewHorizontalStepTextBox") as TextBox;
                _elementVerticalStepTextBox = loadedWindow.FindName("ElementVerticalStepTextBox") as TextBox;
                _viewTitleAnchorComboBox = loadedWindow.FindName("ViewTitleAnchorComboBox") as ComboBox;
                _viewTitleOffsetXTextBox = loadedWindow.FindName("ViewTitleOffsetXTextBox") as TextBox;
                _viewTitleOffsetYTextBox = loadedWindow.FindName("ViewTitleOffsetYTextBox") as TextBox;
                _isolateSelectedElementToggleButton = loadedWindow.FindName("IsolateSelectedElementToggleButton") as ToggleButton;
                _createFrontDimensionsCheckBox = loadedWindow.FindName("CreateFrontDimensionsCheckBox") as CheckBox;
                _dimensionTypeComboBox = loadedWindow.FindName("DimensionTypeComboBox") as ComboBox;
                _detailedDimensionOffsetTextBox = loadedWindow.FindName("DetailedDimensionOffsetTextBox") as TextBox;
                _overallDimensionOffsetTextBox = loadedWindow.FindName("OverallDimensionOffsetTextBox") as TextBox;
                _sideDetailedDimensionOffsetTextBox = loadedWindow.FindName("SideDetailedDimensionOffsetTextBox") as TextBox;
                _sideOverallDimensionOffsetTextBox = loadedWindow.FindName("SideOverallDimensionOffsetTextBox") as TextBox;
                _horizontalDimensionSideComboBox = loadedWindow.FindName("HorizontalDimensionSideComboBox") as ComboBox;
                _verticalDimensionSideComboBox = loadedWindow.FindName("VerticalDimensionSideComboBox") as ComboBox;
                _dimensionTextHeightTextBox = loadedWindow.FindName("DimensionTextHeightTextBox") as TextBox;
                _createElementImagesCheckBox = loadedWindow.FindName("CreateElementImagesCheckBox") as CheckBox;
                _curtainWallImageParameterTextBox = loadedWindow.FindName("CurtainWallImageParameterTextBox") as TextBox;
                _doorWindowTypeImageParameterTextBox = loadedWindow.FindName("DoorWindowTypeImageParameterTextBox") as TextBox;
                _imageHeightPaperMmTextBox = loadedWindow.FindName("ImageHeightPaperMmTextBox") as TextBox;
                _saveSettingsCheckBox = loadedWindow.FindName("SaveSettingsCheckBox") as CheckBox;
                _validationTextBlock = loadedWindow.FindName("ValidationTextBlock") as TextBlock;
                _createButton = loadedWindow.FindName("CreateButton") as Button;
                _cancelButton = loadedWindow.FindName("CancelButton") as Button;

                Title = loadedWindow.Title;
                Width = loadedWindow.Width;
                Height = loadedWindow.Height;
                MinWidth = loadedWindow.MinWidth;
                MinHeight = loadedWindow.MinHeight;
                WindowStartupLocation = loadedWindow.WindowStartupLocation;
                ResizeMode = loadedWindow.ResizeMode;
                Background = loadedWindow.Background;
                FontFamily = loadedWindow.FontFamily;
                FontSize = loadedWindow.FontSize;
                Resources = loadedWindow.Resources;
                Content = loadedWindow.Content;
            }
        }

        private void BindControls()
        {
            if (_createButton == null || _cancelButton == null ||
                _fullExplicationRadioButton == null || _imageOnlyRadioButton == null ||
                _singleViewKindComboBox == null || _curtainWallFrontSideModeComboBox == null ||
                _topViewSettingsPanel == null || _frontViewSettingsPanel == null ||
                _sectionViewSettingsPanel == null || _sheetSettingsPanel == null ||
                _automaticBoundsRadioButton == null ||
                _manualBoundsRadioButton == null || _frontElevationSideRadioButton == null ||
                _backElevationSideRadioButton == null || _positionParameterComboBox == null ||
                _titleBlockTypeComboBox == null || _viewportTypeComboBox == null ||
                _viewTitleAnchorComboBox == null || _viewTitleOffsetXTextBox == null ||
                _viewTitleOffsetYTextBox == null || _isolateSelectedElementToggleButton == null ||
                _createFrontDimensionsCheckBox == null ||
                _dimensionTypeComboBox == null || _detailedDimensionOffsetTextBox == null ||
                _overallDimensionOffsetTextBox == null ||
                _sideDetailedDimensionOffsetTextBox == null || _sideOverallDimensionOffsetTextBox == null ||
                _horizontalDimensionSideComboBox == null ||
                _verticalDimensionSideComboBox == null || _dimensionTextHeightTextBox == null ||
                _createElementImagesCheckBox == null ||
                _curtainWallImageParameterTextBox == null || _doorWindowTypeImageParameterTextBox == null ||
                _imageHeightPaperMmTextBox == null)
            {
                throw new InvalidOperationException("Не удалось привязать элементы окна настроек.");
            }
        }

        private void Populate(DoorWindowViewSettings settings)
        {
            _fullExplicationRadioButton.IsChecked = settings.WorkflowMode == DoorWindowWorkflowMode.FullExplication;
            _imageOnlyRadioButton.IsChecked = settings.WorkflowMode == DoorWindowWorkflowMode.ImageOnly;
            _singleViewKindComboBox.SelectedIndex = (int)settings.SingleViewKind;
            _curtainWallFrontSideModeComboBox.SelectedIndex = settings.CurtainWallFrontSideMode == CurtainWallFrontSideMode.AwayFromInteriorPoint
                ? 0
                : settings.CurtainWallFrontSideMode == CurtainWallFrontSideMode.RevitExterior ? 1 : 2;
            _elementSummaryTextBlock.Text = BuildSelectionSummary();
            _automaticBoundsRadioButton.IsChecked = settings.BoundsSourceMode == DoorWindowBoundsSourceMode.Automatic;
            _manualBoundsRadioButton.IsChecked = settings.BoundsSourceMode == DoorWindowBoundsSourceMode.ManualFrontContour;
            _frontElevationSideRadioButton.IsChecked = settings.ElevationSide == DoorWindowElevationSide.Front;
            _backElevationSideRadioButton.IsChecked = settings.ElevationSide == DoorWindowElevationSide.Back;

            _positionParameterComboBox.ItemsSource = _parameterNames;
            DoorWindowParameterService parameterService = new DoorWindowParameterService();
            _positionParameterComboBox.SelectedItem = parameterService.FindPreferredPositionParameter(
                _parameterNames,
                settings.PositionParameterName);

            _topNameFormulaTextBox.Text = settings.TopViewNameFormula;
            _frontNameFormulaTextBox.Text = settings.FrontViewNameFormula;
            _sectionNameFormulaTextBox.Text = settings.SectionViewNameFormula;

            PopulateTemplateComboBox(_topTemplateComboBox, settings.TopViewTemplateName);
            PopulateTemplateComboBox(_frontTemplateComboBox, settings.FrontViewTemplateName);
            PopulateTemplateComboBox(_sectionTemplateComboBox, settings.SectionViewTemplateName);

            _topScaleTextBox.Text = settings.TopViewScale.ToString(CultureInfo.CurrentCulture);
            _frontScaleTextBox.Text = settings.FrontViewScale.ToString(CultureInfo.CurrentCulture);
            _sectionScaleTextBox.Text = settings.SectionViewScale.ToString(CultureInfo.CurrentCulture);
            _topProjectionDepthTextBox.Text = FormatDouble(settings.TopProjectionDepthMm);
            _frontProjectionDepthTextBox.Text = FormatDouble(settings.FrontProjectionDepthMm);
            _topCutHeightPercentTextBox.Text = FormatDouble(settings.TopCutHeightPercent);
            _widthOffsetTextBox.Text = FormatDouble(settings.WidthCropOffsetMm);
            _planDepthOffsetTextBox.Text = FormatDouble(settings.PlanDepthCropOffsetMm);
            _frontVerticalOffsetTextBox.Text = FormatDouble(settings.FrontVerticalCropOffsetMm);
            _sectionOffsetTextBox.Text = FormatDouble(settings.SectionCropOffsetMm);
            _manualPlanDepthTextBox.Text = FormatDouble(settings.ManualPlanDepthMm);
            _sectionProjectionDepthTextBox.Text = FormatDouble(settings.SectionProjectionDepthMm);
            _sectionLineOverhangTextBox.Text = FormatDouble(settings.SectionMarkerExtensionMm);
            PopulateNamedComboBox(_titleBlockTypeComboBox, _titleBlockTypes, settings.TitleBlockTypeName);
            PopulateNamedComboBox(
                _viewportTypeComboBox,
                _viewportTypes,
                settings.ViewportTypeIdValue,
                settings.ViewportTypeName);
            _sheetNumberTextBox.Text = settings.SheetNumber ?? string.Empty;
            _sheetNameTextBox.Text = settings.SheetName ?? string.Empty;
            _viewHorizontalStepTextBox.Text = FormatDouble(settings.ViewHorizontalStepMm);
            _elementVerticalStepTextBox.Text = FormatDouble(settings.ElementVerticalStepMm);
            PopulateViewTitleAnchorComboBox(settings.ViewTitleAnchor);
            _viewTitleOffsetXTextBox.Text = FormatDouble(settings.ViewTitleOffsetXmm);
            _viewTitleOffsetYTextBox.Text = FormatDouble(settings.ViewTitleOffsetYmm);
            bool onlyCurtainWalls = _selections.Count > 0;
            for (int selectionIndex = 0; selectionIndex < _selections.Count; selectionIndex++)
            {
                if (_selections[selectionIndex] == null || !_selections[selectionIndex].IsCurtainWall)
                {
                    onlyCurtainWalls = false;
                    break;
                }
            }

            _isolateSelectedElementToggleButton.IsChecked = onlyCurtainWalls
                ? true
                : settings.IsolateSelectedElement;
            _isolateSelectedElementToggleButton.IsEnabled = !onlyCurtainWalls;
            _createFrontDimensionsCheckBox.IsChecked = settings.CreateFrontDimensions;
            PopulateNamedComboBox(
                _dimensionTypeComboBox,
                _dimensionTypes,
                settings.DimensionTypeIdValue,
                settings.DimensionTypeName);
            _detailedDimensionOffsetTextBox.Text = FormatDouble(settings.DetailedDimensionOffsetPaperMm);
            _overallDimensionOffsetTextBox.Text = FormatDouble(settings.OverallDimensionOffsetPaperMm);
            _sideDetailedDimensionOffsetTextBox.Text = FormatDouble(settings.SideDetailedDimensionOffsetPaperMm);
            _sideOverallDimensionOffsetTextBox.Text = FormatDouble(settings.SideOverallDimensionOffsetPaperMm);
            _horizontalDimensionSideComboBox.SelectedIndex = (int)settings.HorizontalDimensionSide;
            _verticalDimensionSideComboBox.SelectedIndex = (int)settings.VerticalDimensionSide;
            _dimensionTextHeightTextBox.Text = FormatDouble(settings.DimensionTextHeightMm);
            _createElementImagesCheckBox.IsChecked = settings.CreateElementImages;
            _curtainWallImageParameterTextBox.Text = settings.CurtainWallInstanceImageParameterName ?? string.Empty;
            _doorWindowTypeImageParameterTextBox.Text = settings.DoorWindowTypeImageParameterName ?? string.Empty;
            _imageHeightPaperMmTextBox.Text = FormatDouble(settings.ImageHeightPaperMm);
            _saveSettingsCheckBox.IsChecked = settings.SaveSettings;
            UpdateManualModeState();
            UpdateWorkflowState();
        }

        private void PopulateTemplateComboBox(ComboBox comboBox, string selectedName)
        {
            comboBox.ItemsSource = _templates;
            DoorWindowRevitDataService dataService = new DoorWindowRevitDataService();
            comboBox.SelectedItem = dataService.FindTemplate(_templates, selectedName);
        }

        private void PopulateNamedComboBox(
            ComboBox comboBox,
            IList<DoorWindowNamedElementItem> items,
            string selectedName)
        {
            PopulateNamedComboBox(comboBox, items, -1, selectedName);
        }

        private void PopulateNamedComboBox(
            ComboBox comboBox,
            IList<DoorWindowNamedElementItem> items,
            int selectedIdValue,
            string selectedName)
        {
            comboBox.ItemsSource = items;
            DoorWindowRevitDataService dataService = new DoorWindowRevitDataService();
            comboBox.SelectedItem = dataService.FindNamedItem(items, selectedIdValue, selectedName);
        }

        private void AttachHandlers()
        {
            _createButton.Click += CreateButton_Click;
            _cancelButton.Click += CancelButton_Click;
            _automaticBoundsRadioButton.Click += BoundsModeRadioButton_Click;
            _manualBoundsRadioButton.Click += BoundsModeRadioButton_Click;
            _fullExplicationRadioButton.Click += WorkflowModeChanged;
            _imageOnlyRadioButton.Click += WorkflowModeChanged;
            _singleViewKindComboBox.SelectionChanged += WorkflowModeChanged;
        }

        private void DoorWindowViewsWindow_Loaded(object sender, RoutedEventArgs e)
        {
            WindowSizeSettingsService.Apply(this, "DoorWindowExplanations.SettingsWindow");
            SabWindowBehaviorService.ApplyLoadedBehavior(this);
        }

        private void BoundsModeRadioButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateManualModeState();
        }

        private void UpdateManualModeState()
        {
            _manualPlanDepthTextBox.IsEnabled = _manualBoundsRadioButton.IsChecked == true;
        }

        private void WorkflowModeChanged(object sender, RoutedEventArgs e)
        {
            UpdateWorkflowState();
        }

        private void UpdateWorkflowState()
        {
            bool imageOnly = _imageOnlyRadioButton.IsChecked == true;
            _singleViewKindComboBox.IsEnabled = imageOnly;
            if (imageOnly)
            {
                _createElementImagesCheckBox.IsChecked = true;
            }
            _createElementImagesCheckBox.IsEnabled = !imageOnly;
            bool hasFrontView = !imageOnly || _singleViewKindComboBox.SelectedIndex <= 0;
            _createFrontDimensionsCheckBox.IsEnabled = hasFrontView;
            _topViewSettingsPanel.Visibility = !imageOnly || _singleViewKindComboBox.SelectedIndex == 1
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
            _frontViewSettingsPanel.Visibility = !imageOnly || _singleViewKindComboBox.SelectedIndex <= 0
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
            _sectionViewSettingsPanel.Visibility = !imageOnly || _singleViewKindComboBox.SelectedIndex == 2
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;
            _sheetSettingsPanel.Visibility = imageOnly
                ? System.Windows.Visibility.Collapsed
                : System.Windows.Visibility.Visible;
            int viewCount = imageOnly ? _selections.Count : _selections.Count * 3;
            _validationTextBlock.Text = imageOnly
                ? "Будет создано технических видов: " + viewCount + " · лист не создаётся."
                : "Будет создано видов: " + viewCount + " · элементов на листе: " + _selections.Count + ".";
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            DoorWindowViewSettings settings;
            string validationMessage;
            if (!TryReadSettings(out settings, out validationMessage))
            {
                _validationTextBlock.Text = validationMessage;
                return;
            }

            SelectedSettings = settings;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private bool TryReadSettings(out DoorWindowViewSettings settings, out string message)
        {
            settings = new DoorWindowViewSettings();
            message = string.Empty;

            settings.WorkflowMode = _imageOnlyRadioButton.IsChecked == true
                ? DoorWindowWorkflowMode.ImageOnly
                : DoorWindowWorkflowMode.FullExplication;
            settings.SingleViewKind = _singleViewKindComboBox.SelectedIndex == 1
                ? DoorWindowSingleViewKind.Top
                : _singleViewKindComboBox.SelectedIndex == 2
                    ? DoorWindowSingleViewKind.Section
                    : DoorWindowSingleViewKind.Front;
            settings.CurtainWallFrontSideMode = _curtainWallFrontSideModeComboBox.SelectedIndex == 1
                ? CurtainWallFrontSideMode.RevitExterior
                : _curtainWallFrontSideModeComboBox.SelectedIndex == 2
                    ? CurtainWallFrontSideMode.RevitInterior
                    : CurtainWallFrontSideMode.AwayFromInteriorPoint;
            settings.BoundsSourceMode = _manualBoundsRadioButton.IsChecked == true
                ? DoorWindowBoundsSourceMode.ManualFrontContour
                : DoorWindowBoundsSourceMode.Automatic;
            settings.ElevationSide = _backElevationSideRadioButton.IsChecked == true
                ? DoorWindowElevationSide.Back
                : DoorWindowElevationSide.Front;
            settings.PositionParameterName = _positionParameterComboBox.SelectedItem as string ?? string.Empty;
            settings.TopViewNameFormula = (_topNameFormulaTextBox.Text ?? string.Empty).Trim();
            settings.FrontViewNameFormula = (_frontNameFormulaTextBox.Text ?? string.Empty).Trim();
            settings.SectionViewNameFormula = (_sectionNameFormulaTextBox.Text ?? string.Empty).Trim();
            settings.TopViewTemplateName = GetTemplateName(_topTemplateComboBox);
            settings.FrontViewTemplateName = GetTemplateName(_frontTemplateComboBox);
            settings.SectionViewTemplateName = GetTemplateName(_sectionTemplateComboBox);
            settings.TitleBlockTypeName = GetNamedItemName(_titleBlockTypeComboBox);
            DoorWindowNamedElementItem selectedViewportType =
                _viewportTypeComboBox.SelectedItem as DoorWindowNamedElementItem;
            settings.ViewportTypeName = selectedViewportType != null
                ? selectedViewportType.Name
                : string.Empty;
            settings.ViewportTypeIdValue = selectedViewportType != null && selectedViewportType.Id != null
                ? selectedViewportType.Id.IntegerValue
                : -1;
            settings.SheetNumber = (_sheetNumberTextBox.Text ?? string.Empty).Trim();
            settings.SheetName = (_sheetNameTextBox.Text ?? string.Empty).Trim();
            DoorWindowViewTitleAnchorOption viewTitleAnchor =
                _viewTitleAnchorComboBox.SelectedItem as DoorWindowViewTitleAnchorOption;
            settings.ViewTitleAnchor = viewTitleAnchor != null
                ? viewTitleAnchor.Value
                : DoorWindowViewTitleAnchor.BottomLeft;
            settings.SaveSettings = _saveSettingsCheckBox.IsChecked == true;
            settings.IsolateSelectedElement = _isolateSelectedElementToggleButton.IsChecked == true;
            settings.CreateFrontDimensions = _createFrontDimensionsCheckBox.IsChecked == true &&
                                             (settings.WorkflowMode == DoorWindowWorkflowMode.FullExplication ||
                                              settings.SingleViewKind == DoorWindowSingleViewKind.Front);
            DoorWindowNamedElementItem selectedDimensionType =
                _dimensionTypeComboBox.SelectedItem as DoorWindowNamedElementItem;
            settings.DimensionTypeName = selectedDimensionType != null
                ? selectedDimensionType.Name
                : string.Empty;
            settings.DimensionTypeIdValue = selectedDimensionType != null && selectedDimensionType.Id != null
                ? selectedDimensionType.Id.IntegerValue
                : -1;
            settings.CreateElementImages = settings.WorkflowMode == DoorWindowWorkflowMode.ImageOnly ||
                                           _createElementImagesCheckBox.IsChecked == true;
            settings.CurtainWallInstanceImageParameterName =
                (_curtainWallImageParameterTextBox.Text ?? string.Empty).Trim();
            settings.DoorWindowTypeImageParameterName =
                (_doorWindowTypeImageParameterTextBox.Text ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(settings.TopViewNameFormula) ||
                string.IsNullOrWhiteSpace(settings.FrontViewNameFormula) ||
                string.IsNullOrWhiteSpace(settings.SectionViewNameFormula))
            {
                message = "Заполните формулы имён для всех трёх видов.";
                return false;
            }

            if (settings.WorkflowMode == DoorWindowWorkflowMode.FullExplication &&
                string.IsNullOrWhiteSpace(settings.TitleBlockTypeName))
            {
                message = "Выберите тип основной надписи для общего листа.";
                return false;
            }

            if (settings.WorkflowMode == DoorWindowWorkflowMode.FullExplication &&
                (string.IsNullOrWhiteSpace(settings.SheetNumber) || string.IsNullOrWhiteSpace(settings.SheetName)))
            {
                message = "Заполните номер и имя общего листа.";
                return false;
            }

            if ((ContainsPositionToken(settings.TopViewNameFormula) ||
                 ContainsPositionToken(settings.FrontViewNameFormula) ||
                 ContainsPositionToken(settings.SectionViewNameFormula)) &&
                string.IsNullOrWhiteSpace(settings.PositionParameterName))
            {
                message = "Формула использует {Позиция}, но параметр позиции не выбран.";
                return false;
            }

            int topScale;
            int frontScale;
            int sectionScale;
            if (!TryReadScale(_topScaleTextBox, "масштаб вида сверху", out topScale, out message) ||
                !TryReadScale(_frontScaleTextBox, "масштаб фасадного вида", out frontScale, out message) ||
                !TryReadScale(_sectionScaleTextBox, "масштаб разреза", out sectionScale, out message))
            {
                return false;
            }

            settings.TopViewScale = topScale;
            settings.FrontViewScale = frontScale;
            settings.SectionViewScale = sectionScale;

            double widthOffset;
            double planDepthOffset;
            double frontVerticalOffset;
            double sectionOffset;
            double sectionLineOverhang;
            double topProjectionDepth;
            double frontProjectionDepth;
            double sectionProjectionDepth;
            double manualPlanDepth;
            double topCutHeightPercent;
            double viewHorizontalStep;
            double elementVerticalStep;
            double viewTitleOffsetX;
            double viewTitleOffsetY;
            double detailedDimensionOffset;
            double overallDimensionOffset;
            double sideDetailedDimensionOffset;
            double sideOverallDimensionOffset;
            double dimensionTextHeight;
            if (!TryReadNonNegative(_widthOffsetTextBox, "общий боковой офсет", out widthOffset, out message) ||
                !TryReadNonNegative(_planDepthOffsetTextBox, "офсет глубины плана", out planDepthOffset, out message) ||
                !TryReadNonNegative(_frontVerticalOffsetTextBox, "вертикальный офсет фронта", out frontVerticalOffset, out message) ||
                !TryReadNonNegative(_sectionOffsetTextBox, "офсет разреза", out sectionOffset, out message) ||
                !TryReadNonNegative(
                    _sectionLineOverhangTextBox,
                    "выступ линии разреза за границу фасадного вида",
                    out sectionLineOverhang,
                    out message) ||
                !TryReadPositive(_topProjectionDepthTextBox, "глубина проекции вида сверху", out topProjectionDepth, out message) ||
                !TryReadPositive(_frontProjectionDepthTextBox, "глубина проекции фасадного вида", out frontProjectionDepth, out message) ||
                !TryReadPositive(_sectionProjectionDepthTextBox, "глубина проекции разреза", out sectionProjectionDepth, out message) ||
                !TryReadPositive(_manualPlanDepthTextBox, "глубина стены для ручного контура", out manualPlanDepth, out message) ||
                !TryReadRange(_topCutHeightPercentTextBox, "высота сечения", 0.0, 100.0, out topCutHeightPercent, out message) ||
                !TryReadPositive(_viewHorizontalStepTextBox, "зазор между видами", out viewHorizontalStep, out message) ||
                !TryReadPositive(_elementVerticalStepTextBox, "зазор между элементами", out elementVerticalStep, out message) ||
                !TryReadNumber(_viewTitleOffsetXTextBox, "смещение заголовка X", out viewTitleOffsetX, out message) ||
                !TryReadNumber(_viewTitleOffsetYTextBox, "смещение заголовка Y", out viewTitleOffsetY, out message) ||
                !TryReadNonNegative(_detailedDimensionOffsetTextBox, "отступ цепочки импостов", out detailedDimensionOffset, out message) ||
                !TryReadNonNegative(_overallDimensionOffsetTextBox, "отступ общего габарита", out overallDimensionOffset, out message) ||
                !TryReadNonNegative(_sideDetailedDimensionOffsetTextBox, "отступ боковой цепочки импостов", out sideDetailedDimensionOffset, out message) ||
                !TryReadNonNegative(_sideOverallDimensionOffsetTextBox, "отступ бокового общего габарита", out sideOverallDimensionOffset, out message) ||
                !TryReadNonNegative(_dimensionTextHeightTextBox, "высота текста размера", out dimensionTextHeight, out message))
            {
                return false;
            }

            settings.WidthCropOffsetMm = widthOffset;
            settings.PlanDepthCropOffsetMm = planDepthOffset;
            settings.FrontVerticalCropOffsetMm = frontVerticalOffset;
            settings.SectionCropOffsetMm = sectionOffset;
            settings.SectionMarkerExtensionMm = sectionLineOverhang;
            settings.TopProjectionDepthMm = topProjectionDepth;
            settings.FrontProjectionDepthMm = frontProjectionDepth;
            settings.SectionProjectionDepthMm = sectionProjectionDepth;
            settings.ManualPlanDepthMm = manualPlanDepth;
            settings.TopCutHeightPercent = topCutHeightPercent;
            settings.ViewHorizontalStepMm = viewHorizontalStep;
            settings.ElementVerticalStepMm = elementVerticalStep;
            settings.ViewTitleOffsetXmm = viewTitleOffsetX;
            settings.ViewTitleOffsetYmm = viewTitleOffsetY;
            settings.DetailedDimensionOffsetPaperMm = detailedDimensionOffset;
            settings.OverallDimensionOffsetPaperMm = overallDimensionOffset;
            settings.SideDetailedDimensionOffsetPaperMm = sideDetailedDimensionOffset;
            settings.SideOverallDimensionOffsetPaperMm = sideOverallDimensionOffset;
            if (overallDimensionOffset < detailedDimensionOffset)
            {
                message = "Отступ позиции 2 снизу / сверху должен быть не меньше отступа позиции 1.";
                return false;
            }

            if (sideOverallDimensionOffset < sideDetailedDimensionOffset)
            {
                message = "Отступ позиции 2 слева / справа должен быть не меньше отступа позиции 1.";
                return false;
            }

            settings.DimensionTextHeightMm = dimensionTextHeight;
            settings.HorizontalDimensionSide = _horizontalDimensionSideComboBox.SelectedIndex == 1
                ? DoorWindowHorizontalDimensionSide.Top
                : DoorWindowHorizontalDimensionSide.Bottom;
            settings.VerticalDimensionSide = _verticalDimensionSideComboBox.SelectedIndex == 1
                ? DoorWindowVerticalDimensionSide.Right
                : DoorWindowVerticalDimensionSide.Left;

            double imageHeightPaperMm;
            if (!TryReadRange(_imageHeightPaperMmTextBox, "высота изображения на листе", 10.0, 500.0,
                out imageHeightPaperMm, out message))
            {
                return false;
            }

            settings.ImageHeightPaperMm = imageHeightPaperMm;
            if (settings.CreateFrontDimensions && selectedDimensionType == null)
            {
                message = "Выберите тип линейного размера.";
                return false;
            }

            if (settings.CreateElementImages &&
                (string.IsNullOrWhiteSpace(settings.CurtainWallInstanceImageParameterName) ||
                 string.IsNullOrWhiteSpace(settings.DoorWindowTypeImageParameterName)))
            {
                message = "Заполните имена параметров изображений для витражей, окон и дверей.";
                return false;
            }

            message = "Настройки корректны.";
            return true;
        }

        private bool TryReadScale(TextBox textBox, string fieldName, out int value, out string message)
        {
            if (!int.TryParse(textBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value) ||
                value < 1 || value > 1000)
            {
                message = "Поле «" + fieldName + "» должно содержать целое число от 1 до 1000.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private bool TryReadNonNegative(TextBox textBox, string fieldName, out double value, out string message)
        {
            if (!TryParseDouble(textBox.Text, out value) || value < 0.0)
            {
                message = "Поле «" + fieldName + "» должно содержать число не меньше нуля.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private bool TryReadPositive(TextBox textBox, string fieldName, out double value, out string message)
        {
            if (!TryParseDouble(textBox.Text, out value) || value <= 0.0)
            {
                message = "Поле «" + fieldName + "» должно содержать число больше нуля.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private bool TryReadNumber(TextBox textBox, string fieldName, out double value, out string message)
        {
            if (!TryParseDouble(textBox.Text, out value))
            {
                message = "Поле «" + fieldName + "» должно содержать число.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private bool TryReadRange(
            TextBox textBox,
            string fieldName,
            double minimum,
            double maximum,
            out double value,
            out string message)
        {
            if (!TryParseDouble(textBox.Text, out value) || value < minimum || value > maximum)
            {
                message = "Поле «" + fieldName + "» должно содержать число от " +
                          minimum.ToString("0.###", CultureInfo.CurrentCulture) + " до " +
                          maximum.ToString("0.###", CultureInfo.CurrentCulture) + ".";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private bool TryParseDouble(string text, out double value)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                return true;
            }

            string normalized = (text ?? string.Empty).Replace(',', '.');
            return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private string GetTemplateName(ComboBox comboBox)
        {
            DoorWindowViewTemplateItem item = comboBox.SelectedItem as DoorWindowViewTemplateItem;
            return item == null || item.Id == ElementId.InvalidElementId ? string.Empty : item.Name;
        }

        private string GetNamedItemName(ComboBox comboBox)
        {
            DoorWindowNamedElementItem item = comboBox.SelectedItem as DoorWindowNamedElementItem;
            return item != null ? item.Name : string.Empty;
        }

        private bool ContainsPositionToken(string formula)
        {
            return (formula ?? string.Empty).IndexOf("{Позиция}", StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private string FormatDouble(double value)
        {
            return value.ToString("0.###", CultureInfo.CurrentCulture);
        }

        private void PopulateViewTitleAnchorComboBox(DoorWindowViewTitleAnchor selectedAnchor)
        {
            List<DoorWindowViewTitleAnchorOption> options = new List<DoorWindowViewTitleAnchorOption>
            {
                new DoorWindowViewTitleAnchorOption(
                    DoorWindowViewTitleAnchor.BottomLeft,
                    "Слева под видом"),
                new DoorWindowViewTitleAnchorOption(
                    DoorWindowViewTitleAnchor.BottomCenter,
                    "По центру под видом"),
                new DoorWindowViewTitleAnchorOption(
                    DoorWindowViewTitleAnchor.BottomRight,
                    "Справа под видом"),
                new DoorWindowViewTitleAnchorOption(
                    DoorWindowViewTitleAnchor.TopCenter,
                    "По центру над видом")
            };

            _viewTitleAnchorComboBox.ItemsSource = options;
            _viewTitleAnchorComboBox.SelectedIndex = 0;
            for (int index = 0; index < options.Count; index++)
            {
                if (options[index].Value == selectedAnchor)
                {
                    _viewTitleAnchorComboBox.SelectedIndex = index;
                    break;
                }
            }
        }

        private string BuildSelectionSummary()
        {
            if (_selections.Count == 1)
            {
                return _selections[0].DisplayName;
            }

            int doors = 0;
            int windows = 0;
            int curtainWalls = 0;
            int linked = 0;
            for (int i = 0; i < _selections.Count; i++)
            {
                DoorWindowSelectionData item = _selections[i];
                if (item == null)
                {
                    continue;
                }

                if (item.IsLinked)
                {
                    linked++;
                }

                if (item.IsCurtainWall)
                {
                    curtainWalls++;
                }
                else if (item.IsWindow)
                {
                    windows++;
                }
                else
                {
                    doors++;
                }
            }

            return "Выбрано элементов: " + _selections.Count +
                   "\nДверей: " + doors + " · окон: " + windows +
                   " · витражей: " + curtainWalls + " · из связей: " + linked;
        }

        private class DoorWindowViewTitleAnchorOption
        {
            public DoorWindowViewTitleAnchorOption(
                DoorWindowViewTitleAnchor value,
                string displayName)
            {
                Value = value;
                DisplayName = displayName;
            }

            public DoorWindowViewTitleAnchor Value { get; private set; }

            public string DisplayName { get; private set; }

            public override string ToString()
            {
                return DisplayName;
            }
        }
    }
}
