using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Settings;
using SAB.InteriorElevations.Utils;
using SAB.UI;

namespace SAB.InteriorElevations.Views
{
    public partial class ElevationDecorationWindow : Window
    {
        private readonly Document _document;
        private readonly ElevationDecorationSettings _initialSettings;

        private System.Windows.Controls.Grid _windowRoot;
        private Border _titleBar;
        private Button _minimizeButton;
        private Button _closeButton;
        private Button _okButton;
        private Button _cancelButton;
        private CheckBox _placeSpotElevations;
        private CheckBox _placeTags;
        private CheckBox _placeDimensions;
        private CheckBox _placeWallTags;
        private CheckBox _placeFloorTags;
        private CheckBox _placeCeilingTags;
        private CheckBox _placePlinthTags;
        private CheckBox _placeDoorTags;
        private CheckBox _placeWindowTags;
        private CheckBox _placeVerticalDimensions;
        private CheckBox _placeHorizontalDimensions;
        private CheckBox _placeFinishFloorSpot;
        private CheckBox _placeSubfloorSpots;
        private CheckBox _placeStructuralBaseSpot;
        private RadioButton _leftSide;
        private RadioButton _rightSide;
        private TextBox _spotOffset;
        private TextBox _tagOffset;
        private TextBox _detailedDimensionOffset;
        private TextBox _overallDimensionOffset;
        private TextBox _widthDimensionOffset;
        private ComboBox _floorSpotType;
        private ComboBox _overheadSpotType;
        private ComboBox _spotRelativeBaseLevel;
        private ComboBox _detailedDimensionType;
        private ComboBox _overallDimensionType;
        private ComboBox _widthDimensionType;
        private ComboBox _wallTagType;
        private ComboBox _floorTagType;
        private ComboBox _ceilingTagType;
        private ComboBox _plinthTagType;
        private ComboBox _doorTagType;
        private ComboBox _windowTagType;
        private CheckBox _wallLeader;
        private CheckBox _floorLeader;
        private CheckBox _ceilingLeader;
        private CheckBox _plinthLeader;
        private CheckBox _doorLeader;
        private CheckBox _windowLeader;
        private Button _configureCatalogButton;
        private TextBlock _catalogStatusText;
        private TextBlock _catalogDetailsText;
        private TextBlock _validationStatusText;
        private ElevationDecorationCatalog _catalog;

        public ElevationDecorationWindow(Document document, ElevationDecorationSettings initialSettings)
        {
            _document = document;
            _initialSettings = initialSettings;

            InitializeWindowFromXamlFile();
            ResolveControls();
            PopulateTypeLists();
            ApplyInitialSettings();
            AttachHandlers();
        }

        public ElevationDecorationSettings SelectedSettings { get; private set; }

        public bool OpenCatalogRequested { get; private set; }

        private void InitializeWindowFromXamlFile()
        {
            string assemblyDirectory = Path.GetDirectoryName(typeof(ElevationDecorationWindow).Assembly.Location);
            string xamlPath = Path.Combine(
                assemblyDirectory,
                "Cls_InteriorElevations",
                "Views",
                "ElevationDecorationWindow.xaml");
            if (!File.Exists(xamlPath))
            {
                throw new InvalidOperationException("Файл окна оформления не найден: " + xamlPath);
            }

            using (FileStream stream = File.OpenRead(xamlPath))
            {
                ParserContext context = new ParserContext();
                context.BaseUri = new Uri(xamlPath, UriKind.Absolute);
                Window loadedWindow = XamlReader.Load(stream, context) as Window;
                if (loadedWindow == null)
                {
                    throw new InvalidOperationException("Не удалось загрузить ElevationDecorationWindow.xaml.");
                }

                Title = loadedWindow.Title;
                Width = loadedWindow.Width;
                Height = loadedWindow.Height;
                MinWidth = loadedWindow.MinWidth;
                MinHeight = loadedWindow.MinHeight;
                WindowStartupLocation = loadedWindow.WindowStartupLocation;
                ResizeMode = loadedWindow.ResizeMode;
                WindowStyle = loadedWindow.WindowStyle;
                AllowsTransparency = loadedWindow.AllowsTransparency;
                Background = loadedWindow.Background;
                FontFamily = loadedWindow.FontFamily;
                FontSize = loadedWindow.FontSize;
                Resources = loadedWindow.Resources;
                Content = loadedWindow.Content;

                WindowChrome loadedChrome = WindowChrome.GetWindowChrome(loadedWindow);
                if (loadedChrome != null)
                {
                    WindowChrome.SetWindowChrome(this, (WindowChrome)loadedChrome.Clone());
                }

                WindowSizeSettingsService.Apply(this, "InteriorElevations.ElevationDecorationWindow.V1");
            }
        }

        private void ResolveControls()
        {
            _windowRoot = Find<System.Windows.Controls.Grid>("WindowRoot");
            _titleBar = Find<Border>("TitleBar");
            _minimizeButton = Find<Button>("MinimizeWindowButton");
            _closeButton = Find<Button>("CloseWindowButton");
            _okButton = Find<Button>("OkButton");
            _cancelButton = Find<Button>("CancelButton");
            _placeSpotElevations = Find<CheckBox>("PlaceSpotElevationsCheckBox");
            _placeTags = Find<CheckBox>("PlaceTagsCheckBox");
            _placeDimensions = Find<CheckBox>("PlaceDimensionsCheckBox");
            _placeWallTags = Find<CheckBox>("PlaceWallTagsCheckBox");
            _placeFloorTags = Find<CheckBox>("PlaceFloorTagsCheckBox");
            _placeCeilingTags = Find<CheckBox>("PlaceCeilingTagsCheckBox");
            _placePlinthTags = Find<CheckBox>("PlacePlinthTagsCheckBox");
            _placeDoorTags = Find<CheckBox>("PlaceDoorTagsCheckBox");
            _placeWindowTags = Find<CheckBox>("PlaceWindowTagsCheckBox");
            _placeVerticalDimensions = Find<CheckBox>("PlaceVerticalDimensionsCheckBox");
            _placeHorizontalDimensions = Find<CheckBox>("PlaceHorizontalDimensionsCheckBox");
            _placeFinishFloorSpot = Find<CheckBox>("PlaceFinishFloorSpotCheckBox");
            _placeSubfloorSpots = Find<CheckBox>("PlaceSubfloorSpotsCheckBox");
            _placeStructuralBaseSpot = Find<CheckBox>("PlaceStructuralBaseSpotCheckBox");
            _leftSide = Find<RadioButton>("LeftSideRadioButton");
            _rightSide = Find<RadioButton>("RightSideRadioButton");
            _spotOffset = Find<TextBox>("SpotOffsetTextBox");
            _tagOffset = Find<TextBox>("TagOffsetTextBox");
            _detailedDimensionOffset = Find<TextBox>("DetailedDimensionOffsetTextBox");
            _overallDimensionOffset = Find<TextBox>("OverallDimensionOffsetTextBox");
            _widthDimensionOffset = Find<TextBox>("WidthDimensionOffsetTextBox");
            _floorSpotType = Find<ComboBox>("FloorSpotElevationTypeComboBox");
            _overheadSpotType = Find<ComboBox>("OverheadSpotElevationTypeComboBox");
            _spotRelativeBaseLevel = Find<ComboBox>("SpotRelativeBaseLevelComboBox");
            _detailedDimensionType = Find<ComboBox>("DetailedDimensionTypeComboBox");
            _overallDimensionType = Find<ComboBox>("OverallDimensionTypeComboBox");
            _widthDimensionType = Find<ComboBox>("WidthDimensionTypeComboBox");
            _wallTagType = Find<ComboBox>("WallTagTypeComboBox");
            _floorTagType = Find<ComboBox>("FloorTagTypeComboBox");
            _ceilingTagType = Find<ComboBox>("CeilingTagTypeComboBox");
            _plinthTagType = Find<ComboBox>("PlinthTagTypeComboBox");
            _doorTagType = Find<ComboBox>("DoorTagTypeComboBox");
            _windowTagType = Find<ComboBox>("WindowTagTypeComboBox");
            _wallLeader = Find<CheckBox>("WallLeaderCheckBox");
            _floorLeader = Find<CheckBox>("FloorLeaderCheckBox");
            _ceilingLeader = Find<CheckBox>("CeilingLeaderCheckBox");
            _plinthLeader = Find<CheckBox>("PlinthLeaderCheckBox");
            _doorLeader = Find<CheckBox>("DoorLeaderCheckBox");
            _windowLeader = Find<CheckBox>("WindowLeaderCheckBox");
            _configureCatalogButton = Find<Button>("ConfigureCatalogButton");
            _catalogStatusText = Find<TextBlock>("CatalogStatusTextBlock");
            _catalogDetailsText = Find<TextBlock>("CatalogDetailsTextBlock");
            _validationStatusText = Find<TextBlock>("ValidationStatusTextBlock");

            if (_windowRoot == null || _titleBar == null || _okButton == null || _cancelButton == null)
            {
                throw new InvalidOperationException("Не удалось привязать элементы окна оформления разверток.");
            }
        }

        private void PopulateTypeLists()
        {
            SetItems(_floorSpotType, CollectSpotTypes());
            SetItems(_overheadSpotType, CollectSpotTypes());
            SetItems(_spotRelativeBaseLevel, CollectLevels());
            IList<AnnotationTypeOption> dimensionTypes = CollectDimensionTypes();
            SetItems(_detailedDimensionType, dimensionTypes);
            SetItems(_overallDimensionType, dimensionTypes);
            SetItems(_widthDimensionType, dimensionTypes);
            SetItems(_wallTagType, CollectTagTypes(BuiltInCategory.OST_WallTags));
            SetItems(_floorTagType, CollectTagTypes(BuiltInCategory.OST_FloorTags));
            SetItems(_ceilingTagType, CollectTagTypes(BuiltInCategory.OST_CeilingTags));
            SetItems(_plinthTagType, CollectTagTypes(BuiltInCategory.OST_StairsRailingTags));
            SetItems(_doorTagType, CollectTagTypes(BuiltInCategory.OST_DoorTags));
            SetItems(_windowTagType, CollectTagTypes(BuiltInCategory.OST_WindowTags));
            _catalog = new ElevationDecorationCatalogService().Load();
            RefreshCatalogStatus();
        }

        private IList<AnnotationTypeOption> CollectSpotTypes()
        {
            List<AnnotationTypeOption> options = new List<AnnotationTypeOption>();
            options.Add(CreateNoneOption());
            options.AddRange(new FilteredElementCollector(_document)
                .OfClass(typeof(SpotDimensionType))
                .Cast<SpotDimensionType>()
                .OrderBy(type => type.Name)
                .Select(type => new AnnotationTypeOption
                {
                    Id = type.Id,
                    DisplayName = type.Name
                }));
            return options;
        }

        private IList<AnnotationTypeOption> CollectTagTypes(BuiltInCategory category)
        {
            List<AnnotationTypeOption> options = new List<AnnotationTypeOption>();
            options.Add(CreateNoneOption());
            options.AddRange(new FilteredElementCollector(_document)
                .OfCategory(category)
                .WhereElementIsElementType()
                .Cast<ElementType>()
                .OrderBy(type => type.Name)
                .Select(type => new AnnotationTypeOption
                {
                    Id = type.Id,
                    DisplayName = BuildTypeDisplayName(type)
                }));
            return options;
        }

        private IList<AnnotationTypeOption> CollectLevels()
        {
            List<AnnotationTypeOption> options = new List<AnnotationTypeOption>();
            options.Add(CreateNoneOption());
            options.AddRange(new FilteredElementCollector(_document)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(level => level.Elevation)
                .ThenBy(level => level.Name)
                .Select(level => new AnnotationTypeOption
                {
                    Id = level.Id,
                    DisplayName = level.Name
                }));
            return options;
        }

        private IList<AnnotationTypeOption> CollectDimensionTypes()
        {
            List<AnnotationTypeOption> options = new List<AnnotationTypeOption>();
            options.Add(CreateNoneOption());
            options.AddRange(new FilteredElementCollector(_document)
                .OfClass(typeof(DimensionType))
                .Cast<DimensionType>()
                .Where(type => type.StyleType == DimensionStyleType.Linear)
                .OrderBy(type => type.Name)
                .Select(type => new AnnotationTypeOption
                {
                    Id = type.Id,
                    DisplayName = type.Name
                }));
            return options;
        }

        private string BuildTypeDisplayName(ElementType type)
        {
            FamilySymbol symbol = type as FamilySymbol;
            return symbol != null
                ? symbol.FamilyName + " : " + symbol.Name
                : type.Name;
        }

        private AnnotationTypeOption CreateNoneOption()
        {
            return new AnnotationTypeOption
            {
                Id = ElementId.InvalidElementId,
                DisplayName = "<Не выбран>"
            };
        }

        private void SetItems(ComboBox comboBox, IList<AnnotationTypeOption> options)
        {
            comboBox.ItemsSource = options;
            comboBox.SelectedIndex = 0;
        }

        private void ApplyInitialSettings()
        {
            ElevationDecorationSettings settings = _initialSettings ?? CreateDefaultSettings();
            _placeSpotElevations.IsChecked = settings.PlaceSpotElevations;
            _placeTags.IsChecked = settings.PlaceTags;
            _placeDimensions.IsChecked = settings.PlaceDimensions;
            _placeWallTags.IsChecked = settings.PlaceWallTags;
            _placeFloorTags.IsChecked = settings.PlaceFloorTags;
            _placeCeilingTags.IsChecked = settings.PlaceCeilingTags;
            _placePlinthTags.IsChecked = settings.PlacePlinthTags;
            _placeDoorTags.IsChecked = settings.PlaceDoorTags;
            _placeWindowTags.IsChecked = settings.PlaceWindowTags;
            _placeVerticalDimensions.IsChecked = settings.PlaceVerticalDimensions;
            _placeHorizontalDimensions.IsChecked = settings.PlaceHorizontalDimensions;
            _placeFinishFloorSpot.IsChecked = settings.PlaceFinishFloorSpotElevation;
            _placeSubfloorSpots.IsChecked = settings.PlaceSubfloorSpotElevations;
            _placeStructuralBaseSpot.IsChecked = settings.PlaceStructuralBaseSpotElevation;
            _leftSide.IsChecked = settings.AnnotationSide == ElevationAnnotationSide.Left;
            _rightSide.IsChecked = settings.AnnotationSide == ElevationAnnotationSide.Right;
            _spotOffset.Text = FormatDouble(settings.SpotOffsetPaperMm);
            _tagOffset.Text = FormatDouble(settings.TagOffsetPaperMm);
            _detailedDimensionOffset.Text = FormatDouble(settings.DetailedDimensionOffsetPaperMm);
            _overallDimensionOffset.Text = FormatDouble(settings.OverallDimensionOffsetPaperMm);
            _widthDimensionOffset.Text = FormatDouble(settings.WidthDimensionOffsetPaperMm);
            SelectOption(_floorSpotType, settings.FloorSpotElevationTypeId);
            SelectOption(_overheadSpotType, settings.OverheadSpotElevationTypeId);
            SelectOption(_spotRelativeBaseLevel, settings.SpotElevationRelativeBaseLevelId);
            SelectOption(_detailedDimensionType, settings.DetailedDimensionTypeId);
            SelectOption(_overallDimensionType, settings.OverallDimensionTypeId);
            SelectOption(_widthDimensionType, settings.WidthDimensionTypeId);
            SelectOption(_wallTagType, settings.WallTagTypeId);
            SelectOption(_floorTagType, settings.FloorTagTypeId);
            SelectOption(_ceilingTagType, settings.CeilingTagTypeId);
            SelectOption(_plinthTagType, settings.PlinthTagTypeId);
            SelectOption(_doorTagType, settings.DoorTagTypeId);
            SelectOption(_windowTagType, settings.WindowTagTypeId);
            _wallLeader.IsChecked = true;
            _floorLeader.IsChecked = settings.FloorTagHasLeader;
            _ceilingLeader.IsChecked = settings.CeilingTagHasLeader;
            _plinthLeader.IsChecked = settings.PlinthTagHasLeader;
            _doorLeader.IsChecked = settings.DoorTagHasLeader;
            _windowLeader.IsChecked = settings.WindowTagHasLeader;
        }

        private ElevationDecorationSettings CreateDefaultSettings()
        {
            return new ElevationDecorationSettings
            {
                PlaceSpotElevations = true,
                PlaceTags = false,
                PlaceDimensions = false,
                PlaceWallTags = true,
                PlaceFloorTags = true,
                PlaceCeilingTags = true,
                PlacePlinthTags = true,
                PlaceDoorTags = true,
                PlaceWindowTags = true,
                PlaceVerticalDimensions = true,
                PlaceHorizontalDimensions = true,
                PlaceFinishFloorSpotElevation = true,
                PlaceSubfloorSpotElevations = false,
                PlaceStructuralBaseSpotElevation = true,
                AnnotationSide = ElevationAnnotationSide.Left,
                SpotOffsetPaperMm = 12.0,
                TagOffsetPaperMm = 6.0,
                DetailedDimensionOffsetPaperMm = 8.0,
                OverallDimensionOffsetPaperMm = 16.0,
                WidthDimensionOffsetPaperMm = 10.0,
                FloorTagHasLeader = true,
                CeilingTagHasLeader = true,
                PlinthTagHasLeader = true,
                SpotElevationRelativeBaseLevelId = ElementId.InvalidElementId
            };
        }

        private void SelectOption(ComboBox comboBox, ElementId elementId)
        {
            if (comboBox.ItemsSource == null || elementId == null)
            {
                return;
            }

            foreach (AnnotationTypeOption option in comboBox.ItemsSource.Cast<AnnotationTypeOption>())
            {
                if (RevitElementIdUtils.AreEqual(option.Id, elementId))
                {
                    comboBox.SelectedItem = option;
                    return;
                }
            }
        }

        private void AttachHandlers()
        {
            _titleBar.MouseLeftButtonDown += TitleBar_MouseLeftButtonDown;
            _minimizeButton.Click += delegate { WindowState = WindowState.Minimized; };
            _closeButton.Click += CancelButton_Click;
            _cancelButton.Click += CancelButton_Click;
            _okButton.Click += OkButton_Click;
            _configureCatalogButton.Click += ConfigureCatalogButton_Click;
            Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SabWindowBehaviorService.ApplyLoadedBehavior(this);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
                return;
            }

            DragMove();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            List<string> validationErrors = new List<string>();
            IList<ElevationDecorationCatalogRole> missingRoles = GetMissingCatalogRoles();
            if (missingRoles.Count > 0)
            {
                validationErrors.Add(
                    "Каталог: не заполнены роли — " +
                    string.Join(", ", missingRoles.Select(
                        ElevationDecorationCatalogRoleNames.GetDisplayName)) + ".");
            }

            bool placeSpotElevations = _placeSpotElevations.IsChecked == true;
            bool placeTags = _placeTags.IsChecked == true;
            bool placeDimensions = _placeDimensions.IsChecked == true;
            bool placeWallTags = placeTags && _placeWallTags.IsChecked == true;
            bool placeFloorTags = placeTags && _placeFloorTags.IsChecked == true;
            bool placeCeilingTags = placeTags && _placeCeilingTags.IsChecked == true;
            bool placePlinthTags = placeTags && _placePlinthTags.IsChecked == true;
            bool placeDoorTags = placeTags && _placeDoorTags.IsChecked == true;
            bool placeWindowTags = placeTags && _placeWindowTags.IsChecked == true;
            bool placeVerticalDimensions = placeDimensions &&
                _placeVerticalDimensions.IsChecked == true;
            bool placeHorizontalDimensions = placeDimensions &&
                _placeHorizontalDimensions.IsChecked == true;
            if (!placeSpotElevations && !placeTags && !placeDimensions)
            {
                validationErrors.Add("Состав: выберите хотя бы один вид оформления.");
            }
            if (placeTags && !placeWallTags && !placeFloorTags &&
                !placeCeilingTags && !placePlinthTags && !placeDoorTags &&
                !placeWindowTags)
            {
                validationErrors.Add("Марки: выберите хотя бы одну категорию элементов.");
            }
            if (placeDimensions && !placeVerticalDimensions && !placeHorizontalDimensions)
            {
                validationErrors.Add("Размеры: выберите вертикальные или горизонтальные линии.");
            }

            double spotOffset = 0.0;
            double tagOffset = 0.0;
            double detailedDimensionOffset = 0.0;
            double overallDimensionOffset = 0.0;
            double widthDimensionOffset = 0.0;
            if (placeSpotElevations && !TryParseNonNegative(_spotOffset.Text, out spotOffset))
            {
                validationErrors.Add("Высотные отметки: задайте неотрицательный отступ.");
            }

            if (placeTags && !TryParseNonNegative(_tagOffset.Text, out tagOffset))
            {
                validationErrors.Add("Марки: задайте неотрицательный отступ.");
            }

            bool detailedDimensionOffsetValid = TryParseNonNegative(
                _detailedDimensionOffset.Text,
                out detailedDimensionOffset);
            if (placeVerticalDimensions && !detailedDimensionOffsetValid)
            {
                validationErrors.Add("Размеры: задайте неотрицательный отступ цепочки.");
            }

            bool overallDimensionOffsetValid = TryParseNonNegative(
                _overallDimensionOffset.Text,
                out overallDimensionOffset);
            if (placeVerticalDimensions && !overallDimensionOffsetValid)
            {
                validationErrors.Add("Размеры: задайте неотрицательный отступ общего размера.");
            }

            if (placeVerticalDimensions &&
                detailedDimensionOffsetValid &&
                overallDimensionOffsetValid &&
                overallDimensionOffset <= detailedDimensionOffset)
            {
                validationErrors.Add(
                    "Размеры: общий размер должен быть дальше от вида, чем цепочка.");
            }

            bool widthDimensionOffsetValid = TryParseNonNegative(
                _widthDimensionOffset.Text,
                out widthDimensionOffset);
            if (placeHorizontalDimensions && !widthDimensionOffsetValid)
            {
                validationErrors.Add("Размеры: задайте неотрицательный отступ ширины.");
            }

            if (placeSpotElevations)
            {
                AddMissingSelection(
                    validationErrors,
                    _floorSpotType,
                    "Высотные отметки: тип для пола",
                    "типы высотных отметок");
                AddMissingSelection(
                    validationErrors,
                    _overheadSpotType,
                    "Высотные отметки: тип для потолка/ЖБ-перекрытия",
                    "типы высотных отметок");
                AddMissingSelection(
                    validationErrors,
                    _spotRelativeBaseLevel,
                    "Высотные отметки: относительная база",
                    "уровни");
            }

            if (placeVerticalDimensions)
            {
                AddMissingSelection(validationErrors, _detailedDimensionType,
                    "Размеры: тип вертикальной цепочки", "линейные типы размеров");
                AddMissingSelection(validationErrors, _overallDimensionType,
                    "Размеры: тип общего размера", "линейные типы размеров");
            }
            if (placeHorizontalDimensions)
            {
                AddMissingSelection(validationErrors, _widthDimensionType,
                    "Размеры: тип размера ширины", "линейные типы размеров");
            }

            if (placeTags)
            {
                if (placeWallTags)
                    AddMissingTagSelection(validationErrors, _wallTagType, "стен");
                if (placeFloorTags)
                    AddMissingTagSelection(validationErrors, _floorTagType, "полов");
                if (placeCeilingTags)
                    AddMissingTagSelection(validationErrors, _ceilingTagType, "потолков");
                if (placePlinthTags)
                    AddMissingTagSelection(validationErrors, _plinthTagType, "плинтусов");
                if (placeDoorTags)
                    AddMissingTagSelection(validationErrors, _doorTagType, "дверей");
                if (placeWindowTags)
                    AddMissingTagSelection(validationErrors, _windowTagType, "окон");
            }

            if (validationErrors.Count > 0)
            {
                ShowValidationErrors(validationErrors);
                return;
            }

            SelectedSettings = new ElevationDecorationSettings
            {
                PlaceSpotElevations = _placeSpotElevations.IsChecked == true,
                PlaceTags = _placeTags.IsChecked == true,
                PlaceDimensions = _placeDimensions.IsChecked == true,
                PlaceWallTags = placeWallTags,
                PlaceFloorTags = placeFloorTags,
                PlaceCeilingTags = placeCeilingTags,
                PlacePlinthTags = placePlinthTags,
                PlaceDoorTags = placeDoorTags,
                PlaceWindowTags = placeWindowTags,
                PlaceVerticalDimensions = placeVerticalDimensions,
                PlaceHorizontalDimensions = placeHorizontalDimensions,
                PlaceFinishFloorSpotElevation = _placeFinishFloorSpot.IsChecked == true,
                PlaceSubfloorSpotElevations = _placeSubfloorSpots.IsChecked == true,
                PlaceStructuralBaseSpotElevation = _placeStructuralBaseSpot.IsChecked == true,
                AnnotationSide = _rightSide.IsChecked == true
                    ? ElevationAnnotationSide.Right
                    : ElevationAnnotationSide.Left,
                SpotOffsetPaperMm = spotOffset,
                TagOffsetPaperMm = tagOffset,
                DetailedDimensionOffsetPaperMm = detailedDimensionOffset,
                OverallDimensionOffsetPaperMm = overallDimensionOffset,
                WidthDimensionOffsetPaperMm = widthDimensionOffset,
                FloorSpotElevationTypeId = GetSelectedId(_floorSpotType),
                OverheadSpotElevationTypeId = GetSelectedId(_overheadSpotType),
                SpotElevationRelativeBaseLevelId = GetSelectedId(_spotRelativeBaseLevel),
                DetailedDimensionTypeId = GetSelectedId(_detailedDimensionType),
                OverallDimensionTypeId = GetSelectedId(_overallDimensionType),
                WidthDimensionTypeId = GetSelectedId(_widthDimensionType),
                WallTagTypeId = GetSelectedId(_wallTagType),
                FloorTagTypeId = GetSelectedId(_floorTagType),
                CeilingTagTypeId = GetSelectedId(_ceilingTagType),
                PlinthTagTypeId = GetSelectedId(_plinthTagType),
                DoorTagTypeId = GetSelectedId(_doorTagType),
                WindowTagTypeId = GetSelectedId(_windowTagType),
                WallTagHasLeader = _wallLeader.IsChecked == true,
                FloorTagHasLeader = _floorLeader.IsChecked == true,
                CeilingTagHasLeader = _ceilingLeader.IsChecked == true,
                PlinthTagHasLeader = _plinthLeader.IsChecked == true,
                DoorTagHasLeader = _doorLeader.IsChecked == true,
                WindowTagHasLeader = _windowLeader.IsChecked == true
            };

            DialogResult = true;
            Close();
        }

        private void AddMissingTagSelection(
            ICollection<string> validationErrors,
            ComboBox comboBox,
            string categoryName)
        {
            AddMissingSelection(
                validationErrors,
                comboBox,
                "Марки: тип марки для " + categoryName,
                "типы марок для " + categoryName);
        }

        private void AddMissingSelection(
            ICollection<string> validationErrors,
            ComboBox comboBox,
            string fieldName,
            string loadedTypeName)
        {
            if (GetSelectedId(comboBox) != ElementId.InvalidElementId)
            {
                return;
            }

            int availableCount = comboBox != null ? comboBox.Items.Count - 1 : 0;
            validationErrors.Add(availableCount > 0
                ? fieldName + " — не выбран."
                : fieldName + " — в проекте нет загруженных элементов (" + loadedTypeName + ").");
        }

        private void ShowValidationErrors(IList<string> validationErrors)
        {
            string details = string.Join(
                Environment.NewLine,
                validationErrors.Select(error => "• " + error));
            if (_validationStatusText != null)
            {
                _validationStatusText.Text = "Оформление не запущено: не хватает настроек (" +
                    validationErrors.Count + ").";
                _validationStatusText.Foreground = new SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(180, 35, 24));
                _validationStatusText.ToolTip = details;
            }

            MessageBox.Show(
                this,
                "Оформление не запущено. Нужно исправить:" +
                Environment.NewLine + Environment.NewLine + details,
                "SAB — проверка настроек",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void ConfigureCatalogButton_Click(object sender, RoutedEventArgs e)
        {
            OpenCatalogRequested = true;
            DialogResult = false;
            Close();
        }

        private void RefreshCatalogStatus()
        {
            if (_catalogStatusText == null || _catalogDetailsText == null)
            {
                return;
            }

            IList<ElevationDecorationCatalogRole> missing = GetMissingCatalogRoles();
            int total = _catalog != null && _catalog.Entries != null
                ? _catalog.Entries.Count
                : 0;
            _catalogStatusText.Text = missing.Count == 0
                ? "Каталог заполнен. Оформление разрешено."
                : "Каталог не заполнен. Оформление заблокировано.";
            _catalogDetailsText.Text = missing.Count == 0
                ? "Типоразмеров в каталоге: " + total + "."
                : "Не заполнены роли: " + string.Join(
                    ", ",
                    missing.Select(ElevationDecorationCatalogRoleNames.GetDisplayName)) + ".";
        }

        private IList<ElevationDecorationCatalogRole> GetMissingCatalogRoles()
        {
            ElevationDecorationCatalogService service =
                new ElevationDecorationCatalogService();
            return Enum.GetValues(typeof(ElevationDecorationCatalogRole))
                .Cast<ElevationDecorationCatalogRole>()
                .Where(role => !service.HasRole(_catalog, role))
                .ToList();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private ElementId GetSelectedId(ComboBox comboBox)
        {
            AnnotationTypeOption option = comboBox.SelectedItem as AnnotationTypeOption;
            return option != null && option.Id != null
                ? option.Id
                : ElementId.InvalidElementId;
        }

        private bool TryParseNonNegative(string value, out double result)
        {
            bool parsed = double.TryParse(
                value,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.CurrentCulture,
                out result) ||
                double.TryParse(
                    value,
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture,
                    out result);
            return parsed && result >= 0.0;
        }

        private string FormatDouble(double value)
        {
            return value.ToString("0.###", CultureInfo.CurrentCulture);
        }

        private T Find<T>(string name) where T : FrameworkElement
        {
            return FindElementByName<T>(Content as DependencyObject, name);
        }

        private T FindElementByName<T>(DependencyObject root, string name) where T : FrameworkElement
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
                DependencyObject child = childObject as DependencyObject;
                if (child == null)
                {
                    continue;
                }

                T result = FindElementByName<T>(child, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }
}
