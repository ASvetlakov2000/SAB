using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Elevations;
using SAB.InteriorElevations.Services.Marks;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.ViewModels
{
    public class RevitElementOption
    {
        public ElementId Id { get; set; }

        public string DisplayName { get; set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public class ViewTitleAnchorOption
    {
        public ViewTitleAnchor Value { get; set; }

        public string DisplayName { get; set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public class ElevationNamingPreviewContext
    {
        public RoomData RoomData { get; set; }

        public int StartPointNumber { get; set; }

        public int EndPointNumber { get; set; }
    }

    public class ElevationNamingPreviewItem
    {
        public string RoomName { get; set; }
        public string Corners { get; set; }
        public string ViewName { get; set; }

        public string ViewTitle { get; set; }
    }

    public class ElevationSettingsViewModel : INotifyPropertyChanged
    {
        private readonly Document _document;
        private readonly List<ElevationNamingPreviewContext> _namingPreviewContexts;

        private RevitElementOption _selectedElevationViewFamilyType;
        private RevitElementOption _selectedViewTemplate;
        private RevitElementOption _selectedTitleBlockType;
        private RevitElementOption _selectedViewportType;
        private RevitElementOption _selectedPlanCornerMarkType;
        private RevitElementOption _selectedSheetCornerMarkType;
        private RevitElementOption _selectedRoomPlanViewTemplate;
        private RevitElementOption _selectedRoomPlanRoomTagType;
        private RevitElementOption _selectedExistingSheet;
        private ViewTitleAnchorOption _selectedViewTitleAnchor;
        private bool _createSheet;
        private bool _useExistingSheet;
        private bool _isExistingSheetDropDownOpen;
        private bool _createRoomPlanScheme;
        private bool _placeRoomPlanSchemeOnSheet;
        private bool _multipleRoomsOnSheet;
        private bool _pickRoomFromLink;
        private bool _cornerMarksOnlyCornerNumber;
        private bool _sheetCornerMarksBelowView;
        private string _startXmmText;
        private string _startYmmText;
        private string _existingSheetSearchText;
        private ElementId _existingRoomPlanViewId = ElementId.InvalidElementId;
        private string _existingRoomPlanViewName;

        public ElevationSettingsViewModel(Document document, ElevationSettings initialSettings = null)
        {
            _document = document;

            ElevationViewFamilyTypes = new ObservableCollection<RevitElementOption>();
            ViewTemplates = new ObservableCollection<RevitElementOption>();
            TitleBlockTypes = new ObservableCollection<RevitElementOption>();
            ViewportTypes = new ObservableCollection<RevitElementOption>();
            PlanCornerMarkTypes = new ObservableCollection<RevitElementOption>();
            SheetCornerMarkTypes = new ObservableCollection<RevitElementOption>();
            RoomPlanViewTemplates = new ObservableCollection<RevitElementOption>();
            RoomPlanRoomTagTypes = new ObservableCollection<RevitElementOption>();
            ExistingSheets = new ObservableCollection<RevitElementOption>();
            ViewTitleAnchors = new ObservableCollection<ViewTitleAnchorOption>();
            NamingPreviewItems = new ObservableCollection<ElevationNamingPreviewItem>();
            _namingPreviewContexts = new List<ElevationNamingPreviewContext>();

            // Блок значений по умолчанию, которые пользователь может менять в окне.
            ViewScaleText = "50";
            TopOffsetMmText = "3000";
            BottomOffsetMmText = "0";
            LeftOffsetMmText = "100";
            RightOffsetMmText = "100";
            ViewDepthMmText = "3000";
            MarkerOffsetMmText = "250";
            GridTopExtensionPaperMmText = "5";
            GridBottomExtensionPaperMmText = "15";
            EnableRoomObjectCategory = true;
            ElevationNamePart1Text = "ELV_r";
            ElevationNamePart2Text = "[Номер помещения]_[Имя помещения]";
            ElevationNamePart3Text = "_Elev_[Начальный угол]-[Конечный угол]";
            ElevationTitlePart1Text = "Пом. №";
            ElevationTitlePart2Text = "[Номер помещения] [Имя помещения]. Вид ";
            ElevationTitlePart3Text = "[Начальный угол]-[Конечный угол]";

            ColumnsCountText = "2";
            StartXmmText = "150";
            StartYmmText = "200";
            StepXmmText = "180";
            StepYmmText = "140";
            SheetFormatAText = "3";
            SheetNamePart1Text = "Развертки стен пом. ";
            SheetNamePart2Text = "[Помещения]";
            SheetNamePart3Text = string.Empty;
            ViewTitleOffsetXmmText = "0";
            ViewTitleOffsetYmmText = "-5";

            // Блок настроек план-схемы, интегрированный в окно разверток.
            RoomPlanNamePart1Text = "План-схема разверток пом. ";
            RoomPlanNamePart2Text = "[Номер помещения]";
            RoomPlanNamePart3Text = string.Empty;
            RoomPlanViewScaleText = "20";
            RoomPlanCropOffsetMmText = "0";
            _createRoomPlanScheme = true;
            _placeRoomPlanSchemeOnSheet = true;

            LoadElevationViewFamilyTypes();
            LoadViewTemplates();
            LoadTitleBlockTypes();
            LoadViewportTypes();
            LoadCornerMarkTypes(PlanCornerMarkTypes);
            LoadCornerMarkTypes(SheetCornerMarkTypes);
            LoadRoomPlanViewTemplates();
            LoadRoomPlanRoomTagTypes();
            LoadExistingSheets();
            LoadViewTitleAnchors();

            ExistingSheetsView = CollectionViewSource.GetDefaultView(ExistingSheets);
            if (ExistingSheetsView != null)
            {
                ExistingSheetsView.Filter = FilterExistingSheet;
            }

            _createSheet = TitleBlockTypes.Count > 0 && ViewportTypes.Count > 0;

            // Блок восстановления последних сохраненных значений из предыдущей сессии.
            ApplyInitialSettings(initialSettings);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<RevitElementOption> ElevationViewFamilyTypes { get; private set; }

        public ObservableCollection<RevitElementOption> ViewTemplates { get; private set; }

        public ObservableCollection<RevitElementOption> TitleBlockTypes { get; private set; }

        public ObservableCollection<RevitElementOption> ViewportTypes { get; private set; }

        public ObservableCollection<RevitElementOption> PlanCornerMarkTypes { get; private set; }

        public ObservableCollection<RevitElementOption> SheetCornerMarkTypes { get; private set; }

        public ObservableCollection<RevitElementOption> RoomPlanViewTemplates { get; private set; }

        public ObservableCollection<RevitElementOption> RoomPlanRoomTagTypes { get; private set; }

        public ObservableCollection<RevitElementOption> ExistingSheets { get; private set; }

        public ICollectionView ExistingSheetsView { get; private set; }

        public ObservableCollection<ViewTitleAnchorOption> ViewTitleAnchors { get; private set; }

        public ObservableCollection<ElevationNamingPreviewItem> NamingPreviewItems { get; private set; }

        public bool HasNamingPreview
        {
            get { return NamingPreviewItems.Count > 0; }
        }

        public string NamingPreviewStatusText
        {
            get
            {
                return HasNamingPreview
                    ? "Предпросмотр: " + NamingPreviewItems.Count + ". Имена проверяются повторно при создании."
                    : "Выберите линии и помещение — здесь появятся итоговые имена и заголовки.";
            }
        }

        public RevitElementOption SelectedElevationViewFamilyType
        {
            get { return _selectedElevationViewFamilyType; }
            set
            {
                _selectedElevationViewFamilyType = value;
                OnPropertyChanged("SelectedElevationViewFamilyType");
            }
        }

        public RevitElementOption SelectedViewTemplate
        {
            get { return _selectedViewTemplate; }
            set
            {
                _selectedViewTemplate = value;
                OnPropertyChanged("SelectedViewTemplate");
            }
        }

        public RevitElementOption SelectedTitleBlockType
        {
            get { return _selectedTitleBlockType; }
            set
            {
                _selectedTitleBlockType = value;
                OnPropertyChanged("SelectedTitleBlockType");
            }
        }

        public RevitElementOption SelectedViewportType
        {
            get { return _selectedViewportType; }
            set
            {
                _selectedViewportType = value;
                OnPropertyChanged("SelectedViewportType");
            }
        }

        public RevitElementOption SelectedPlanCornerMarkType
        {
            get { return _selectedPlanCornerMarkType; }
            set
            {
                _selectedPlanCornerMarkType = value;
                OnPropertyChanged("SelectedPlanCornerMarkType");
            }
        }

        public RevitElementOption SelectedSheetCornerMarkType
        {
            get { return _selectedSheetCornerMarkType; }
            set
            {
                _selectedSheetCornerMarkType = value;
                OnPropertyChanged("SelectedSheetCornerMarkType");
            }
        }

        public RevitElementOption SelectedRoomPlanViewTemplate
        {
            get { return _selectedRoomPlanViewTemplate; }
            set
            {
                _selectedRoomPlanViewTemplate = value;
                OnPropertyChanged("SelectedRoomPlanViewTemplate");
            }
        }

        public RevitElementOption SelectedRoomPlanRoomTagType
        {
            get { return _selectedRoomPlanRoomTagType; }
            set
            {
                _selectedRoomPlanRoomTagType = value;
                OnPropertyChanged("SelectedRoomPlanRoomTagType");
            }
        }

        public ViewTitleAnchorOption SelectedViewTitleAnchor
        {
            get { return _selectedViewTitleAnchor; }
            set
            {
                _selectedViewTitleAnchor = value;
                OnPropertyChanged("SelectedViewTitleAnchor");
            }
        }

        public string ViewScaleText { get; set; }

        public string TopOffsetMmText { get; set; }

        public string BottomOffsetMmText { get; set; }

        public string LeftOffsetMmText { get; set; }

        public string RightOffsetMmText { get; set; }

        public string ViewDepthMmText { get; set; }

        public string MarkerOffsetMmText { get; set; }

        public string GridTopExtensionPaperMmText { get; set; }

        public string GridBottomExtensionPaperMmText { get; set; }

        public string ElevationNamePart1Text { get; set; }

        public string ElevationNamePart2Text { get; set; }

        public string ElevationNamePart3Text { get; set; }

        public string ElevationNameFormulaText
        {
            get { return NormalizeFormulaSyntax((ElevationNamePart1Text ?? string.Empty) + (ElevationNamePart2Text ?? string.Empty) + (ElevationNamePart3Text ?? string.Empty)); }
            set
            {
                ElevationNamePart1Text = NormalizeFormulaSyntax(value);
                ElevationNamePart2Text = string.Empty;
                ElevationNamePart3Text = string.Empty;
                OnPropertyChanged("ElevationNameFormulaText");
                RefreshNamingPreview();
            }
        }

        public string ElevationTitlePart1Text { get; set; }

        public string ElevationTitlePart2Text { get; set; }

        public string ElevationTitlePart3Text { get; set; }

        public string ElevationTitleFormulaText
        {
            get { return NormalizeFormulaSyntax((ElevationTitlePart1Text ?? string.Empty) + (ElevationTitlePart2Text ?? string.Empty) + (ElevationTitlePart3Text ?? string.Empty)); }
            set
            {
                ElevationTitlePart1Text = NormalizeFormulaSyntax(value);
                ElevationTitlePart2Text = string.Empty;
                ElevationTitlePart3Text = string.Empty;
                OnPropertyChanged("ElevationTitleFormulaText");
                RefreshNamingPreview();
            }
        }

        public bool CreateSheet
        {
            get { return _createSheet; }
            set
            {
                if (_createSheet == value)
                {
                    return;
                }

                _createSheet = value;
                if (value)
                {
                    _useExistingSheet = false;
                    ClearExistingRoomPlanViewSelection();
                }

                OnPropertyChanged("CreateSheet");
                OnPropertyChanged("UseExistingSheet");
                OnPropertyChanged("CreateNewSheetMode");
                OnPropertyChanged("ExistingSheetMode");
                OnPropertyChanged("CreateRoomPlanScheme");
                OnPropertyChanged("PlaceViewsOnSheet");
                OnPropertyChanged("DoNotPlaceViewsOnSheetMode");
                OnPropertyChanged("CanPlaceRoomPlanSchemeOnSheet");
                OnPropertyChanged("CanPositionRoomPlan");
                OnPropertyChanged("PickSheetPointButtonText");
                OnPropertyChanged("RoomPlanOperationLabel");
                OnPropertyChanged("RoomPlanModeHint");
                OnPropertyChanged("CanEditRoomPlanCreationParameters");
            }
        }

        public bool UseExistingSheet
        {
            get { return _useExistingSheet; }
            set
            {
                if (_useExistingSheet == value)
                {
                    return;
                }

                _useExistingSheet = value;
                if (value)
                {
                    _createSheet = false;
                }
                else
                {
                    ClearExistingRoomPlanViewSelection();
                }

                OnPropertyChanged("UseExistingSheet");
                OnPropertyChanged("CreateSheet");
                OnPropertyChanged("ExistingSheetMode");
                OnPropertyChanged("CreateNewSheetMode");
                OnPropertyChanged("CreateRoomPlanScheme");
                OnPropertyChanged("PlaceViewsOnSheet");
                OnPropertyChanged("DoNotPlaceViewsOnSheetMode");
                OnPropertyChanged("CanPlaceRoomPlanSchemeOnSheet");
                OnPropertyChanged("CanPositionRoomPlan");
                OnPropertyChanged("PickSheetPointButtonText");
                OnPropertyChanged("RoomPlanOperationLabel");
                OnPropertyChanged("RoomPlanModeHint");
                OnPropertyChanged("CanEditRoomPlanCreationParameters");
            }
        }

        public bool CreateNewSheetMode
        {
            get { return CreateSheet; }
            set
            {
                if (value)
                {
                    CreateSheet = true;
                }
            }
        }

        public bool ExistingSheetMode
        {
            get { return UseExistingSheet; }
            set
            {
                if (value)
                {
                    UseExistingSheet = true;
                }
            }
        }

        public bool DoNotPlaceViewsOnSheetMode
        {
            get { return !PlaceViewsOnSheet; }
            set
            {
                if (value)
                {
                    PlaceViewsOnSheet = false;
                }
            }
        }

        public bool PlaceViewsOnSheet
        {
            get { return CreateSheet || UseExistingSheet; }
            set
            {
                if (value)
                {
                    if (!CreateSheet && !UseExistingSheet)
                    {
                        CreateSheet = true;
                    }
                }
                else
                {
                    bool changed = _createSheet || _useExistingSheet;
                    _createSheet = false;
                    _useExistingSheet = false;
                    ClearExistingRoomPlanViewSelection();
                    if (changed)
                    {
                        OnPropertyChanged("CreateSheet");
                        OnPropertyChanged("UseExistingSheet");
                        OnPropertyChanged("CreateNewSheetMode");
                        OnPropertyChanged("ExistingSheetMode");
                    }
                }

                OnPropertyChanged("PlaceViewsOnSheet");
                OnPropertyChanged("DoNotPlaceViewsOnSheetMode");
                OnPropertyChanged("CanPlaceRoomPlanSchemeOnSheet");
                OnPropertyChanged("CanPositionRoomPlan");
                OnPropertyChanged("PickSheetPointButtonText");
                OnPropertyChanged("RoomPlanOperationLabel");
                OnPropertyChanged("RoomPlanModeHint");
                OnPropertyChanged("CanEditRoomPlanCreationParameters");
            }
        }

        public RevitElementOption SelectedExistingSheet
        {
            get { return _selectedExistingSheet; }
            set
            {
                if (ReferenceEquals(_selectedExistingSheet, value))
                {
                    return;
                }

                _selectedExistingSheet = value;
                ClearExistingRoomPlanViewSelection();
                if (value != null &&
                    !string.Equals(_existingSheetSearchText, value.DisplayName, StringComparison.Ordinal))
                {
                    _existingSheetSearchText = value.DisplayName;
                    OnPropertyChanged("ExistingSheetSearchText");
                }

                OnPropertyChanged("SelectedExistingSheet");
            }
        }

        public ElementId ExistingRoomPlanViewId
        {
            get { return _existingRoomPlanViewId; }
        }

        public void SelectSuggestedExistingSheet(ElementId sheetId)
        {
            RevitElementOption sheetOption = FindOptionById(ExistingSheets, sheetId);
            if (sheetOption != null)
            {
                SelectedExistingSheet = sheetOption;
            }
        }

        public string ExistingRoomPlanSummary
        {
            get
            {
                return HasExistingRoomPlanSelection
                    ? _existingRoomPlanViewName
                    : "не выбрана";
            }
        }

        public bool HasExistingRoomPlanSelection
        {
            get
            {
                return _existingRoomPlanViewId != null &&
                       !RevitElementIdUtils.AreEqual(_existingRoomPlanViewId, ElementId.InvalidElementId);
            }
        }

        public string PickSheetPointButtonText
        {
            get
            {
                return UseExistingSheet && CreateRoomPlanScheme
                    ? "Указать точку и план-схему"
                    : "Указать на листе";
            }
        }

        public string RoomPlanOperationLabel
        {
            get
            {
                return UseExistingSheet
                    ? "Добавить марки на существующую план-схему"
                    : "Создать план-схему помещения";
            }
        }

        public string RoomPlanModeHint
        {
            get
            {
                return UseExistingSheet
                    ? "Выбранная план-схема сохраняет положение. Её копирование на листы продолжения можно отключить."
                    : "Создание план-схемы и её размещение на листах включаются отдельно.";
            }
        }

        public string ExistingSheetSearchText
        {
            get { return _existingSheetSearchText; }
            set
            {
                if (string.Equals(_existingSheetSearchText, value, StringComparison.Ordinal))
                {
                    return;
                }

                _existingSheetSearchText = value;
                if (_selectedExistingSheet != null &&
                    !string.Equals(value, _selectedExistingSheet.DisplayName, StringComparison.Ordinal))
                {
                    _selectedExistingSheet = null;
                    ClearExistingRoomPlanViewSelection();
                    OnPropertyChanged("SelectedExistingSheet");
                }

                OnPropertyChanged("ExistingSheetSearchText");
                if (ExistingSheetsView != null)
                {
                    ExistingSheetsView.Refresh();
                }

                if (UseExistingSheet)
                {
                    IsExistingSheetDropDownOpen = true;
                }
            }
        }

        public bool IsExistingSheetDropDownOpen
        {
            get { return _isExistingSheetDropDownOpen; }
            set
            {
                if (_isExistingSheetDropDownOpen == value)
                {
                    return;
                }

                _isExistingSheetDropDownOpen = value;
                OnPropertyChanged("IsExistingSheetDropDownOpen");
            }
        }

        public bool OpenCreatedSheet { get; set; }

        public bool PickRoomFromLink
        {
            get { return _pickRoomFromLink; }
            set
            {
                if (_pickRoomFromLink == value)
                {
                    return;
                }

                _pickRoomFromLink = value;
                OnPropertyChanged("PickRoomFromLink");
                OnPropertyChanged("PickRoomFromHost");
            }
        }

        public bool PickRoomFromHost
        {
            get { return !PickRoomFromLink; }
            set
            {
                if (value)
                {
                    PickRoomFromLink = false;
                }
            }
        }

        public bool EnableRoomObjectCategory { get; set; }

        public bool CornerMarksWithRoomNumber
        {
            get { return !CornerMarksOnlyCornerNumber; }
            set
            {
                if (value)
                {
                    CornerMarksOnlyCornerNumber = false;
                }
            }
        }

        public bool CornerMarksOnlyCornerNumber
        {
            get { return _cornerMarksOnlyCornerNumber; }
            set
            {
                if (_cornerMarksOnlyCornerNumber == value)
                {
                    return;
                }

                _cornerMarksOnlyCornerNumber = value;
                OnPropertyChanged("CornerMarksOnlyCornerNumber");
                OnPropertyChanged("CornerMarksWithRoomNumber");
            }
        }

        public bool SheetCornerMarksAboveView
        {
            get { return !SheetCornerMarksBelowView; }
            set
            {
                if (value)
                {
                    SheetCornerMarksBelowView = false;
                }
            }
        }

        public bool SheetCornerMarksBelowView
        {
            get { return _sheetCornerMarksBelowView; }
            set
            {
                if (_sheetCornerMarksBelowView == value)
                {
                    return;
                }

                _sheetCornerMarksBelowView = value;
                OnPropertyChanged("SheetCornerMarksBelowView");
                OnPropertyChanged("SheetCornerMarksAboveView");
            }
        }

        public ElevationMarkPreferences CaptureMarkPreferences()
        {
            return new ElevationMarkPreferences
            {
                OnlyCornerNumber = CornerMarksOnlyCornerNumber,
                BelowView = SheetCornerMarksBelowView,
                PlanMarkTypeId = SelectedPlanCornerMarkType != null
                    ? SelectedPlanCornerMarkType.Id
                    : ElementId.InvalidElementId,
                SheetMarkTypeId = SelectedSheetCornerMarkType != null
                    ? SelectedSheetCornerMarkType.Id
                    : ElementId.InvalidElementId
            };
        }

        public void ApplyMarkPreferences(ElevationMarkPreferences preferences)
        {
            if (preferences == null)
            {
                return;
            }

            CornerMarksOnlyCornerNumber = preferences.OnlyCornerNumber;
            SheetCornerMarksBelowView = preferences.BelowView;

            RevitElementOption planType = FindOptionById(PlanCornerMarkTypes, preferences.PlanMarkTypeId);
            if (planType != null)
            {
                SelectedPlanCornerMarkType = planType;
            }

            RevitElementOption sheetType = FindOptionById(SheetCornerMarkTypes, preferences.SheetMarkTypeId);
            if (sheetType != null)
            {
                SelectedSheetCornerMarkType = sheetType;
            }
        }

        public bool IsSingleRoomPerSheet
        {
            get { return !IsMultipleRoomsOnSheet; }
            set
            {
                if (value)
                {
                    IsMultipleRoomsOnSheet = false;
                }
            }
        }

        public bool IsMultipleRoomsOnSheet
        {
            get { return _multipleRoomsOnSheet; }
            set
            {
                if (_multipleRoomsOnSheet == value)
                {
                    return;
                }

                _multipleRoomsOnSheet = value;
                OnPropertyChanged("IsMultipleRoomsOnSheet");
                OnPropertyChanged("IsSingleRoomPerSheet");
            }
        }

        public string ColumnsCountText { get; set; }

        private bool _useAutomaticSheetPlacement = true;

        public bool UseAutomaticSheetPlacement
        {
            get { return _useAutomaticSheetPlacement; }
            set
            {
                if (_useAutomaticSheetPlacement == value) return;
                _useAutomaticSheetPlacement = value;
                OnPropertyChanged("UseAutomaticSheetPlacement");
                OnPropertyChanged("UseManualSheetPlacement");
            }
        }

        public bool UseManualSheetPlacement
        {
            get { return !UseAutomaticSheetPlacement; }
            set { UseAutomaticSheetPlacement = !value; }
        }

        public string StartXmmText
        {
            get { return _startXmmText; }
            set
            {
                if (string.Equals(_startXmmText, value, StringComparison.Ordinal))
                {
                    return;
                }

                _startXmmText = value;
                OnPropertyChanged("StartXmmText");
                OnPropertyChanged("SheetPointSummary");
            }
        }

        public string StartYmmText
        {
            get { return _startYmmText; }
            set
            {
                if (string.Equals(_startYmmText, value, StringComparison.Ordinal))
                {
                    return;
                }

                _startYmmText = value;
                OnPropertyChanged("StartYmmText");
                OnPropertyChanged("SheetPointSummary");
            }
        }

        public string SheetPointSummary
        {
            get { return "X: " + StartXmmText + " мм | Y: " + StartYmmText + " мм"; }
        }

        public string StepXmmText { get; set; }

        public string StepYmmText { get; set; }

        private bool _useManualRoomPlanPosition;
        public bool UseManualRoomPlanPosition
        {
            get { return _useManualRoomPlanPosition; }
            set { if (_useManualRoomPlanPosition == value) return; _useManualRoomPlanPosition = value; OnPropertyChanged("UseManualRoomPlanPosition"); }
        }
        public bool CanPositionRoomPlan { get { return CanPlaceRoomPlanSchemeOnSheet && PlaceRoomPlanSchemeOnSheet; } }
        private string _roomPlanOffsetRightMmText = "0";
        private string _roomPlanOffsetBottomMmText = "0";
        public string RoomPlanOffsetRightMmText
        {
            get { return _roomPlanOffsetRightMmText; }
            set { _roomPlanOffsetRightMmText = value; OnPropertyChanged("RoomPlanOffsetRightMmText"); }
        }
        public string RoomPlanOffsetBottomMmText
        {
            get { return _roomPlanOffsetBottomMmText; }
            set { _roomPlanOffsetBottomMmText = value; OnPropertyChanged("RoomPlanOffsetBottomMmText"); }
        }
        public void SetRoomPlanPosition(double rightMm, double bottomMm)
        {
            RoomPlanOffsetRightMmText = FormatDouble(rightMm);
            RoomPlanOffsetBottomMmText = FormatDouble(bottomMm);
            UseManualRoomPlanPosition = true;
        }

        public string SheetFormatAText { get; set; }

        public string SheetNamePart1Text { get; set; }

        public string SheetNamePart2Text { get; set; }

        public string SheetNamePart3Text { get; set; }

        public string SheetNameFormulaText
        {
            get { return NormalizeFormulaSyntax((SheetNamePart1Text ?? string.Empty) + (SheetNamePart2Text ?? string.Empty) + (SheetNamePart3Text ?? string.Empty)); }
            set
            {
                SheetNamePart1Text = NormalizeFormulaSyntax(value);
                SheetNamePart2Text = string.Empty;
                SheetNamePart3Text = string.Empty;
                OnPropertyChanged("SheetNameFormulaText");
            }
        }

        public string ViewTitleOffsetXmmText { get; set; }

        public string ViewTitleOffsetYmmText { get; set; }

        public string RoomPlanNamePart1Text { get; set; }

        public string RoomPlanNamePart2Text { get; set; }

        public string RoomPlanNamePart3Text { get; set; }

        public string RoomPlanNameFormulaText
        {
            get { return NormalizeFormulaSyntax((RoomPlanNamePart1Text ?? string.Empty) + (RoomPlanNamePart2Text ?? string.Empty) + (RoomPlanNamePart3Text ?? string.Empty)); }
            set
            {
                RoomPlanNamePart1Text = NormalizeFormulaSyntax(value);
                RoomPlanNamePart2Text = string.Empty;
                RoomPlanNamePart3Text = string.Empty;
                OnPropertyChanged("RoomPlanNameFormulaText");
            }
        }

        public string RoomPlanViewScaleText { get; set; }

        public string RoomPlanCropOffsetMmText { get; set; }

        public bool CreateRoomPlanScheme
        {
            get { return _createRoomPlanScheme; }
            set
            {
                if (_createRoomPlanScheme == value)
                {
                    return;
                }

                _createRoomPlanScheme = value;
                if (!value)
                {
                    _placeRoomPlanSchemeOnSheet = false;
                    OnPropertyChanged("PlaceRoomPlanSchemeOnSheet");
                }

                OnPropertyChanged("CreateRoomPlanScheme");
                OnPropertyChanged("CanPlaceRoomPlanSchemeOnSheet");
                OnPropertyChanged("CanPositionRoomPlan");
                OnPropertyChanged("PickSheetPointButtonText");
                OnPropertyChanged("CanEditRoomPlanCreationParameters");
            }
        }

        public bool PlaceRoomPlanSchemeOnSheet
        {
            get { return _placeRoomPlanSchemeOnSheet; }
            set
            {
                bool normalizedValue = value && CreateRoomPlanScheme;
                if (_placeRoomPlanSchemeOnSheet == normalizedValue)
                {
                    return;
                }

                _placeRoomPlanSchemeOnSheet = normalizedValue;
                OnPropertyChanged("PlaceRoomPlanSchemeOnSheet");
                OnPropertyChanged("CanPlaceRoomPlanSchemeOnSheet");
                OnPropertyChanged("CanPositionRoomPlan");
            }
        }

        public bool CanPlaceRoomPlanSchemeOnSheet
        {
            get { return CreateRoomPlanScheme && PlaceViewsOnSheet; }
        }

        public bool CanEditRoomPlanCreationParameters
        {
            get { return CreateRoomPlanScheme && !UseExistingSheet; }
        }

        public void SetNamingPreviewContexts(IEnumerable<ElevationNamingPreviewContext> contexts)
        {
            _namingPreviewContexts.Clear();
            if (contexts != null)
            {
                foreach (ElevationNamingPreviewContext context in contexts)
                {
                    if (context != null && context.RoomData != null)
                    {
                        _namingPreviewContexts.Add(context);
                    }
                }
            }

            RefreshNamingPreview();
        }

        private void RefreshNamingPreview()
        {
            NamingPreviewItems.Clear();
            if (_namingPreviewContexts.Count > 0)
            {
                ElevationSettings previewSettings = new ElevationSettings();
                previewSettings.ElevationNamePart1 = ElevationNamePart1Text ?? string.Empty;
                previewSettings.ElevationNamePart2 = ElevationNamePart2Text ?? string.Empty;
                previewSettings.ElevationNamePart3 = ElevationNamePart3Text ?? string.Empty;
                previewSettings.ElevationTitlePart1 = ElevationTitlePart1Text ?? string.Empty;
                previewSettings.ElevationTitlePart2 = ElevationTitlePart2Text ?? string.Empty;
                previewSettings.ElevationTitlePart3 = ElevationTitlePart3Text ?? string.Empty;

                ElevationNamingService namingService = new ElevationNamingService(_document);
                foreach (ElevationNamingPreviewContext context in _namingPreviewContexts)
                {
                    ElevationNamingPreviewItem item = new ElevationNamingPreviewItem();
                    item.RoomName = context.RoomData.RoomNumber + " · " + context.RoomData.RoomName;
                    item.Corners = context.StartPointNumber + "–" + context.EndPointNumber;
                    item.ViewName = namingService.GenerateUniqueElevationViewName(
                        context.RoomData,
                        context.StartPointNumber,
                        context.EndPointNumber,
                        previewSettings);
                    item.ViewTitle = namingService.GenerateElevationTitleOnSheet(
                        context.RoomData,
                        context.StartPointNumber,
                        context.EndPointNumber,
                        previewSettings);
                    NamingPreviewItems.Add(item);
                }
            }

            OnPropertyChanged("HasNamingPreview");
            OnPropertyChanged("NamingPreviewStatusText");
        }

        public bool TryBuildSettings(
            out ElevationSettings settings,
            out string validationMessage,
            bool requireExistingRoomPlanSelection = true,
            bool requireRoomPlanPosition = true)
        {
            settings = null;
            validationMessage = ValidateInput(requireExistingRoomPlanSelection, requireRoomPlanPosition);
            if (!string.IsNullOrWhiteSpace(validationMessage))
            {
                return false;
            }

            int viewScale = ParseInt(ViewScaleText);

            ElevationSettings elevationSettings = new ElevationSettings();
            elevationSettings.ElevationViewFamilyTypeId = SelectedElevationViewFamilyType.Id;
            elevationSettings.ViewTemplateId = SelectedViewTemplate != null ? SelectedViewTemplate.Id : ElementId.InvalidElementId;
            elevationSettings.ViewScale = viewScale;

            elevationSettings.TopOffsetMm = ParseDouble(TopOffsetMmText);
            elevationSettings.BottomOffsetMm = ParseDouble(BottomOffsetMmText);
            elevationSettings.LeftOffsetMm = ParseDouble(LeftOffsetMmText);
            elevationSettings.RightOffsetMm = ParseDouble(RightOffsetMmText);
            elevationSettings.ViewDepthMm = ParseDouble(ViewDepthMmText);
            elevationSettings.MarkerOffsetMm = ParseDouble(MarkerOffsetMmText);
            elevationSettings.GridTopExtensionPaperMm = ParseDouble(GridTopExtensionPaperMmText);
            elevationSettings.GridBottomExtensionPaperMm = ParseDouble(GridBottomExtensionPaperMmText);
            elevationSettings.ElevationNamePart1 = ElevationNamePart1Text ?? string.Empty;
            elevationSettings.ElevationNamePart2 = ElevationNamePart2Text ?? string.Empty;
            elevationSettings.ElevationNamePart3 = ElevationNamePart3Text ?? string.Empty;
            elevationSettings.ElevationTitlePart1 = ElevationTitlePart1Text ?? string.Empty;
            elevationSettings.ElevationTitlePart2 = ElevationTitlePart2Text ?? string.Empty;
            elevationSettings.ElevationTitlePart3 = ElevationTitlePart3Text ?? string.Empty;

            elevationSettings.CreateSheet = CreateSheet;
            elevationSettings.UseExistingSheet = UseExistingSheet;
            elevationSettings.ExistingSheetId = UseExistingSheet && SelectedExistingSheet != null
                ? SelectedExistingSheet.Id
                : ElementId.InvalidElementId;
            elevationSettings.ExistingRoomPlanViewId = UseExistingSheet && CreateRoomPlanScheme
                ? ExistingRoomPlanViewId
                : ElementId.InvalidElementId;
            elevationSettings.OpenCreatedSheet = OpenCreatedSheet;
            elevationSettings.MultipleRoomsOnSheet = IsMultipleRoomsOnSheet;
            elevationSettings.PickRoomFromLink = PickRoomFromLink;
            elevationSettings.EnableRoomObjectCategory = EnableRoomObjectCategory;
            elevationSettings.TitleBlockTypeId = CreateSheet && SelectedTitleBlockType != null
                ? SelectedTitleBlockType.Id
                : ElementId.InvalidElementId;
            elevationSettings.ViewportTypeId = PlaceViewsOnSheet && SelectedViewportType != null
                ? SelectedViewportType.Id
                : ElementId.InvalidElementId;

            elevationSettings.PlanCornerMarkTypeId = SelectedPlanCornerMarkType != null
                ? SelectedPlanCornerMarkType.Id
                : ElementId.InvalidElementId;

            elevationSettings.CornerMarksOnlyCornerNumber = CornerMarksOnlyCornerNumber;
            elevationSettings.SheetCornerMarksBelowView = SheetCornerMarksBelowView;

            elevationSettings.SheetCornerMarkTypeId = PlaceViewsOnSheet && SelectedSheetCornerMarkType != null
                ? SelectedSheetCornerMarkType.Id
                : ElementId.InvalidElementId;
            elevationSettings.SheetFormatAValue = ParseNullableInt(SheetFormatAText);

            elevationSettings.SheetLayoutSettings = new SheetLayoutSettings();
            // Manual mode and automatic fallback use the same row capacity.
            elevationSettings.SheetLayoutSettings.ColumnsCount = ParseInt(ColumnsCountText);
            elevationSettings.SheetLayoutSettings.UseAutomaticPlacement = UseAutomaticSheetPlacement;
            elevationSettings.SheetLayoutSettings.StartXmm = ParseDouble(StartXmmText);
            elevationSettings.SheetLayoutSettings.StartYmm = ParseDouble(StartYmmText);
            elevationSettings.SheetLayoutSettings.StepXmm = ParseDouble(StepXmmText);
            elevationSettings.SheetLayoutSettings.StepYmm = ParseDouble(StepYmmText);
            elevationSettings.SheetLayoutSettings.UseManualRoomPlanPosition = UseManualRoomPlanPosition;
            elevationSettings.SheetLayoutSettings.RoomPlanOffsetRightMm = ParseDouble(RoomPlanOffsetRightMmText);
            elevationSettings.SheetLayoutSettings.RoomPlanOffsetBottomMm = ParseDouble(RoomPlanOffsetBottomMmText);
            elevationSettings.SheetLayoutSettings.ViewTitleAnchor = SelectedViewTitleAnchor != null
                ? SelectedViewTitleAnchor.Value
                : ViewTitleAnchor.BottomLeft;
            elevationSettings.SheetLayoutSettings.ViewTitleOffsetXmm = ParseDouble(ViewTitleOffsetXmmText);
            elevationSettings.SheetLayoutSettings.ViewTitleOffsetYmm = ParseDouble(ViewTitleOffsetYmmText);

            elevationSettings.SheetNamePart1 = SheetNamePart1Text ?? string.Empty;
            elevationSettings.SheetNamePart2 = SheetNamePart2Text ?? string.Empty;
            elevationSettings.SheetNamePart3 = SheetNamePart3Text ?? string.Empty;

            // Блок параметров план-схемы помещения.
            elevationSettings.CreateRoomPlanScheme = CreateRoomPlanScheme;
            elevationSettings.PlaceRoomPlanSchemeOnSheet = CreateRoomPlanScheme && PlaceViewsOnSheet && PlaceRoomPlanSchemeOnSheet;
            elevationSettings.RoomPlanNamePart1 = RoomPlanNamePart1Text ?? string.Empty;
            elevationSettings.RoomPlanNamePart2 = RoomPlanNamePart2Text ?? string.Empty;
            elevationSettings.RoomPlanNamePart3 = RoomPlanNamePart3Text ?? string.Empty;
            elevationSettings.RoomPlanViewTemplateId = !UseExistingSheet && SelectedRoomPlanViewTemplate != null
                ? SelectedRoomPlanViewTemplate.Id
                : ElementId.InvalidElementId;
            elevationSettings.RoomPlanRoomTagTypeId = !UseExistingSheet && SelectedRoomPlanRoomTagType != null
                ? SelectedRoomPlanRoomTagType.Id
                : ElementId.InvalidElementId;
            elevationSettings.RoomPlanViewScale = UseExistingSheet ? 1 : ParseInt(RoomPlanViewScaleText);
            elevationSettings.RoomPlanCropOffsetMm = UseExistingSheet ? 0.0 : ParseDouble(RoomPlanCropOffsetMmText);

            settings = elevationSettings;
            return true;
        }

        public void SetSheetStartPointFromRevitPoint(XYZ sheetPoint)
        {
            if (sheetPoint == null)
            {
                return;
            }

            // Блок переноса координаты листа из внутренних футов Revit в миллиметры окна настроек.
            StartXmmText = FormatDouble(UnitConversionUtils.FeetToMillimeters(sheetPoint.X));
            StartYmmText = FormatDouble(UnitConversionUtils.FeetToMillimeters(sheetPoint.Y));
        }

        public void SetExistingRoomPlanViewSelection(ViewPlan planView)
        {
            if (planView == null || !planView.IsValidObject)
            {
                ClearExistingRoomPlanViewSelection();
                return;
            }

            _existingRoomPlanViewId = planView.Id;
            _existingRoomPlanViewName = planView.Name;
            OnPropertyChanged("ExistingRoomPlanViewId");
            OnPropertyChanged("ExistingRoomPlanSummary");
            OnPropertyChanged("HasExistingRoomPlanSelection");
        }

        private void ClearExistingRoomPlanViewSelection()
        {
            if (!HasExistingRoomPlanSelection && string.IsNullOrWhiteSpace(_existingRoomPlanViewName))
            {
                return;
            }

            _existingRoomPlanViewId = ElementId.InvalidElementId;
            _existingRoomPlanViewName = null;
            OnPropertyChanged("ExistingRoomPlanViewId");
            OnPropertyChanged("ExistingRoomPlanSummary");
            OnPropertyChanged("HasExistingRoomPlanSelection");
        }

        public void SetCropHeightOffsetsFromExample(double topOffsetMm, double bottomOffsetMm)
        {
            // Блок записи значений из вида-примера в те же поля, которые использует основная команда.
            TopOffsetMmText = FormatDouble(Math.Max(0.0, topOffsetMm));
            BottomOffsetMmText = FormatDouble(Math.Max(0.0, bottomOffsetMm));
            OnPropertyChanged("TopOffsetMmText");
            OnPropertyChanged("BottomOffsetMmText");
        }

        private string ValidateInput(bool requireExistingRoomPlanSelection, bool requireRoomPlanPosition)
        {
            if (requireRoomPlanPosition && CanPlaceRoomPlanSchemeOnSheet && PlaceRoomPlanSchemeOnSheet && UseManualRoomPlanPosition)
            {
                double planRight, planBottom;
                if (!TryParseDouble(RoomPlanOffsetRightMmText, out planRight) || double.IsNaN(planRight) || double.IsInfinity(planRight) || planRight < 0 ||
                    !TryParseDouble(RoomPlanOffsetBottomMmText, out planBottom) || double.IsNaN(planBottom) || double.IsInfinity(planBottom) || planBottom < 0)
                    return "Отступы план-схемы справа и снизу должны быть неотрицательными числами (мм).";
            }
            if (SelectedElevationViewFamilyType == null)
            {
                return "Не выбран тип вида развертки.";
            }

            if (SelectedPlanCornerMarkType == null)
            {
                return "Не выбран тип семейства марки угла на плане.";
            }

            int viewScale;
            if (!TryParseInt(ViewScaleText, out viewScale) || viewScale <= 0)
            {
                return "Масштаб вида должен быть положительным целым числом.";
            }

            double top;
            if (!TryParseDouble(TopOffsetMmText, out top) || top < 0)
            {
                return "Верхний отступ должен быть неотрицательным числом (мм).";
            }

            double bottom;
            if (!TryParseDouble(BottomOffsetMmText, out bottom) || bottom < 0)
            {
                return "Нижний отступ должен быть неотрицательным числом (мм).";
            }

            double left;
            if (!TryParseDouble(LeftOffsetMmText, out left) || left < 0)
            {
                return "Левый отступ должен быть неотрицательным числом (мм).";
            }

            double right;
            if (!TryParseDouble(RightOffsetMmText, out right) || right < 0)
            {
                return "Правый отступ должен быть неотрицательным числом (мм).";
            }

            double depth;
            if (!TryParseDouble(ViewDepthMmText, out depth) || depth <= 0)
            {
                return "Глубина проекции должна быть положительным числом (мм).";
            }

            double markerOffset;
            if (!TryParseDouble(MarkerOffsetMmText, out markerOffset) || markerOffset < 0)
            {
                return "Отступ вида от линии должен быть неотрицательным числом (мм).";
            }

            double gridTopExtension;
            if (!TryParseDouble(GridTopExtensionPaperMmText, out gridTopExtension) || gridTopExtension < 0)
            {
                return "Верхний выступ оси должен быть неотрицательным числом (мм на листе).";
            }

            double gridBottomExtension;
            if (!TryParseDouble(GridBottomExtensionPaperMmText, out gridBottomExtension) || gridBottomExtension < 0)
            {
                return "Нижний выступ оси должен быть неотрицательным числом (мм на листе).";
            }

            if (string.IsNullOrWhiteSpace(ElevationNamePart1Text) &&
                string.IsNullOrWhiteSpace(ElevationNamePart2Text) &&
                string.IsNullOrWhiteSpace(ElevationNamePart3Text))
            {
                return "Формула имени развертки не может быть пустой.";
            }

            if (CreateRoomPlanScheme && !UseExistingSheet)
            {
                if (string.IsNullOrWhiteSpace(RoomPlanNamePart1Text) &&
                    string.IsNullOrWhiteSpace(RoomPlanNamePart2Text) &&
                    string.IsNullOrWhiteSpace(RoomPlanNamePart3Text))
                {
                    return "Формула имени план-схемы не может быть пустой.";
                }

                double roomPlanCropOffset;
                if (!TryParseDouble(RoomPlanCropOffsetMmText, out roomPlanCropOffset))
                {
                    return "Отступ границы обрезки план-схемы должен быть числом (мм).";
                }

                int roomPlanViewScale;
                if (!TryParseInt(RoomPlanViewScaleText, out roomPlanViewScale) || roomPlanViewScale <= 0)
                {
                    return "Масштаб вида план-схемы должен быть положительным целым числом.";
                }
            }

            if (PlaceViewsOnSheet)
            {
                if (string.IsNullOrWhiteSpace(ElevationTitlePart1Text) &&
                    string.IsNullOrWhiteSpace(ElevationTitlePart2Text) &&
                    string.IsNullOrWhiteSpace(ElevationTitlePart3Text))
                {
                    return "Формула заголовка на листе не может быть пустой.";
                }

                if (CreateSheet && SelectedTitleBlockType == null)
                {
                    return "Включено создание листа, но не выбран тип основной надписи.";
                }

                if (UseExistingSheet && SelectedExistingSheet == null)
                {
                    return "Выберите существующий лист по номеру или имени.";
                }

                if (UseExistingSheet && CreateRoomPlanScheme && requireExistingRoomPlanSelection &&
                    !HasExistingRoomPlanSelection)
                {
                    return "Укажите точку размещения и выберите план-схему на существующем листе.";
                }

                if (SelectedViewTitleAnchor == null)
                {
                    return "Не выбрана привязка размещения заголовка развертки.";
                }

                if (CreateSheet &&
                    string.IsNullOrWhiteSpace(SheetNamePart1Text) &&
                    string.IsNullOrWhiteSpace(SheetNamePart2Text) &&
                    string.IsNullOrWhiteSpace(SheetNamePart3Text))
                {
                    return "Формула имени листа не может быть пустой.";
                }

                if (SelectedSheetCornerMarkType == null)
                {
                    return "Включено создание листа, но не выбран тип семейства марки угла на листе.";
                }

                double startX;
                int columnsCount;
                if (!TryParseInt(ColumnsCountText, out columnsCount) || columnsCount <= 0)
                    return "Количество видов в одной строке должно быть положительным целым числом.";
                if (!TryParseDouble(StartXmmText, out startX))
                {
                    return "Начальная координата X на листе должна быть числом (мм).";
                }

                double startY;
                if (!TryParseDouble(StartYmmText, out startY))
                {
                    return "Начальная координата Y на листе должна быть числом (мм).";
                }

                double stepX;
                if (!TryParseDouble(StepXmmText, out stepX) || stepX <= 0)
                {
                    return "Шаг по X должен быть положительным числом (мм).";
                }

                double stepY;
                if (!TryParseDouble(StepYmmText, out stepY) || stepY <= 0)
                {
                    return "Шаг по Y должен быть положительным числом (мм).";
                }

                double viewTitleOffsetX;
                if (!TryParseDouble(ViewTitleOffsetXmmText, out viewTitleOffsetX))
                {
                    return "Смещение заголовка по X должно быть числом (мм).";
                }

                double viewTitleOffsetY;
                if (!TryParseDouble(ViewTitleOffsetYmmText, out viewTitleOffsetY))
                {
                    return "Смещение заголовка по Y должно быть числом (мм).";
                }

                if (!string.IsNullOrWhiteSpace(SheetFormatAText))
                {
                    int sheetFormatAValue;
                    if (!TryParseInt(SheetFormatAText, out sheetFormatAValue) || sheetFormatAValue < 0)
                    {
                        return "Формат листа должен быть целым неотрицательным числом.";
                    }
                }
            }

            return string.Empty;
        }

        private void LoadElevationViewFamilyTypes()
        {
            List<RevitElementOption> options = new List<RevitElementOption>();

            FilteredElementCollector collector = new FilteredElementCollector(_document).OfClass(typeof(ViewFamilyType));
            foreach (Element element in collector)
            {
                ViewFamilyType viewFamilyType = element as ViewFamilyType;
                if (viewFamilyType == null)
                {
                    continue;
                }

                if (viewFamilyType.ViewFamily != ViewFamily.Elevation)
                {
                    continue;
                }

                RevitElementOption option = new RevitElementOption();
                option.Id = viewFamilyType.Id;
                option.DisplayName = viewFamilyType.Name;
                options.Add(option);
            }

            options.Sort(delegate(RevitElementOption left, RevitElementOption right)
            {
                return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            });

            for (int i = 0; i < options.Count; i++)
            {
                ElevationViewFamilyTypes.Add(options[i]);
            }

            if (ElevationViewFamilyTypes.Count > 0)
            {
                SelectedElevationViewFamilyType = ElevationViewFamilyTypes[0];
            }
        }

        private void LoadViewTemplates()
        {
            RevitElementOption emptyOption = new RevitElementOption();
            emptyOption.Id = ElementId.InvalidElementId;
            emptyOption.DisplayName = "<Не выбран>";
            ViewTemplates.Add(emptyOption);

            List<RevitElementOption> options = new List<RevitElementOption>();
            FilteredElementCollector collector = new FilteredElementCollector(_document).OfClass(typeof(View));

            foreach (Element element in collector)
            {
                View view = element as View;
                if (view == null || !view.IsTemplate)
                {
                    continue;
                }

                if (view.ViewType != ViewType.Elevation && view.ViewType != ViewType.Section)
                {
                    continue;
                }

                RevitElementOption option = new RevitElementOption();
                option.Id = view.Id;
                option.DisplayName = view.Name;
                options.Add(option);
            }

            options.Sort(delegate(RevitElementOption left, RevitElementOption right)
            {
                return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            });

            for (int i = 0; i < options.Count; i++)
            {
                ViewTemplates.Add(options[i]);
            }

            SelectedViewTemplate = ViewTemplates[0];
        }

        private void LoadRoomPlanViewTemplates()
        {
            RevitElementOption emptyOption = new RevitElementOption
            {
                Id = ElementId.InvalidElementId,
                DisplayName = "<Не выбран>"
            };
            RoomPlanViewTemplates.Add(emptyOption);

            List<RevitElementOption> options = new List<RevitElementOption>();
            FilteredElementCollector collector = new FilteredElementCollector(_document).OfClass(typeof(View));
            foreach (Element element in collector)
            {
                View view = element as View;
                if (view == null || !view.IsTemplate)
                {
                    continue;
                }

                if (view.ViewType != ViewType.FloorPlan)
                {
                    continue;
                }

                RevitElementOption option = new RevitElementOption
                {
                    Id = view.Id,
                    DisplayName = view.Name
                };
                options.Add(option);
            }

            options.Sort(delegate(RevitElementOption left, RevitElementOption right)
            {
                return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            });

            for (int index = 0; index < options.Count; index++)
            {
                RoomPlanViewTemplates.Add(options[index]);
            }

            SelectedRoomPlanViewTemplate = RoomPlanViewTemplates.Count > 0 ? RoomPlanViewTemplates[0] : null;
        }

        private void LoadRoomPlanRoomTagTypes()
        {
            RevitElementOption emptyOption = new RevitElementOption
            {
                Id = ElementId.InvalidElementId,
                DisplayName = "<Не выбран>"
            };
            RoomPlanRoomTagTypes.Add(emptyOption);

            List<RevitElementOption> options = new List<RevitElementOption>();
            FilteredElementCollector collector = new FilteredElementCollector(_document)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_RoomTags);

            foreach (Element element in collector)
            {
                FamilySymbol familySymbol = element as FamilySymbol;
                if (familySymbol == null)
                {
                    continue;
                }

                string familyName = familySymbol.Family != null ? familySymbol.Family.Name : familySymbol.FamilyName;
                RevitElementOption option = new RevitElementOption
                {
                    Id = familySymbol.Id,
                    DisplayName = familyName + " : " + familySymbol.Name
                };
                options.Add(option);
            }

            options.Sort(delegate(RevitElementOption left, RevitElementOption right)
            {
                return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            });

            for (int index = 0; index < options.Count; index++)
            {
                RoomPlanRoomTagTypes.Add(options[index]);
            }

            SelectedRoomPlanRoomTagType = RoomPlanRoomTagTypes.Count > 0 ? RoomPlanRoomTagTypes[0] : null;
        }

        private void LoadTitleBlockTypes()
        {
            List<RevitElementOption> options = new List<RevitElementOption>();

            FilteredElementCollector collector =
                new FilteredElementCollector(_document)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_TitleBlocks);

            foreach (Element element in collector)
            {
                FamilySymbol titleBlockSymbol = element as FamilySymbol;
                if (titleBlockSymbol == null)
                {
                    continue;
                }

                RevitElementOption option = new RevitElementOption();
                option.Id = titleBlockSymbol.Id;
                option.DisplayName = titleBlockSymbol.FamilyName + " : " + titleBlockSymbol.Name;
                options.Add(option);
            }

            options.Sort(delegate(RevitElementOption left, RevitElementOption right)
            {
                return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            });

            for (int i = 0; i < options.Count; i++)
            {
                TitleBlockTypes.Add(options[i]);
            }

            if (TitleBlockTypes.Count > 0)
            {
                SelectedTitleBlockType = TitleBlockTypes[0];
            }
        }

        private void LoadExistingSheets()
        {
            List<RevitElementOption> options = new List<RevitElementOption>();
            FilteredElementCollector collector = new FilteredElementCollector(_document).OfClass(typeof(ViewSheet));

            foreach (Element element in collector)
            {
                ViewSheet sheet = element as ViewSheet;
                if (sheet == null || sheet.IsPlaceholder)
                {
                    continue;
                }

                if (string.Equals(sheet.Name, "SAB-Развертки по линии", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(sheet.SheetNumber, "SAB-Развертки по линии", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                RevitElementOption option = new RevitElementOption();
                option.Id = sheet.Id;
                option.DisplayName = sheet.SheetNumber + " — " + sheet.Name;
                options.Add(option);
            }

            options.Sort(delegate(RevitElementOption left, RevitElementOption right)
            {
                return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            });

            for (int index = 0; index < options.Count; index++)
            {
                ExistingSheets.Add(options[index]);
            }
        }

        private bool FilterExistingSheet(object item)
        {
            RevitElementOption option = item as RevitElementOption;
            if (option == null)
            {
                return false;
            }

            string searchText = ExistingSheetSearchText;
            return string.IsNullOrWhiteSpace(searchText) ||
                   (option.DisplayName ?? string.Empty).IndexOf(
                       searchText.Trim(),
                       StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void LoadViewportTypes()
        {
            Dictionary<long, RevitElementOption> optionsById = new Dictionary<long, RevitElementOption>();
            try
            {
                FilteredElementCollector categoryCollector = new FilteredElementCollector(_document)
                    .OfCategory(BuiltInCategory.OST_Viewports)
                    .WhereElementIsElementType();

                foreach (Element element in categoryCollector)
                {
                    AddViewportTypeOption(element as ElementType, optionsById);
                }
            }
            catch
            {
                // В некоторых шаблонах Revit прямой фильтр категории Viewport недоступен.
            }

            try
            {
                FilteredElementCollector fallbackCollector = new FilteredElementCollector(_document)
                    .OfClass(typeof(ElementType))
                    .WhereElementIsElementType();

                foreach (Element element in fallbackCollector)
                {
                    AddViewportTypeOption(element as ElementType, optionsById);
                }
            }
            catch
            {
                // Если оба способа недоступны, создание листа останется выключенным.
            }

            List<RevitElementOption> options = new List<RevitElementOption>();
            foreach (RevitElementOption option in optionsById.Values)
            {
                options.Add(option);
            }

            options.Sort(delegate(RevitElementOption left, RevitElementOption right)
            {
                return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            });

            for (int index = 0; index < options.Count; index++)
            {
                ViewportTypes.Add(options[index]);
            }

            SelectedViewportType = ViewportTypes.Count > 0 ? ViewportTypes[0] : null;
        }

        private void AddViewportTypeOption(
            ElementType viewportType,
            IDictionary<long, RevitElementOption> optionsById)
        {
            if (!IsLikelyViewportType(viewportType) || optionsById == null)
            {
                return;
            }

            long idValue = RevitElementIdUtils.GetElementIdValue(viewportType.Id);
            if (idValue < 0 || optionsById.ContainsKey(idValue))
            {
                return;
            }

            string familyName = viewportType.FamilyName ?? string.Empty;
            string displayName = string.IsNullOrWhiteSpace(familyName) ||
                                 string.Equals(familyName, viewportType.Name, StringComparison.OrdinalIgnoreCase)
                ? viewportType.Name
                : familyName + " : " + viewportType.Name;

            optionsById.Add(idValue, new RevitElementOption
            {
                Id = viewportType.Id,
                DisplayName = displayName
            });
        }

        private bool IsLikelyViewportType(ElementType elementType)
        {
            if (elementType == null)
            {
                return false;
            }

            if (elementType.Category != null &&
                elementType.Category.Id != null &&
                elementType.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Viewports)
            {
                return true;
            }

            string categoryName = elementType.Category != null ? elementType.Category.Name : string.Empty;
            string familyName = elementType.FamilyName ?? string.Empty;
            string typeName = elementType.Name ?? string.Empty;

            return ContainsViewportText(categoryName) ||
                   ContainsViewportText(familyName) ||
                   ContainsViewportText(typeName);
        }

        private bool ContainsViewportText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            return text.IndexOf("Viewport", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Viewports", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Видовой экран", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Видовые экраны", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void LoadViewTitleAnchors()
        {
            ViewTitleAnchors.Add(new ViewTitleAnchorOption
            {
                Value = ViewTitleAnchor.BottomLeft,
                DisplayName = "Слева под видом"
            });
            ViewTitleAnchors.Add(new ViewTitleAnchorOption
            {
                Value = ViewTitleAnchor.BottomCenter,
                DisplayName = "По центру под видом"
            });
            ViewTitleAnchors.Add(new ViewTitleAnchorOption
            {
                Value = ViewTitleAnchor.BottomRight,
                DisplayName = "Справа под видом"
            });
            ViewTitleAnchors.Add(new ViewTitleAnchorOption
            {
                Value = ViewTitleAnchor.TopLeft,
                DisplayName = "Слева над видом"
            });
            ViewTitleAnchors.Add(new ViewTitleAnchorOption
            {
                Value = ViewTitleAnchor.TopCenter,
                DisplayName = "По центру над видом"
            });

            SelectedViewTitleAnchor = ViewTitleAnchors[0];
        }

        private void LoadCornerMarkTypes(ObservableCollection<RevitElementOption> targetCollection)
        {
            List<RevitElementOption> options = new List<RevitElementOption>();
            RevitElementOption projectSpecificFamilyOption = null;
            bool hideProjectSpecificSideTypes = _document != null &&
                _document.Application != null &&
                string.Equals(_document.Application.VersionNumber, "2022", StringComparison.Ordinal);

            FilteredElementCollector collector = new FilteredElementCollector(_document)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_GenericAnnotation);

            foreach (Element element in collector)
            {
                FamilySymbol familySymbol = element as FamilySymbol;
                if (familySymbol == null)
                {
                    continue;
                }

                if (!CornerMarkConstants.IsAnnotationSymbol(familySymbol))
                {
                    continue;
                }

                string familyName = familySymbol.Family != null ? familySymbol.Family.Name : familySymbol.FamilyName;
                if (hideProjectSpecificSideTypes &&
                    string.Equals(
                        familyName,
                        CornerMarkConstants.ProjectSpecificCornerMarkFamilyName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    if (projectSpecificFamilyOption == null)
                    {
                        projectSpecificFamilyOption = new RevitElementOption();
                        projectSpecificFamilyOption.Id = familySymbol.Id;
                        projectSpecificFamilyOption.DisplayName = familyName;
                    }

                    if (string.Equals(
                        familySymbol.Name,
                        CornerMarkConstants.LeftCornerMarkTypeName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        projectSpecificFamilyOption.Id = familySymbol.Id;
                    }

                    continue;
                }

                RevitElementOption option = new RevitElementOption();
                option.Id = familySymbol.Id;
                option.DisplayName = familyName + " : " + familySymbol.Name;
                options.Add(option);
            }

            if (projectSpecificFamilyOption != null)
            {
                options.Add(projectSpecificFamilyOption);
            }

            options.Sort(delegate(RevitElementOption left, RevitElementOption right)
            {
                return string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase);
            });

            for (int index = 0; index < options.Count; index++)
            {
                targetCollection.Add(options[index]);
            }

            if (targetCollection == PlanCornerMarkTypes && PlanCornerMarkTypes.Count > 0)
            {
                SelectedPlanCornerMarkType = PlanCornerMarkTypes[0];
            }

            if (targetCollection == SheetCornerMarkTypes && SheetCornerMarkTypes.Count > 0)
            {
                SelectedSheetCornerMarkType = SheetCornerMarkTypes[0];
            }
        }

        private int ParseInt(string text)
        {
            int value;
            if (!TryParseInt(text, out value))
            {
                return 0;
            }

            return value;
        }

        private int? ParseNullableInt(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            int value;
            if (!TryParseInt(text, out value))
            {
                return null;
            }

            return value;
        }

        private void ApplyInitialSettings(ElevationSettings initialSettings)
        {
            if (initialSettings == null)
            {
                return;
            }

            // Восстанавливаем тип вида развертки только если тип доступен в текущем документе.
            RevitElementOption elevationTypeOption = FindOptionById(ElevationViewFamilyTypes, initialSettings.ElevationViewFamilyTypeId);
            if (elevationTypeOption != null)
            {
                SelectedElevationViewFamilyType = elevationTypeOption;
            }

            // Если ранее шаблон не выбирали, оставляем вариант "<Не выбран>".
            if (initialSettings.ViewTemplateId == null || RevitElementIdUtils.AreEqual(initialSettings.ViewTemplateId, ElementId.InvalidElementId))
            {
                if (ViewTemplates.Count > 0)
                {
                    SelectedViewTemplate = ViewTemplates[0];
                }
            }
            else
            {
                RevitElementOption templateOption = FindOptionById(ViewTemplates, initialSettings.ViewTemplateId);
                if (templateOption != null)
                {
                    SelectedViewTemplate = templateOption;
                }
            }

            if (initialSettings.ViewScale > 0)
            {
                ViewScaleText = initialSettings.ViewScale.ToString(CultureInfo.CurrentCulture);
            }

            if (initialSettings.TopOffsetMm >= 0)
            {
                TopOffsetMmText = FormatDouble(initialSettings.TopOffsetMm);
            }

            if (initialSettings.BottomOffsetMm >= 0)
            {
                BottomOffsetMmText = FormatDouble(initialSettings.BottomOffsetMm);
            }

            if (initialSettings.LeftOffsetMm >= 0)
            {
                LeftOffsetMmText = FormatDouble(initialSettings.LeftOffsetMm);
            }

            if (initialSettings.RightOffsetMm >= 0)
            {
                RightOffsetMmText = FormatDouble(initialSettings.RightOffsetMm);
            }

            if (initialSettings.ViewDepthMm > 0)
            {
                ViewDepthMmText = FormatDouble(initialSettings.ViewDepthMm);
            }

            if (initialSettings.MarkerOffsetMm >= 0)
            {
                MarkerOffsetMmText = FormatDouble(initialSettings.MarkerOffsetMm);
            }

            if (initialSettings.GridTopExtensionPaperMm >= 0)
            {
                GridTopExtensionPaperMmText = FormatDouble(initialSettings.GridTopExtensionPaperMm);
            }

            if (initialSettings.GridBottomExtensionPaperMm >= 0)
            {
                GridBottomExtensionPaperMmText = FormatDouble(initialSettings.GridBottomExtensionPaperMm);
            }

            ElevationNamePart1Text = NormalizeFormulaSyntax(initialSettings.ElevationNamePart1);
            ElevationNamePart2Text = NormalizeFormulaSyntax(initialSettings.ElevationNamePart2);
            ElevationNamePart3Text = NormalizeFormulaSyntax(initialSettings.ElevationNamePart3);
            ElevationTitlePart1Text = NormalizeFormulaSyntax(initialSettings.ElevationTitlePart1);
            ElevationTitlePart2Text = NormalizeFormulaSyntax(initialSettings.ElevationTitlePart2);
            ElevationTitlePart3Text = NormalizeFormulaSyntax(initialSettings.ElevationTitlePart3);

            if (initialSettings.SheetFormatAValue.HasValue)
            {
                SheetFormatAText = initialSettings.SheetFormatAValue.Value.ToString(CultureInfo.CurrentCulture);
            }

            // Включаем создание листа только если в проекте есть доступные типы основной надписи.
            CreateSheet = initialSettings.CreateSheet && TitleBlockTypes.Count > 0 && ViewportTypes.Count > 0;
            OpenCreatedSheet = initialSettings.OpenCreatedSheet;
            IsMultipleRoomsOnSheet = initialSettings.MultipleRoomsOnSheet;
            PickRoomFromLink = initialSettings.PickRoomFromLink;
            EnableRoomObjectCategory = initialSettings.EnableRoomObjectCategory;
            CornerMarksOnlyCornerNumber = initialSettings.CornerMarksOnlyCornerNumber;
            SheetCornerMarksBelowView = initialSettings.SheetCornerMarksBelowView;
            RevitElementOption titleBlockOption = FindOptionById(TitleBlockTypes, initialSettings.TitleBlockTypeId);
            if (titleBlockOption != null)
            {
                SelectedTitleBlockType = titleBlockOption;
            }

            RevitElementOption viewportTypeOption = FindOptionById(ViewportTypes, initialSettings.ViewportTypeId);
            if (viewportTypeOption != null)
            {
                SelectedViewportType = viewportTypeOption;
            }

            RevitElementOption planMarkOption = FindOptionById(PlanCornerMarkTypes, initialSettings.PlanCornerMarkTypeId);
            if (planMarkOption != null)
            {
                SelectedPlanCornerMarkType = planMarkOption;
            }

            RevitElementOption sheetMarkOption = FindOptionById(SheetCornerMarkTypes, initialSettings.SheetCornerMarkTypeId);
            if (sheetMarkOption != null)
            {
                SelectedSheetCornerMarkType = sheetMarkOption;
            }

            SheetLayoutSettings savedLayout = initialSettings.SheetLayoutSettings;
            if (savedLayout != null)
            {
                UseAutomaticSheetPlacement = savedLayout.UseAutomaticPlacement;
                UseManualRoomPlanPosition = savedLayout.UseManualRoomPlanPosition;
                RoomPlanOffsetRightMmText = FormatDouble(savedLayout.RoomPlanOffsetRightMm);
                RoomPlanOffsetBottomMmText = FormatDouble(savedLayout.RoomPlanOffsetBottomMm);
                if (savedLayout.ColumnsCount > 0)
                {
                    ColumnsCountText = savedLayout.ColumnsCount.ToString(CultureInfo.CurrentCulture);
                }

                StartXmmText = FormatDouble(savedLayout.StartXmm);
                StartYmmText = FormatDouble(savedLayout.StartYmm);

                if (savedLayout.StepXmm > 0)
                {
                    StepXmmText = FormatDouble(savedLayout.StepXmm);
                }

                if (savedLayout.StepYmm > 0)
                {
                    StepYmmText = FormatDouble(savedLayout.StepYmm);
                }

                ViewTitleOffsetXmmText = FormatDouble(savedLayout.ViewTitleOffsetXmm);
                ViewTitleOffsetYmmText = FormatDouble(savedLayout.ViewTitleOffsetYmm);
                for (int anchorIndex = 0; anchorIndex < ViewTitleAnchors.Count; anchorIndex++)
                {
                    if (ViewTitleAnchors[anchorIndex].Value == savedLayout.ViewTitleAnchor)
                    {
                        SelectedViewTitleAnchor = ViewTitleAnchors[anchorIndex];
                        break;
                    }
                }
            }

            SheetNamePart1Text = NormalizeFormulaSyntax(initialSettings.SheetNamePart1);
            SheetNamePart2Text = NormalizeFormulaSyntax(initialSettings.SheetNamePart2);
            SheetNamePart3Text = NormalizeFormulaSyntax(initialSettings.SheetNamePart3);

            // Блок восстановления настроек план-схемы.
            CreateRoomPlanScheme = initialSettings.CreateRoomPlanScheme;
            PlaceRoomPlanSchemeOnSheet = initialSettings.PlaceRoomPlanSchemeOnSheet;
            if (!string.IsNullOrWhiteSpace(initialSettings.RoomPlanNamePart1))
            {
                RoomPlanNamePart1Text = NormalizeFormulaSyntax(initialSettings.RoomPlanNamePart1);
            }

            if (!string.IsNullOrWhiteSpace(initialSettings.RoomPlanNamePart2))
            {
                RoomPlanNamePart2Text = NormalizeFormulaSyntax(initialSettings.RoomPlanNamePart2);
            }

            RoomPlanNamePart3Text = NormalizeFormulaSyntax(initialSettings.RoomPlanNamePart3);
            if (initialSettings.RoomPlanViewScale > 0)
            {
                RoomPlanViewScaleText = initialSettings.RoomPlanViewScale.ToString(CultureInfo.CurrentCulture);
            }
            RoomPlanCropOffsetMmText = FormatDouble(initialSettings.RoomPlanCropOffsetMm);

            if (initialSettings.RoomPlanViewTemplateId != null && !RevitElementIdUtils.AreEqual(initialSettings.RoomPlanViewTemplateId, ElementId.InvalidElementId))
            {
                RevitElementOption roomPlanTemplateOption = FindOptionById(RoomPlanViewTemplates, initialSettings.RoomPlanViewTemplateId);
                if (roomPlanTemplateOption != null)
                {
                    SelectedRoomPlanViewTemplate = roomPlanTemplateOption;
                }
            }

            if (initialSettings.RoomPlanRoomTagTypeId != null && !RevitElementIdUtils.AreEqual(initialSettings.RoomPlanRoomTagTypeId, ElementId.InvalidElementId))
            {
                RevitElementOption roomTagOption = FindOptionById(RoomPlanRoomTagTypes, initialSettings.RoomPlanRoomTagTypeId);
                if (roomTagOption != null)
                {
                    SelectedRoomPlanRoomTagType = roomTagOption;
                }
            }
        }

        private RevitElementOption FindOptionById(ObservableCollection<RevitElementOption> options, ElementId elementId)
        {
            if (options == null || elementId == null || RevitElementIdUtils.AreEqual(elementId, ElementId.InvalidElementId))
            {
                return null;
            }

            for (int i = 0; i < options.Count; i++)
            {
                RevitElementOption option = options[i];
                if (option == null || option.Id == null)
                {
                    continue;
                }

                if (RevitElementIdUtils.AreEqual(option.Id, elementId))
                {
                    return option;
                }
            }

            return null;
        }

        private static string NormalizeFormulaSyntax(string value)
        {
            return (value ?? string.Empty)
                .Replace("{Номер помещения}", "[Номер помещения]")
                .Replace("{Имя помещения}", "[Имя помещения]")
                .Replace("{Начальный угол}", "[Начальный угол]")
                .Replace("{Конечный угол}", "[Конечный угол]")
                .Replace("{Помещения}", "[Помещения]");
        }

        private string FormatDouble(double value)
        {
            return value.ToString("0.###", CultureInfo.CurrentCulture);
        }

        private double ParseDouble(string text)
        {
            double value;
            if (!TryParseDouble(text, out value))
            {
                return 0.0;
            }

            return value;
        }

        private bool TryParseInt(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value)
                   || int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private bool TryParseDouble(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out value)
                   || double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
}
