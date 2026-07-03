using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Windows.Input;
using Autodesk.Revit.DB;
using SAB.CreateViewsAndSheets.Models;
using SAB.InteriorElevations.Utils;
using SAB.RoomGeometryTools.Utils;

namespace SAB.CreateViewsAndSheets.ViewModels
{
    public class CreateViewsAndSheetsViewModel : INotifyPropertyChanged
    {
        private const double DefaultTitleLineLengthMm = 80.0;

        private readonly List<RevitElementItem> _allViewTemplates;
        private readonly Dictionary<long, List<string>> _sheetBrowserParameterValuesById;
        private readonly HashSet<string> _existingViewNames;
        private readonly HashSet<string> _existingSheetNumbers;

        private CreateViewsAndSheetsStructureMode _structureMode;
        private RevitElementItem _selectedSourceView;
        private RevitElementItem _selectedSourceSheet;
        private RevitElementItem _selectedCeilingSourceView;
        private RevitElementItem _selectedCeilingSourceSheet;
        private RevitElementItem _selectedViewportType;
        private RevitElementItem _selectedTitleBlockType;
        private RevitElementItem _selectedSheetBrowserParameter;

        private string _viewCenterXText;
        private string _viewCenterYText;
        private string _viewTitleXText;
        private string _viewTitleYText;
        private string _titleLineLengthText;
        private string _validationSummary;
        private string _statusText;
        private bool _saveSettings;
        private bool _isAccepted;
        private bool _canCreate;
        private bool _isRefreshingValidation;
        private bool _useSourceSheetViewportPlacement;
        private bool _isViewCenterManualMode;
        private bool _isViewTitleManualMode;
        private bool _copySheetWithDetailing;
        private bool _copySchedules;
        private bool _copyLegends;
        private bool _copyDraftingViews;
        private bool _copyDetailLines;
        private bool _copyFilledRegions;
        private bool _copyTextNotes;
        private bool _copyGenericAnnotations;
        private bool _copyImages;

        public CreateViewsAndSheetsViewModel(
            IList<RevitElementItem> sourceViews,
            IList<RevitElementItem> sourceSheets,
            IList<RevitElementItem> viewportTypes,
            IList<RevitElementItem> titleBlockTypes,
            IList<RevitElementItem> sheetBrowserParameters,
            Dictionary<long, List<string>> sheetBrowserParameterValuesById,
            IList<RevitElementItem> viewTemplates,
            HashSet<string> existingViewNames,
            HashSet<string> existingSheetNumbers,
            CreateViewsAndSheetsSettings initialSettings)
        {
            SourceViews = new ObservableCollection<RevitElementItem>();
            StandardSourceViews = new ObservableCollection<RevitElementItem>();
            CeilingSourceViews = new ObservableCollection<RevitElementItem>();
            SourceSheets = new ObservableCollection<RevitElementItem>();
            ViewportTypes = new ObservableCollection<RevitElementItem>();
            TitleBlockTypes = new ObservableCollection<RevitElementItem>();
            SheetBrowserParameters = new ObservableCollection<RevitElementItem>();
            SheetBrowserParameterValues = new ObservableCollection<string>();
            SheetBrowserParameterLevels = new ObservableCollection<SheetBrowserParameterLevelViewModel>();
            FloorMappings = new ObservableCollection<FloorSourceMappingRowViewModel>();
            FloorNames = new ObservableCollection<string>();
            PlanKindOptions = new ObservableCollection<PlanKindOptionViewModel>();
            ViewTemplates = new ObservableCollection<RevitElementItem>();
            Rows = new ObservableCollection<SheetCreationRowViewModel>();
            ScaleOptions = new ObservableCollection<string>();

            _allViewTemplates = new List<RevitElementItem>();
            _sheetBrowserParameterValuesById = new Dictionary<long, List<string>>();
            _existingViewNames = existingViewNames ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _existingSheetNumbers = existingSheetNumbers ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            FillCollection(SourceViews, sourceViews);
            PopulateSourceViewGroups(sourceViews);
            FillCollection(SourceSheets, sourceSheets);
            FillCollection(ViewportTypes, viewportTypes);
            FillCollection(TitleBlockTypes, titleBlockTypes);
            FillCollection(SheetBrowserParameters, sheetBrowserParameters);
            FillParameterValuesMap(_sheetBrowserParameterValuesById, sheetBrowserParameterValuesById);
            InitializeSheetBrowserParameterLevels(sheetBrowserParameters);
            FillList(_allViewTemplates, viewTemplates);

            ScaleOptions.Add("20");
            ScaleOptions.Add("25");
            ScaleOptions.Add("50");
            ScaleOptions.Add("75");
            ScaleOptions.Add("100");
            ScaleOptions.Add("200");
            PlanKindOptions.Add(new PlanKindOptionViewModel(SheetPlanKind.StandardPlan, "План стандартный"));
            PlanKindOptions.Add(new PlanKindOptionViewModel(SheetPlanKind.CeilingPlan, "План потолков"));

            PlacementSettings placement = initialSettings != null && initialSettings.Placement != null
                ? initialSettings.Placement
                : new PlacementSettings();

            _viewCenterXText = FormatDouble(placement.ViewCenterXmm);
            _viewCenterYText = FormatDouble(placement.ViewCenterYmm);
            _viewTitleXText = FormatDouble(placement.ViewTitleXmm);
            _viewTitleYText = FormatDouble(placement.ViewTitleYmm);
            _titleLineLengthText = FormatDouble(DefaultTitleLineLengthMm);
            _useSourceSheetViewportPlacement = placement.UseSourceSheetViewportPlacement;
            _isViewCenterManualMode = !placement.UsePointSelectionForViewCenter;
            _isViewTitleManualMode = !placement.UsePointSelectionForViewTitle;
            _saveSettings = placement.SaveSettings;

            SheetDetailCopySettings detailCopy = initialSettings != null && initialSettings.DetailCopy != null
                ? initialSettings.DetailCopy
                : new SheetDetailCopySettings();

            _copySheetWithDetailing = detailCopy.CopySheetWithDetailing;
            _copySchedules = detailCopy.CopySchedules;
            _copyLegends = detailCopy.CopyLegends;
            _copyDraftingViews = detailCopy.CopyDraftingViews;
            _copyDetailLines = detailCopy.CopyDetailLines;
            _copyFilledRegions = detailCopy.CopyFilledRegions;
            _copyTextNotes = detailCopy.CopyTextNotes;
            _copyGenericAnnotations = detailCopy.CopyGenericAnnotations;
            _copyImages = detailCopy.CopyImages;
            _structureMode = initialSettings != null ? initialSettings.StructureMode : CreateViewsAndSheetsStructureMode.SingleStory;

            InitializeFloorMappings(initialSettings);

            ValidateCommand = new RelayCommand(delegate { RefreshValidation(); });
            OpenSettingsWindowCommand = new RelayCommand(delegate { RequestSettingsWindowInternal(); });
            ImportSheetTableCommand = new RelayCommand(delegate { RequestSheetTableImportInternal(); });
            ClearRowsCommand = new RelayCommand(delegate { ClearAllRows(); }, delegate { return Rows != null && Rows.Count > 0; });
            CopySheetNamesToViewNamesCommand = new RelayCommand(delegate { CopySheetNamesToViewNames(); }, delegate { return Rows != null && Rows.Count > 0; });
            PickViewCenterPointCommand = new RelayCommand(delegate { RequestPointSelectionInternal(PlacementPointTarget.ViewCenter); });
            PickViewTitlePointCommand = new RelayCommand(delegate { RequestPointSelectionInternal(PlacementPointTarget.ViewTitle); });
            MoveRowUpCommand = new RelayCommand(
                delegate(object parameter) { MoveRow(parameter as SheetCreationRowViewModel, -1); },
                delegate(object parameter) { return CanMoveRow(parameter as SheetCreationRowViewModel, -1); });
            MoveRowDownCommand = new RelayCommand(
                delegate(object parameter) { MoveRow(parameter as SheetCreationRowViewModel, 1); },
                delegate(object parameter) { return CanMoveRow(parameter as SheetCreationRowViewModel, 1); });
            InsertRowAfterCommand = new RelayCommand(delegate(object parameter) { InsertRowAfter(parameter as SheetCreationRowViewModel); });
            DeleteRowCommand = new RelayCommand(delegate(object parameter) { DeleteRow(parameter as SheetCreationRowViewModel); });
            AddFloorMappingCommand = new RelayCommand(delegate { AddFloorMapping(null); RefreshValidation(); });
            DeleteFloorMappingCommand = new RelayCommand(delegate(object parameter) { DeleteFloorMapping(parameter as FloorSourceMappingRowViewModel); });
            CreateCommand = new RelayCommand(delegate { AcceptWindow(); }, delegate { return CanCreate; });
            CancelCommand = new RelayCommand(delegate { CancelWindow(); });

            ApplyInitialSelections(initialSettings);
            InitializeRows(initialSettings);
            RefreshValidation();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler RequestClose;

        public event EventHandler RequestSettingsWindow;

        public event EventHandler RequestSheetTableImport;

        public event EventHandler<PlacementPointSelectionRequestEventArgs> RequestPointSelection;

        public ObservableCollection<RevitElementItem> SourceViews { get; private set; }

        public ObservableCollection<RevitElementItem> StandardSourceViews { get; private set; }

        public ObservableCollection<RevitElementItem> CeilingSourceViews { get; private set; }

        public ObservableCollection<RevitElementItem> SourceSheets { get; private set; }

        public ObservableCollection<RevitElementItem> ViewportTypes { get; private set; }

        public ObservableCollection<RevitElementItem> TitleBlockTypes { get; private set; }

        public ObservableCollection<RevitElementItem> SheetBrowserParameters { get; private set; }

        public ObservableCollection<string> SheetBrowserParameterValues { get; private set; }

        public ObservableCollection<SheetBrowserParameterLevelViewModel> SheetBrowserParameterLevels { get; private set; }

        public ObservableCollection<FloorSourceMappingRowViewModel> FloorMappings { get; private set; }

        public ObservableCollection<string> FloorNames { get; private set; }

        public ObservableCollection<PlanKindOptionViewModel> PlanKindOptions { get; private set; }

        public ObservableCollection<RevitElementItem> ViewTemplates { get; private set; }

        public ObservableCollection<SheetCreationRowViewModel> Rows { get; private set; }

        public ObservableCollection<string> ScaleOptions { get; private set; }

        public ICommand ValidateCommand { get; private set; }

        public ICommand OpenSettingsWindowCommand { get; private set; }

        public ICommand ImportSheetTableCommand { get; private set; }

        public ICommand ClearRowsCommand { get; private set; }

        public ICommand CopySheetNamesToViewNamesCommand { get; private set; }

        public ICommand PickViewCenterPointCommand { get; private set; }

        public ICommand PickViewTitlePointCommand { get; private set; }

        public ICommand MoveRowUpCommand { get; private set; }

        public ICommand MoveRowDownCommand { get; private set; }

        public ICommand InsertRowAfterCommand { get; private set; }

        public ICommand DeleteRowCommand { get; private set; }

        public ICommand AddFloorMappingCommand { get; private set; }

        public ICommand DeleteFloorMappingCommand { get; private set; }

        public ICommand CreateCommand { get; private set; }

        public ICommand CancelCommand { get; private set; }

        public bool IsAccepted
        {
            get { return _isAccepted; }
        }

        public bool IsSingleStoryStructure
        {
            get { return _structureMode == CreateViewsAndSheetsStructureMode.SingleStory; }
            set
            {
                if (value)
                {
                    SetStructureMode(CreateViewsAndSheetsStructureMode.SingleStory);
                }
            }
        }

        public bool IsMultiStoryStructure
        {
            get { return _structureMode == CreateViewsAndSheetsStructureMode.MultiStory; }
            set
            {
                if (value)
                {
                    SetStructureMode(CreateViewsAndSheetsStructureMode.MultiStory);
                }
            }
        }

        public bool IsViewCenterManualMode
        {
            get { return _isViewCenterManualMode; }
            set
            {
                if (_isViewCenterManualMode == value)
                {
                    return;
                }

                _isViewCenterManualMode = value;
                OnPropertyChanged("IsViewCenterManualMode");
                OnPropertyChanged("IsViewCenterPointMode");
            }
        }

        public bool IsViewCenterPointMode
        {
            get { return !IsViewCenterManualMode; }
            set
            {
                if (value)
                {
                    IsViewCenterManualMode = false;
                }
            }
        }

        public bool IsViewTitleManualMode
        {
            get { return _isViewTitleManualMode; }
            set
            {
                if (_isViewTitleManualMode == value)
                {
                    return;
                }

                _isViewTitleManualMode = value;
                OnPropertyChanged("IsViewTitleManualMode");
                OnPropertyChanged("IsViewTitlePointMode");
            }
        }

        public bool IsViewTitlePointMode
        {
            get { return !IsViewTitleManualMode; }
            set
            {
                if (value)
                {
                    IsViewTitleManualMode = false;
                }
            }
        }

        public bool UseSourceSheetViewportPlacement
        {
            get { return _useSourceSheetViewportPlacement; }
            set
            {
                if (_useSourceSheetViewportPlacement == value)
                {
                    return;
                }

                _useSourceSheetViewportPlacement = value;
                OnPropertyChanged("UseSourceSheetViewportPlacement");
                OnPropertyChanged("IsSourceSheetViewportPlacementMode");
                OnPropertyChanged("IsPointViewportPlacementMode");
                OnPropertyChanged("IsManualViewportPlacementEnabled");
                RefreshValidation();
            }
        }

        public bool IsSourceSheetViewportPlacementMode
        {
            get { return UseSourceSheetViewportPlacement; }
            set
            {
                if (value)
                {
                    UseSourceSheetViewportPlacement = true;
                }
            }
        }

        public bool IsPointViewportPlacementMode
        {
            get { return !UseSourceSheetViewportPlacement; }
            set
            {
                if (value)
                {
                    UseSourceSheetViewportPlacement = false;
                }
            }
        }

        public bool IsManualViewportPlacementEnabled
        {
            get { return !UseSourceSheetViewportPlacement; }
        }

        public string ViewCenterPointSummary
        {
            get { return "X: " + ViewCenterXText + " мм | Y: " + ViewCenterYText + " мм"; }
        }

        public string ViewTitlePointSummary
        {
            get { return "X: " + ViewTitleXText + " мм | Y: " + ViewTitleYText + " мм"; }
        }

        public RevitElementItem SelectedSourceView
        {
            get { return _selectedSourceView; }
            set
            {
                _selectedSourceView = value;
                OnPropertyChanged("SelectedSourceView");
                ReloadCompatibleViewTemplates();
                RefreshValidation();
            }
        }

        public RevitElementItem SelectedSourceSheet
        {
            get { return _selectedSourceSheet; }
            set
            {
                _selectedSourceSheet = value;
                OnPropertyChanged("SelectedSourceSheet");
                ApplyTitleBlockFromSourceSheet();
                RefreshValidation();
            }
        }

        public RevitElementItem SelectedCeilingSourceView
        {
            get { return _selectedCeilingSourceView; }
            set
            {
                _selectedCeilingSourceView = value;
                OnPropertyChanged("SelectedCeilingSourceView");
                ReloadCompatibleViewTemplates();
                RefreshValidation();
            }
        }

        public RevitElementItem SelectedCeilingSourceSheet
        {
            get { return _selectedCeilingSourceSheet; }
            set
            {
                _selectedCeilingSourceSheet = value;
                OnPropertyChanged("SelectedCeilingSourceSheet");
                ApplyTitleBlockFromSourceSheet();
                RefreshValidation();
            }
        }

        public RevitElementItem SelectedViewportType
        {
            get { return _selectedViewportType; }
            set
            {
                _selectedViewportType = value;
                OnPropertyChanged("SelectedViewportType");
                RefreshValidation();
            }
        }

        public RevitElementItem SelectedTitleBlockType
        {
            get { return _selectedTitleBlockType; }
            set
            {
                _selectedTitleBlockType = value;
                OnPropertyChanged("SelectedTitleBlockType");
                RefreshValidation();
            }
        }

        public RevitElementItem SelectedSheetBrowserParameter
        {
            get { return _selectedSheetBrowserParameter; }
            set
            {
                _selectedSheetBrowserParameter = value;
                RefreshSheetBrowserParameterValues();
                OnPropertyChanged("SelectedSheetBrowserParameter");
                OnPropertyChanged("IsSheetBrowserParameterSelected");
                RefreshValidation();
            }
        }

        public bool IsSheetBrowserParameterSelected
        {
            get
            {
                return SelectedSheetBrowserParameter != null &&
                       SelectedSheetBrowserParameter.Id != null &&
                       SelectedSheetBrowserParameter.Id != ElementId.InvalidElementId;
            }
        }

        public string ViewCenterXText
        {
            get { return _viewCenterXText; }
            set
            {
                _viewCenterXText = value ?? string.Empty;
                OnPropertyChanged("ViewCenterXText");
                OnPropertyChanged("ViewCenterPointSummary");
                RefreshValidation();
            }
        }

        public string ViewCenterYText
        {
            get { return _viewCenterYText; }
            set
            {
                _viewCenterYText = value ?? string.Empty;
                OnPropertyChanged("ViewCenterYText");
                OnPropertyChanged("ViewCenterPointSummary");
                RefreshValidation();
            }
        }

        public string ViewTitleXText
        {
            get { return _viewTitleXText; }
            set
            {
                _viewTitleXText = value ?? string.Empty;
                OnPropertyChanged("ViewTitleXText");
                OnPropertyChanged("ViewTitlePointSummary");
                RefreshValidation();
            }
        }

        public string ViewTitleYText
        {
            get { return _viewTitleYText; }
            set
            {
                _viewTitleYText = value ?? string.Empty;
                OnPropertyChanged("ViewTitleYText");
                OnPropertyChanged("ViewTitlePointSummary");
                RefreshValidation();
            }
        }

        public string TitleLineLengthText
        {
            get { return _titleLineLengthText; }
            set
            {
                _titleLineLengthText = value ?? string.Empty;
                OnPropertyChanged("TitleLineLengthText");
                RefreshValidation();
            }
        }

        public bool SaveSettings
        {
            get { return _saveSettings; }
            set
            {
                _saveSettings = value;
                OnPropertyChanged("SaveSettings");
            }
        }

        public bool CopySheetWithDetailing
        {
            get { return _copySheetWithDetailing; }
            set
            {
                if (_copySheetWithDetailing == value)
                {
                    return;
                }

                _copySheetWithDetailing = value;
                OnPropertyChanged("CopySheetWithDetailing");
            }
        }

        public bool CopySchedules
        {
            get { return _copySchedules; }
            set
            {
                if (_copySchedules == value)
                {
                    return;
                }

                _copySchedules = value;
                OnPropertyChanged("CopySchedules");
            }
        }

        public bool CopyLegends
        {
            get { return _copyLegends; }
            set
            {
                if (_copyLegends == value)
                {
                    return;
                }

                _copyLegends = value;
                OnPropertyChanged("CopyLegends");
            }
        }

        public bool CopyDraftingViews
        {
            get { return _copyDraftingViews; }
            set
            {
                if (_copyDraftingViews == value)
                {
                    return;
                }

                _copyDraftingViews = value;
                OnPropertyChanged("CopyDraftingViews");
            }
        }

        public bool CopyDetailLines
        {
            get { return _copyDetailLines; }
            set
            {
                if (_copyDetailLines == value)
                {
                    return;
                }

                _copyDetailLines = value;
                OnPropertyChanged("CopyDetailLines");
            }
        }

        public bool CopyFilledRegions
        {
            get { return _copyFilledRegions; }
            set
            {
                if (_copyFilledRegions == value)
                {
                    return;
                }

                _copyFilledRegions = value;
                OnPropertyChanged("CopyFilledRegions");
            }
        }

        public bool CopyTextNotes
        {
            get { return _copyTextNotes; }
            set
            {
                if (_copyTextNotes == value)
                {
                    return;
                }

                _copyTextNotes = value;
                OnPropertyChanged("CopyTextNotes");
            }
        }

        public bool CopyGenericAnnotations
        {
            get { return _copyGenericAnnotations; }
            set
            {
                if (_copyGenericAnnotations == value)
                {
                    return;
                }

                _copyGenericAnnotations = value;
                OnPropertyChanged("CopyGenericAnnotations");
            }
        }

        public bool CopyImages
        {
            get { return _copyImages; }
            set
            {
                if (_copyImages == value)
                {
                    return;
                }

                _copyImages = value;
                OnPropertyChanged("CopyImages");
            }
        }

        public string ValidationSummary
        {
            get { return _validationSummary; }
            private set
            {
                _validationSummary = value ?? string.Empty;
                OnPropertyChanged("ValidationSummary");
            }
        }

        public string StatusText
        {
            get { return _statusText; }
            private set
            {
                _statusText = value ?? string.Empty;
                OnPropertyChanged("StatusText");
            }
        }

        public bool CanCreate
        {
            get { return _canCreate; }
            private set
            {
                _canCreate = value;
                OnPropertyChanged("CanCreate");

                RelayCommand createCommand = CreateCommand as RelayCommand;
                if (createCommand != null)
                {
                    createCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public bool TryBuildRequest(
            out CreateViewsAndSheetsSettings settings,
            out List<SheetCreationItem> items,
            out string validationMessage)
        {
            settings = null;
            items = null;
            validationMessage = string.Empty;

            ValidationState validationState = ValidateAllRows();
            ApplyValidationState(validationState);
            if (validationState.Errors.Count > 0)
            {
                validationMessage = BuildValidationText(validationState.Errors, validationState.Warnings);
                return false;
            }

            double centerX = ParseDouble(ViewCenterXText);
            double centerY = ParseDouble(ViewCenterYText);
            double titleX = ParseDouble(ViewTitleXText);
            double titleY = ParseDouble(ViewTitleYText);
            // Стандартное скрытое значение: изменение LabelLineLength в Revit может сбрасывать LabelOffset.
            double lineLength = DefaultTitleLineLengthMm;

            settings = new CreateViewsAndSheetsSettings();
            settings.StructureMode = _structureMode;
            settings.SourceViewId = SelectedSourceView != null ? SelectedSourceView.Id : ElementId.InvalidElementId;
            settings.SourceSheetId = SelectedSourceSheet != null ? SelectedSourceSheet.Id : ElementId.InvalidElementId;
            settings.CeilingSourceViewId = SelectedCeilingSourceView != null ? SelectedCeilingSourceView.Id : ElementId.InvalidElementId;
            settings.CeilingSourceSheetId = SelectedCeilingSourceSheet != null ? SelectedCeilingSourceSheet.Id : ElementId.InvalidElementId;
            settings.ViewportTypeId = SelectedViewportType != null ? SelectedViewportType.Id : ElementId.InvalidElementId;
            settings.TitleBlockTypeId = SelectedTitleBlockType != null ? SelectedTitleBlockType.Id : ElementId.InvalidElementId;
            settings.SheetBrowserParameterId = SheetBrowserParameterLevels.Count > 0 ? SheetBrowserParameterLevels[0].ParameterId : ElementId.InvalidElementId;
            settings.SheetBrowserParameterIds = BuildSheetBrowserParameterIds();
            settings.SheetBounds = ResolveCurrentSheetBounds();
            settings.FloorMappings = BuildFloorMappings();
            settings.Placement = new PlacementSettings();
            settings.Placement.CoordinateUnits = "мм";
            settings.Placement.ViewCenterXmm = centerX;
            settings.Placement.ViewCenterYmm = centerY;
            settings.Placement.ViewTitleXmm = titleX;
            settings.Placement.ViewTitleYmm = titleY;
            settings.Placement.TitleLineLengthMm = lineLength;
            settings.Placement.UseSourceSheetViewportPlacement = UseSourceSheetViewportPlacement;
            settings.Placement.UsePointSelectionForViewCenter = IsViewCenterPointMode;
            settings.Placement.UsePointSelectionForViewTitle = IsViewTitlePointMode;
            settings.Placement.SaveSettings = SaveSettings;
            settings.DetailCopy = new SheetDetailCopySettings();
            settings.DetailCopy.CopySheetWithDetailing = CopySheetWithDetailing;
            settings.DetailCopy.CopySchedules = CopySchedules;
            settings.DetailCopy.CopyLegends = CopyLegends;
            settings.DetailCopy.CopyDraftingViews = CopyDraftingViews;
            settings.DetailCopy.CopyDetailLines = CopyDetailLines;
            settings.DetailCopy.CopyFilledRegions = CopyFilledRegions;
            settings.DetailCopy.CopyTextNotes = CopyTextNotes;
            settings.DetailCopy.CopyGenericAnnotations = CopyGenericAnnotations;
            settings.DetailCopy.CopyImages = CopyImages;
            settings.SessionRows = BuildSessionRows();

            items = BuildItems();
            return true;
        }

        public CreateViewsAndSheetsSettings BuildSessionSettings()
        {
            double centerX = ParseDouble(ViewCenterXText);
            double centerY = ParseDouble(ViewCenterYText);
            double titleX = ParseDouble(ViewTitleXText);
            double titleY = ParseDouble(ViewTitleYText);

            CreateViewsAndSheetsSettings settings = new CreateViewsAndSheetsSettings();
            settings.StructureMode = _structureMode;
            settings.SourceViewId = SelectedSourceView != null ? SelectedSourceView.Id : ElementId.InvalidElementId;
            settings.SourceSheetId = SelectedSourceSheet != null ? SelectedSourceSheet.Id : ElementId.InvalidElementId;
            settings.CeilingSourceViewId = SelectedCeilingSourceView != null ? SelectedCeilingSourceView.Id : ElementId.InvalidElementId;
            settings.CeilingSourceSheetId = SelectedCeilingSourceSheet != null ? SelectedCeilingSourceSheet.Id : ElementId.InvalidElementId;
            settings.ViewportTypeId = SelectedViewportType != null ? SelectedViewportType.Id : ElementId.InvalidElementId;
            settings.TitleBlockTypeId = SelectedTitleBlockType != null ? SelectedTitleBlockType.Id : ElementId.InvalidElementId;
            settings.SheetBrowserParameterId = SheetBrowserParameterLevels.Count > 0 ? SheetBrowserParameterLevels[0].ParameterId : ElementId.InvalidElementId;
            settings.SheetBrowserParameterIds = BuildSheetBrowserParameterIds();
            settings.SheetBounds = ResolveCurrentSheetBounds();
            settings.FloorMappings = BuildFloorMappings();
            settings.Placement = new PlacementSettings();
            settings.Placement.CoordinateUnits = "мм";
            settings.Placement.ViewCenterXmm = centerX;
            settings.Placement.ViewCenterYmm = centerY;
            settings.Placement.ViewTitleXmm = titleX;
            settings.Placement.ViewTitleYmm = titleY;
            settings.Placement.TitleLineLengthMm = DefaultTitleLineLengthMm;
            settings.Placement.UseSourceSheetViewportPlacement = UseSourceSheetViewportPlacement;
            settings.Placement.UsePointSelectionForViewCenter = IsViewCenterPointMode;
            settings.Placement.UsePointSelectionForViewTitle = IsViewTitlePointMode;
            settings.Placement.SaveSettings = SaveSettings;
            settings.DetailCopy = new SheetDetailCopySettings();
            settings.DetailCopy.CopySheetWithDetailing = CopySheetWithDetailing;
            settings.DetailCopy.CopySchedules = CopySchedules;
            settings.DetailCopy.CopyLegends = CopyLegends;
            settings.DetailCopy.CopyDraftingViews = CopyDraftingViews;
            settings.DetailCopy.CopyDetailLines = CopyDetailLines;
            settings.DetailCopy.CopyFilledRegions = CopyFilledRegions;
            settings.DetailCopy.CopyTextNotes = CopyTextNotes;
            settings.DetailCopy.CopyGenericAnnotations = CopyGenericAnnotations;
            settings.DetailCopy.CopyImages = CopyImages;
            settings.SessionRows = BuildSessionRows();
            return settings;
        }

        public void ApplyPickedPoint(PlacementPointTarget target, double xMm, double yMm)
        {
            string xText = FormatDouble(xMm);
            string yText = FormatDouble(yMm);

            if (target == PlacementPointTarget.ViewCenter)
            {
                ViewCenterXText = xText;
                ViewCenterYText = yText;
                IsViewCenterManualMode = false;
                return;
            }

            if (target == PlacementPointTarget.ViewTitle)
            {
                ViewTitleXText = xText;
                ViewTitleYText = yText;
                IsViewTitleManualMode = false;
            }
        }

        public RevitElementItem GetPointSelectionSourceSheet()
        {
            if (IsMultiStoryStructure)
            {
                for (int i = 0; i < FloorMappings.Count; i++)
                {
                FloorSourceMappingRowViewModel mapping = FloorMappings[i];
                if (mapping != null && mapping.SelectedSourceSheet != null)
                {
                    return mapping.SelectedSourceSheet;
                }

                if (mapping != null && mapping.SelectedCeilingSourceSheet != null)
                {
                    return mapping.SelectedCeilingSourceSheet;
                }
            }
            }

            if (SelectedSourceSheet != null)
            {
                return SelectedSourceSheet;
            }

            return SelectedCeilingSourceSheet;
        }

        public SheetBounds GetPointSelectionSourceSheetBounds()
        {
            RevitElementItem sourceSheet = GetPointSelectionSourceSheet();
            if (sourceSheet != null && sourceSheet.SheetBounds != null)
            {
                return sourceSheet.SheetBounds;
            }

            if (SelectedTitleBlockType != null && SelectedTitleBlockType.SheetBounds != null)
            {
                return SelectedTitleBlockType.SheetBounds;
            }

            return null;
        }

        public void MoveRowToIndex(SheetCreationRowViewModel row, int targetIndex)
        {
            if (row == null || Rows == null || Rows.Count <= 1)
            {
                return;
            }

            int currentIndex = Rows.IndexOf(row);
            if (currentIndex < 0)
            {
                return;
            }

            if (targetIndex < 0)
            {
                targetIndex = 0;
            }

            if (targetIndex >= Rows.Count)
            {
                targetIndex = Rows.Count - 1;
            }

            if (currentIndex == targetIndex)
            {
                return;
            }

            Rows.Move(currentIndex, targetIndex);
            RenumberRows();
            RefreshValidation();
        }

        private void SetStructureMode(CreateViewsAndSheetsStructureMode mode)
        {
            if (_structureMode == mode)
            {
                return;
            }

            _structureMode = mode;
            OnPropertyChanged("IsSingleStoryStructure");
            OnPropertyChanged("IsMultiStoryStructure");

            ReloadCompatibleViewTemplates();
            EnsureRowsHaveDefaultFloorName();
            RefreshValidation();
        }

        private void RequestSettingsWindowInternal()
        {
            EventHandler handler = RequestSettingsWindow;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void RequestSheetTableImportInternal()
        {
            EventHandler handler = RequestSheetTableImport;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void RequestPointSelectionInternal(PlacementPointTarget target)
        {
            EventHandler<PlacementPointSelectionRequestEventArgs> handler = RequestPointSelection;
            if (handler == null)
            {
                return;
            }

            string prompt = target == PlacementPointTarget.ViewCenter
                ? "Укажите точку размещения центра вида на листе-образце."
                : "Укажите точку размещения заголовка вида на листе-образце.";

            handler(this, new PlacementPointSelectionRequestEventArgs(target, prompt));
        }

        private void ApplyInitialSelections(CreateViewsAndSheetsSettings initialSettings)
        {
            SelectedSourceView = FindById(StandardSourceViews, initialSettings != null ? initialSettings.SourceViewId : ElementId.InvalidElementId)
                                 ?? (StandardSourceViews.Count > 0 ? StandardSourceViews[0] : null);

            SelectedSourceSheet = FindById(SourceSheets, initialSettings != null ? initialSettings.SourceSheetId : ElementId.InvalidElementId)
                                  ?? (SourceSheets.Count > 0 ? SourceSheets[0] : null);

            SelectedCeilingSourceView = FindById(CeilingSourceViews, initialSettings != null ? initialSettings.CeilingSourceViewId : ElementId.InvalidElementId)
                                        ?? (CeilingSourceViews.Count > 0 ? CeilingSourceViews[0] : null);

            SelectedCeilingSourceSheet = FindById(SourceSheets, initialSettings != null ? initialSettings.CeilingSourceSheetId : ElementId.InvalidElementId)
                                         ?? (SourceSheets.Count > 0 ? SourceSheets[0] : null);

            SelectedViewportType = FindById(ViewportTypes, initialSettings != null ? initialSettings.ViewportTypeId : ElementId.InvalidElementId)
                                   ?? (ViewportTypes.Count > 0 ? ViewportTypes[0] : null);

            RevitElementItem savedTitleBlock = FindById(TitleBlockTypes, initialSettings != null ? initialSettings.TitleBlockTypeId : ElementId.InvalidElementId);
            if (savedTitleBlock != null)
            {
                SelectedTitleBlockType = savedTitleBlock;
            }
            else
            {
                ApplyTitleBlockFromSourceSheet();
            }

            SelectedSheetBrowserParameter = FindById(
                                                SheetBrowserParameters,
                                                initialSettings != null ? initialSettings.SheetBrowserParameterId : ElementId.InvalidElementId)
                                            ?? (SheetBrowserParameters.Count > 0 ? SheetBrowserParameters[0] : null);

            if (Rows.Count > 0)
            {
                AssignDefaultTemplate(Rows[0]);
                AssignDefaultFloorName(Rows[0]);
            }
        }

        private void InitializeFloorMappings(CreateViewsAndSheetsSettings initialSettings)
        {
            if (initialSettings != null && initialSettings.FloorMappings != null && initialSettings.FloorMappings.Count > 0)
            {
                for (int i = 0; i < initialSettings.FloorMappings.Count; i++)
                {
                    AddFloorMapping(initialSettings.FloorMappings[i]);
                }
            }

            if (FloorMappings.Count == 0)
            {
                AddFloorMapping(null);
            }
        }

        private void InitializeRows(CreateViewsAndSheetsSettings initialSettings)
        {
            if (initialSettings != null && initialSettings.SessionRows != null && initialSettings.SessionRows.Count > 0)
            {
                for (int i = 0; i < initialSettings.SessionRows.Count; i++)
                {
                    AddSessionRow(initialSettings.SessionRows[i]);
                }
            }

            if (Rows.Count == 0)
            {
                AddRowInternal(null);
            }

            RenumberRows();
            EnsureRowsHaveValidTemplateSelection();
        }

        private void AddSessionRow(SheetCreationSessionRow sessionRow)
        {
            if (sessionRow == null)
            {
                return;
            }

            SheetCreationRowViewModel row = CreateRow();
            row.PlanKind = sessionRow.PlanKind;
            row.FloorName = sessionRow.FloorName ?? string.Empty;
            row.ViewName = sessionRow.ViewName ?? string.Empty;
            row.ViewScaleText = string.IsNullOrWhiteSpace(sessionRow.ViewScaleText) ? "50" : sessionRow.ViewScaleText;
            row.SelectedViewTemplate = FindById(ViewTemplates, sessionRow.ViewTemplateId)
                                       ?? FindById(_allViewTemplates, sessionRow.ViewTemplateId)
                                       ?? row.SelectedViewTemplate;
            row.SheetNumber = sessionRow.SheetNumber ?? string.Empty;
            row.SheetName = sessionRow.SheetName ?? string.Empty;
            row.SheetBrowserParameterValue = sessionRow.SheetBrowserParameterValue ?? string.Empty;
            ApplySessionSheetBrowserParameterValues(row, sessionRow.SheetBrowserParameterValues);
            Rows.Add(row);
        }

        private void ApplySessionSheetBrowserParameterValues(
            SheetCreationRowViewModel row,
            IList<SheetBrowserParameterValueItem> savedValues)
        {
            if (row == null || savedValues == null || savedValues.Count == 0)
            {
                return;
            }

            row.EnsureSheetBrowserParameterValues(SheetBrowserParameterLevels);
            for (int i = 0; i < savedValues.Count; i++)
            {
                SheetBrowserParameterValueItem savedValue = savedValues[i];
                if (savedValue == null)
                {
                    continue;
                }

                SheetBrowserParameterValueViewModel targetValue = FindRowParameterValue(row, savedValue.ParameterId);
                if (targetValue == null && i < row.SheetBrowserParameterValues.Count)
                {
                    targetValue = row.SheetBrowserParameterValues[i];
                }

                if (targetValue != null)
                {
                    targetValue.Value = savedValue.Value ?? string.Empty;
                }
            }
        }

        private SheetBrowserParameterValueViewModel FindRowParameterValue(SheetCreationRowViewModel row, ElementId parameterId)
        {
            if (row == null || parameterId == null || parameterId == ElementId.InvalidElementId)
            {
                return null;
            }

            for (int i = 0; i < row.SheetBrowserParameterValues.Count; i++)
            {
                SheetBrowserParameterValueViewModel value = row.SheetBrowserParameterValues[i];
                if (value != null && RevitElementIdUtils.AreEqual(value.ParameterId, parameterId))
                {
                    return value;
                }
            }

            return null;
        }

        private void AddFloorMapping(FloorSourceMapping sourceMapping)
        {
            FloorSourceMappingRowViewModel row = new FloorSourceMappingRowViewModel();
            row.FloorName = sourceMapping != null ? sourceMapping.FloorName : string.Empty;
            row.SelectedSourceView = sourceMapping != null
                ? FindById(SourceViews, sourceMapping.SourceViewId)
                : null;
            row.SelectedSourceSheet = sourceMapping != null
                ? FindById(SourceSheets, sourceMapping.SourceSheetId)
                : null;
            row.SelectedCeilingSourceView = sourceMapping != null
                ? FindById(SourceViews, sourceMapping.CeilingSourceViewId)
                : null;
            row.SelectedCeilingSourceSheet = sourceMapping != null
                ? FindById(SourceSheets, sourceMapping.CeilingSourceSheetId)
                : null;

            row.PropertyChanged += FloorMappingRow_PropertyChanged;
            FloorMappings.Add(row);
            RefreshFloorNames();
        }

        private void DeleteFloorMapping(FloorSourceMappingRowViewModel row)
        {
            if (row == null || FloorMappings == null || !FloorMappings.Contains(row))
            {
                return;
            }

            row.PropertyChanged -= FloorMappingRow_PropertyChanged;
            FloorMappings.Remove(row);

            if (FloorMappings.Count == 0)
            {
                AddFloorMapping(null);
                RefreshValidation();
                return;
            }

            RefreshFloorNames();
            RefreshValidation();
        }

        private void RefreshFloorNames()
        {
            FloorNames.Clear();

            for (int i = 0; i < FloorMappings.Count; i++)
            {
                FloorSourceMappingRowViewModel row = FloorMappings[i];
                string floorName = row != null ? (row.FloorName ?? string.Empty).Trim() : string.Empty;
                if (string.IsNullOrWhiteSpace(floorName))
                {
                    continue;
                }

                if (!ContainsText(FloorNames, floorName))
                {
                    FloorNames.Add(floorName);
                }
            }

            EnsureRowsHaveDefaultFloorName();
        }

        private void FloorMappingRow_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_isRefreshingValidation)
            {
                return;
            }

            if (e != null && string.Equals(e.PropertyName, "FloorName", StringComparison.Ordinal))
            {
                RefreshFloorNames();
            }

            if (e != null &&
                (string.Equals(e.PropertyName, "SelectedSourceView", StringComparison.Ordinal) ||
                 string.Equals(e.PropertyName, "SelectedCeilingSourceView", StringComparison.Ordinal)))
            {
                EnsureRowsHaveValidTemplateSelection();
            }

            RefreshValidation();
        }

        private void ReloadCompatibleViewTemplates()
        {
            ViewTemplates.Clear();

            if (IsMultiStoryStructure)
            {
                for (int i = 0; i < _allViewTemplates.Count; i++)
                {
                    ViewTemplates.Add(_allViewTemplates[i]);
                }

                EnsureRowsHaveValidTemplateSelection();
                return;
            }

            if (IsSingleStoryStructure && SelectedSourceView == null && SelectedCeilingSourceView == null)
            {
                return;
            }

            for (int i = 0; i < _allViewTemplates.Count; i++)
            {
                RevitElementItem template = _allViewTemplates[i];
                if (template == null)
                {
                    continue;
                }

                bool isEmptyTemplate = template.Id == ElementId.InvalidElementId;
                bool matchesStandardView = SelectedSourceView != null && template.ViewType == SelectedSourceView.ViewType;
                bool matchesCeilingView = SelectedCeilingSourceView != null && template.ViewType == SelectedCeilingSourceView.ViewType;
                if (isEmptyTemplate || matchesStandardView || matchesCeilingView)
                {
                    ViewTemplates.Add(template);
                }
            }

            EnsureRowsHaveValidTemplateSelection();
        }

        private void ApplyTitleBlockFromSourceSheet()
        {
            RevitElementItem sourceSheet = SelectedSourceSheet ?? SelectedCeilingSourceSheet;
            if (sourceSheet == null || sourceSheet.RelatedElementId == null ||
                sourceSheet.RelatedElementId == ElementId.InvalidElementId)
            {
                return;
            }

            RevitElementItem titleBlockType = FindById(TitleBlockTypes, sourceSheet.RelatedElementId);
            if (titleBlockType != null)
            {
                _selectedTitleBlockType = titleBlockType;
                OnPropertyChanged("SelectedTitleBlockType");
            }
        }

        private SheetBounds ResolveCurrentSheetBounds()
        {
            if (IsMultiStoryStructure)
            {
                for (int i = 0; i < FloorMappings.Count; i++)
                {
                    FloorSourceMappingRowViewModel mapping = FloorMappings[i];
                    if (mapping != null && mapping.SelectedSourceSheet != null && mapping.SelectedSourceSheet.SheetBounds != null)
                    {
                        return mapping.SelectedSourceSheet.SheetBounds;
                    }

                    if (mapping != null && mapping.SelectedCeilingSourceSheet != null && mapping.SelectedCeilingSourceSheet.SheetBounds != null)
                    {
                        return mapping.SelectedCeilingSourceSheet.SheetBounds;
                    }
                }
            }

            if (SelectedSourceSheet != null && SelectedSourceSheet.SheetBounds != null)
            {
                return SelectedSourceSheet.SheetBounds;
            }

            if (SelectedCeilingSourceSheet != null && SelectedCeilingSourceSheet.SheetBounds != null)
            {
                return SelectedCeilingSourceSheet.SheetBounds;
            }

            if (SelectedTitleBlockType != null && SelectedTitleBlockType.SheetBounds != null)
            {
                return SelectedTitleBlockType.SheetBounds;
            }

            return null;
        }

        private List<FloorSourceMapping> BuildFloorMappings()
        {
            List<FloorSourceMapping> result = new List<FloorSourceMapping>();
            for (int i = 0; i < FloorMappings.Count; i++)
            {
                FloorSourceMappingRowViewModel row = FloorMappings[i];
                if (row == null)
                {
                    continue;
                }

                string floorName = (row.FloorName ?? string.Empty).Trim();
                bool hasData = !string.IsNullOrWhiteSpace(floorName) ||
                               row.SelectedSourceView != null ||
                               row.SelectedSourceSheet != null ||
                               row.SelectedCeilingSourceView != null ||
                               row.SelectedCeilingSourceSheet != null;
                if (!hasData)
                {
                    continue;
                }

                FloorSourceMapping mapping = new FloorSourceMapping();
                mapping.FloorId = ElementId.InvalidElementId;
                mapping.FloorName = floorName;
                mapping.SourceViewId = row.SelectedSourceView != null ? row.SelectedSourceView.Id : ElementId.InvalidElementId;
                mapping.SourceSheetId = row.SelectedSourceSheet != null ? row.SelectedSourceSheet.Id : ElementId.InvalidElementId;
                mapping.CeilingSourceViewId = row.SelectedCeilingSourceView != null ? row.SelectedCeilingSourceView.Id : ElementId.InvalidElementId;
                mapping.CeilingSourceSheetId = row.SelectedCeilingSourceSheet != null ? row.SelectedCeilingSourceSheet.Id : ElementId.InvalidElementId;
                mapping.SheetBounds = row.SelectedSourceSheet != null ? row.SelectedSourceSheet.SheetBounds : null;
                mapping.CeilingSheetBounds = row.SelectedCeilingSourceSheet != null ? row.SelectedCeilingSourceSheet.SheetBounds : null;
                result.Add(mapping);
            }

            return result;
        }

        private FloorSourceMappingRowViewModel FindFloorMappingForName(string floorName)
        {
            string cleanFloorName = (floorName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(cleanFloorName))
            {
                return null;
            }

            for (int i = 0; i < FloorMappings.Count; i++)
            {
                FloorSourceMappingRowViewModel mapping = FloorMappings[i];
                if (mapping == null)
                {
                    continue;
                }

                string mappingFloorName = (mapping.FloorName ?? string.Empty).Trim();
                if (string.Equals(mappingFloorName, cleanFloorName, StringComparison.OrdinalIgnoreCase))
                {
                    return mapping;
                }
            }

            return null;
        }

        public void ImportSheetTableRows(IList<SheetTableImportRow> importedRows)
        {
            if (importedRows == null || importedRows.Count == 0)
            {
                return;
            }

            ClearRowsWithoutDefaultRow();

            for (int i = 0; i < importedRows.Count; i++)
            {
                SheetTableImportRow importedRow = importedRows[i];
                if (importedRow == null)
                {
                    continue;
                }

                SheetCreationRowViewModel row = CreateImportedSheetTableRow(importedRow);
                Rows.Add(row);
            }

            if (Rows.Count == 0)
            {
                AddRowInternal(null);
            }

            RenumberRows();
            RefreshValidation();
        }

        public void ClearAllRows()
        {
            ClearRowsWithoutDefaultRow();
            AddRowInternal(null);
            RenumberRows();
            RefreshValidation();
        }

        public void CopySheetNamesToViewNames()
        {
            if (Rows == null)
            {
                return;
            }

            for (int i = 0; i < Rows.Count; i++)
            {
                SheetCreationRowViewModel row = Rows[i];
                if (row == null)
                {
                    continue;
                }

                string sheetName = (row.SheetName ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(sheetName))
                {
                    row.ViewName = sheetName;
                }
            }

            RefreshValidation();
        }

        private SheetCreationRowViewModel CreateImportedSheetTableRow(SheetTableImportRow importedRow)
        {
            SheetCreationRowViewModel row = CreateRow();
            row.PropertyChanged -= Row_PropertyChanged;

            // Блок импорта Excel: заполняем только данные из таблицы, остальные рабочие поля остаются пустыми.
            row.FloorName = string.Empty;
            row.ViewName = string.Empty;
            row.ViewScaleText = string.Empty;
            row.SelectedViewTemplate = null;
            row.SheetNumber = importedRow.SheetNumber ?? string.Empty;
            row.SheetName = importedRow.SheetName ?? string.Empty;
            ApplyImportedSheetSection(row, importedRow.SectionName);

            row.PropertyChanged += Row_PropertyChanged;
            return row;
        }

        private void ApplyImportedSheetSection(SheetCreationRowViewModel row, string sectionName)
        {
            if (row == null)
            {
                return;
            }

            string cleanSectionName = (sectionName ?? string.Empty).Trim();
            row.SheetBrowserParameterValue = cleanSectionName;
            row.EnsureSheetBrowserParameterValues(SheetBrowserParameterLevels);

            // Раздел из таблицы записывается в первый параметр группирования листов.
            if (row.SheetBrowserParameterValues.Count > 0 && row.SheetBrowserParameterValues[0] != null)
            {
                row.SheetBrowserParameterValues[0].Value = cleanSectionName;
            }
        }

        private void ClearRowsWithoutDefaultRow()
        {
            if (Rows == null)
            {
                return;
            }

            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i] != null)
                {
                    Rows[i].PropertyChanged -= Row_PropertyChanged;
                }
            }

            Rows.Clear();
        }

        private void AddRowAtEnd()
        {
            SheetCreationRowViewModel sourceRow = Rows != null && Rows.Count > 0 ? Rows[Rows.Count - 1] : null;
            AddRowInternal(sourceRow);
            RefreshValidation();
        }

        private void InsertRowAfter(SheetCreationRowViewModel currentRow)
        {
            if (currentRow == null)
            {
                AddRowAtEnd();
                return;
            }

            int index = Rows.IndexOf(currentRow);
            if (index < 0)
            {
                AddRowAtEnd();
                return;
            }

            SheetCreationRowViewModel newRow = CreateRow();
            CopyRowValues(currentRow, newRow);
            Rows.Insert(index + 1, newRow);
            RenumberRows();
            RefreshValidation();
        }

        private void MoveRow(SheetCreationRowViewModel row, int direction)
        {
            if (!CanMoveRow(row, direction))
            {
                return;
            }

            int currentIndex = Rows.IndexOf(row);
            int targetIndex = currentIndex + direction;
            Rows.Move(currentIndex, targetIndex);
            RenumberRows();
            RefreshValidation();
        }

        private bool CanMoveRow(SheetCreationRowViewModel row, int direction)
        {
            if (row == null || Rows == null || Rows.Count <= 1)
            {
                return false;
            }

            int currentIndex = Rows.IndexOf(row);
            if (currentIndex < 0)
            {
                return false;
            }

            int targetIndex = currentIndex + direction;
            return targetIndex >= 0 && targetIndex < Rows.Count;
        }

        private void DeleteRow(SheetCreationRowViewModel row)
        {
            if (row == null || Rows == null || Rows.IndexOf(row) < 0)
            {
                return;
            }

            row.PropertyChanged -= Row_PropertyChanged;
            Rows.Remove(row);

            if (Rows.Count == 0)
            {
                AddRowInternal(null);
            }

            RenumberRows();
            RefreshValidation();
        }

        private void AddRowInternal(SheetCreationRowViewModel sourceRow)
        {
            SheetCreationRowViewModel row = CreateRow();
            if (sourceRow != null)
            {
                CopyRowValues(sourceRow, row);
            }
            else
            {
                AssignDefaultFloorName(row);
                EnsureRowHasValidTemplateSelection(row);
            }

            Rows.Add(row);
            RenumberRows();
        }

        private void CopyRowValues(SheetCreationRowViewModel sourceRow, SheetCreationRowViewModel targetRow)
        {
            if (sourceRow == null || targetRow == null)
            {
                return;
            }

            targetRow.FloorName = sourceRow.FloorName;
            targetRow.PlanKind = sourceRow.PlanKind;
            targetRow.ViewName = sourceRow.ViewName;
            targetRow.ViewScaleText = sourceRow.ViewScaleText;
            targetRow.SelectedViewTemplate = sourceRow.SelectedViewTemplate;
            targetRow.SheetNumber = sourceRow.SheetNumber;
            targetRow.SheetName = sourceRow.SheetName;
            targetRow.SheetBrowserParameterValue = sourceRow.SheetBrowserParameterValue;
            CopySheetBrowserParameterValues(sourceRow, targetRow);
        }

        private SheetCreationRowViewModel CreateRow()
        {
            SheetCreationRowViewModel row = new SheetCreationRowViewModel();
            row.EnsureSheetBrowserParameterValues(SheetBrowserParameterLevels);
            AssignDefaultTemplate(row);
            row.PropertyChanged += Row_PropertyChanged;
            return row;
        }

        private void AssignDefaultTemplate(SheetCreationRowViewModel row)
        {
            if (row == null)
            {
                return;
            }

            RevitElementItem sourceView = GetTemplateSourceView(row);
            RevitElementItem firstRealTemplate = null;
            for (int i = 0; i < ViewTemplates.Count; i++)
            {
                RevitElementItem item = ViewTemplates[i];
                if (item != null && item.Id != ElementId.InvalidElementId)
                {
                    if (firstRealTemplate == null)
                    {
                        firstRealTemplate = item;
                    }

                    if (sourceView == null || item.ViewType == sourceView.ViewType)
                    {
                        row.SelectedViewTemplate = item;
                        return;
                    }
                }
            }

            row.SelectedViewTemplate = firstRealTemplate;
        }

        private void AssignDefaultFloorName(SheetCreationRowViewModel row)
        {
            if (row == null || !IsMultiStoryStructure || !string.IsNullOrWhiteSpace(row.FloorName) || FloorNames.Count == 0)
            {
                return;
            }

            row.FloorName = FloorNames[0];
        }

        private void EnsureRowsHaveDefaultFloorName()
        {
            if (!IsMultiStoryStructure || Rows == null || FloorNames.Count == 0)
            {
                return;
            }

            for (int i = 0; i < Rows.Count; i++)
            {
                AssignDefaultFloorName(Rows[i]);
            }
        }

        private void EnsureRowsHaveValidTemplateSelection()
        {
            if (Rows == null)
            {
                return;
            }

            for (int i = 0; i < Rows.Count; i++)
            {
                EnsureRowHasValidTemplateSelection(Rows[i]);
            }
        }

        private void EnsureRowHasValidTemplateSelection(SheetCreationRowViewModel row)
        {
            if (row == null)
            {
                return;
            }

            RevitElementItem sourceView = GetTemplateSourceView(row);
            if (row.SelectedViewTemplate == null ||
                row.SelectedViewTemplate.Id == ElementId.InvalidElementId ||
                !IsTemplateAvailable(row.SelectedViewTemplate) ||
                !IsTemplateCompatibleWithSource(row.SelectedViewTemplate, sourceView))
            {
                AssignDefaultTemplate(row);
            }
        }

        private RevitElementItem GetTemplateSourceView(SheetCreationRowViewModel row)
        {
            if (!IsMultiStoryStructure)
            {
                return row != null && row.PlanKind == SheetPlanKind.CeilingPlan
                    ? SelectedCeilingSourceView
                    : SelectedSourceView;
            }

            if (row == null)
            {
                return null;
            }

            FloorSourceMappingRowViewModel mapping = FindFloorMappingForName(row.FloorName);
            if (mapping == null)
            {
                return null;
            }

            return row.PlanKind == SheetPlanKind.CeilingPlan
                ? mapping.SelectedCeilingSourceView
                : mapping.SelectedSourceView;
        }

        private void Row_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_isRefreshingValidation)
            {
                return;
            }

            if (e != null &&
                (string.Equals(e.PropertyName, "RowError", StringComparison.Ordinal) ||
                 string.Equals(e.PropertyName, "HasError", StringComparison.Ordinal) ||
                 string.Equals(e.PropertyName, "RowNumber", StringComparison.Ordinal)))
            {
                return;
            }

            SheetCreationRowViewModel row = sender as SheetCreationRowViewModel;
            if (e != null &&
                (string.Equals(e.PropertyName, "FloorName", StringComparison.Ordinal) ||
                 string.Equals(e.PropertyName, "PlanKind", StringComparison.Ordinal)))
            {
                EnsureRowHasValidTemplateSelection(row);
            }

            RefreshValidation();
        }

        private void RenumberRows()
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                Rows[i].RowNumber = i + 1;
            }

            RaiseRowCommandCanExecuteChanged();
        }

        private void RaiseRowCommandCanExecuteChanged()
        {
            RaiseCanExecuteChanged(MoveRowUpCommand);
            RaiseCanExecuteChanged(MoveRowDownCommand);
            RaiseCanExecuteChanged(DeleteRowCommand);
            RaiseCanExecuteChanged(ClearRowsCommand);
            RaiseCanExecuteChanged(CopySheetNamesToViewNamesCommand);
        }

        private void RaiseCanExecuteChanged(ICommand command)
        {
            RelayCommand relayCommand = command as RelayCommand;
            if (relayCommand != null)
            {
                relayCommand.RaiseCanExecuteChanged();
            }
        }

        private void AcceptWindow()
        {
            RefreshValidation();
            if (!CanCreate)
            {
                return;
            }

            _isAccepted = true;
            EventHandler handler = RequestClose;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void CancelWindow()
        {
            _isAccepted = false;
            EventHandler handler = RequestClose;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        private void RefreshValidation()
        {
            if (_isRefreshingValidation)
            {
                return;
            }

            try
            {
                _isRefreshingValidation = true;

                ValidationState validationState = ValidateAllRows();
                ApplyValidationState(validationState);
            }
            finally
            {
                _isRefreshingValidation = false;
            }
        }

        private ValidationState ValidateAllRows()
        {
            ValidationState state = new ValidationState();
            ClearRowErrors();

            bool hasStandardRows = HasFilledRowsForPlanKind(SheetPlanKind.StandardPlan);
            bool hasCeilingRows = HasFilledRowsForPlanKind(SheetPlanKind.CeilingPlan);

            if (IsSingleStoryStructure && hasStandardRows && SelectedSourceView == null)
            {
                state.Errors.Add("Не выбран вид-образец стандартного плана.");
            }

            if (IsSingleStoryStructure && hasStandardRows && SelectedSourceSheet == null)
            {
                state.Errors.Add("Не выбран лист-образец стандартного плана.");
            }

            if (IsSingleStoryStructure && hasCeilingRows && SelectedCeilingSourceView == null)
            {
                state.Errors.Add("Не выбран вид-образец плана потолков.");
            }

            if (IsSingleStoryStructure && hasCeilingRows && SelectedCeilingSourceSheet == null)
            {
                state.Errors.Add("Не выбран лист-образец плана потолков.");
            }

            if (!UseSourceSheetViewportPlacement && SelectedViewportType == null)
            {
                state.Errors.Add("Не выбран тип видового экрана.");
            }

            if (SelectedTitleBlockType == null)
            {
                state.Errors.Add("Не выбрана основная надпись.");
            }

            ValidateFloorMappings(state);

            SheetBounds bounds = ResolveCurrentSheetBounds();
            if (!UseSourceSheetViewportPlacement && bounds == null)
            {
                state.Errors.Add("Не удалось определить габарит листа по выбранной основной надписи или листу-образцу.");
            }

            if (!UseSourceSheetViewportPlacement)
            {
                ValidatePlacementValues(bounds, state);
            }

            ValidateRows(state);
            UpdateStatusText(state);
            return state;
        }

        private void ValidateFloorMappings(ValidationState state)
        {
            if (!IsMultiStoryStructure)
            {
                return;
            }

            int completeCount = 0;
            HashSet<string> floorNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < FloorMappings.Count; i++)
            {
                FloorSourceMappingRowViewModel mapping = FloorMappings[i];
                if (mapping == null)
                {
                    continue;
                }

                string floorName = (mapping.FloorName ?? string.Empty).Trim();
                bool hasAnyData = !string.IsNullOrWhiteSpace(floorName) ||
                                  mapping.SelectedSourceView != null ||
                                  mapping.SelectedSourceSheet != null ||
                                  mapping.SelectedCeilingSourceView != null ||
                                  mapping.SelectedCeilingSourceSheet != null;
                if (!hasAnyData)
                {
                    continue;
                }

                string rowPrefix = "Сопоставление этажа " + (i + 1) + ": ";
                bool hasStandardData = mapping.SelectedSourceView != null || mapping.SelectedSourceSheet != null;
                bool hasCeilingData = mapping.SelectedCeilingSourceView != null || mapping.SelectedCeilingSourceSheet != null;
                bool hasCompleteStandardData = mapping.SelectedSourceView != null && mapping.SelectedSourceSheet != null;
                bool hasCompleteCeilingData = mapping.SelectedCeilingSourceView != null && mapping.SelectedCeilingSourceSheet != null;

                if (string.IsNullOrWhiteSpace(floorName))
                {
                    state.Errors.Add(rowPrefix + "не заполнено поле Этаж.");
                }
                else if (!floorNames.Add(floorName))
                {
                    state.Errors.Add(rowPrefix + "этаж повторяется в списке сопоставлений.");
                }

                if (hasStandardData && mapping.SelectedSourceView == null)
                {
                    state.Errors.Add(rowPrefix + "не выбран вид-образец для стандартного плана.");
                }

                if (hasStandardData && mapping.SelectedSourceSheet == null)
                {
                    state.Errors.Add(rowPrefix + "не выбран лист-образец для стандартного плана.");
                }

                if (hasCeilingData && mapping.SelectedCeilingSourceView == null)
                {
                    state.Errors.Add(rowPrefix + "не выбран вид-образец для плана потолков.");
                }

                if (hasCeilingData && mapping.SelectedCeilingSourceSheet == null)
                {
                    state.Errors.Add(rowPrefix + "не выбран лист-образец для плана потолков.");
                }

                if (!string.IsNullOrWhiteSpace(floorName) && (hasCompleteStandardData || hasCompleteCeilingData))
                {
                    completeCount++;
                }
            }

            if (completeCount == 0)
            {
                state.Errors.Add("Для многоэтажной структуры добавьте хотя бы одно заполненное сопоставление этажа.");
            }
        }

        private void ValidatePlacementValues(SheetBounds bounds, ValidationState state)
        {
            double centerX;
            double centerY;
            double titleX;
            double titleY;

            if (!TryParseDouble(ViewCenterXText, out centerX))
            {
                state.Errors.Add("X центра вида должен быть числом.");
            }

            if (!TryParseDouble(ViewCenterYText, out centerY))
            {
                state.Errors.Add("Y центра вида должен быть числом.");
            }

            if (!TryParseDouble(ViewTitleXText, out titleX))
            {
                state.Errors.Add("X заголовка должен быть числом.");
            }

            if (!TryParseDouble(ViewTitleYText, out titleY))
            {
                state.Errors.Add("Y заголовка должен быть числом.");
            }

            if (bounds == null)
            {
                return;
            }

            if (TryParseDouble(ViewCenterXText, out centerX) &&
                TryParseDouble(ViewCenterYText, out centerY) &&
                !bounds.ContainsPointMm(centerX, centerY))
            {
                state.Errors.Add("Координаты центра вида выходят за габарит листа.");
            }

            if (TryParseDouble(ViewTitleXText, out titleX) &&
                TryParseDouble(ViewTitleYText, out titleY) &&
                !bounds.ContainsPointMm(titleX, titleY))
            {
                state.Errors.Add("Координаты заголовка выходят за габарит листа.");
            }

        }

        private void ValidateRows(ValidationState state)
        {
            int filledCount = 0;
            HashSet<string> tableViewNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> tableSheetNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < Rows.Count; i++)
            {
                SheetCreationRowViewModel row = Rows[i];
                if (!HasSessionRowData(row))
                {
                    continue;
                }

                filledCount++;
                List<string> rowErrors = new List<string>();

                string viewName = (row.ViewName ?? string.Empty).Trim();
                string sheetNumber = (row.SheetNumber ?? string.Empty).Trim();
                string sheetName = (row.SheetName ?? string.Empty).Trim();
                string floorName = (row.FloorName ?? string.Empty).Trim();
                FloorSourceMappingRowViewModel rowFloorMapping = null;
                bool isCeilingPlan = row.PlanKind == SheetPlanKind.CeilingPlan;
                RevitElementItem templateSourceView = isCeilingPlan ? SelectedCeilingSourceView : SelectedSourceView;

                if (IsMultiStoryStructure)
                {
                    if (string.IsNullOrWhiteSpace(floorName))
                    {
                        rowErrors.Add("этаж не заполнен");
                    }
                    else
                    {
                        rowFloorMapping = FindFloorMappingForName(floorName);
                        if (rowFloorMapping == null)
                        {
                            rowErrors.Add("этаж не найден в сопоставлении");
                        }
                        else
                        {
                            templateSourceView = isCeilingPlan
                                ? rowFloorMapping.SelectedCeilingSourceView
                                : rowFloorMapping.SelectedSourceView;

                            if (templateSourceView == null)
                            {
                                rowErrors.Add(isCeilingPlan
                                    ? "для этажа не выбран вид-образец плана потолков"
                                    : "для этажа не выбран вид-образец стандартного плана");
                            }

                            RevitElementItem sourceSheet = isCeilingPlan
                                ? rowFloorMapping.SelectedCeilingSourceSheet
                                : rowFloorMapping.SelectedSourceSheet;
                            if (sourceSheet == null)
                            {
                                rowErrors.Add(isCeilingPlan
                                    ? "для этажа не выбран лист-образец плана потолков"
                                    : "для этажа не выбран лист-образец стандартного плана");
                            }
                        }
                    }
                }
                else
                {
                    if (templateSourceView == null)
                    {
                        rowErrors.Add(isCeilingPlan
                            ? "не выбран вид-образец плана потолков"
                            : "не выбран вид-образец стандартного плана");
                    }

                    RevitElementItem sourceSheet = isCeilingPlan ? SelectedCeilingSourceSheet : SelectedSourceSheet;
                    if (sourceSheet == null)
                    {
                        rowErrors.Add(isCeilingPlan
                            ? "не выбран лист-образец плана потолков"
                            : "не выбран лист-образец стандартного плана");
                    }
                }

                if (string.IsNullOrWhiteSpace(viewName))
                {
                    rowErrors.Add("имя вида не заполнено");
                }

                if (string.IsNullOrWhiteSpace(sheetNumber))
                {
                    rowErrors.Add("номер листа не заполнен");
                }

                if (string.IsNullOrWhiteSpace(sheetName))
                {
                    rowErrors.Add("имя листа не заполнено");
                }

                int scale;
                if (row.IsScaleEnabled && (!TryParseScale(row.ViewScaleText, out scale) || scale <= 0))
                {
                    rowErrors.Add("масштаб должен быть положительным целым числом");
                }

                if (row.SelectedViewTemplate == null || row.SelectedViewTemplate.Id == ElementId.InvalidElementId)
                {
                    rowErrors.Add("шаблон вида не выбран");
                }
                else if (!IsTemplateAvailable(row.SelectedViewTemplate))
                {
                    rowErrors.Add("шаблон вида несовместим с видом-образцом");
                }
                else if (!IsTemplateCompatibleWithSource(row.SelectedViewTemplate, templateSourceView))
                {
                    rowErrors.Add("шаблон вида не совместим с выбранным видом-образцом");
                }
                else if (row.SelectedViewTemplate.ControlsScale)
                {
                    state.Warnings.Add("Строка " + row.RowNumber + ": масштаб будет задан выбранным шаблоном вида.");
                }

                if (!string.IsNullOrWhiteSpace(viewName))
                {
                    if (!tableViewNames.Add(viewName))
                    {
                        rowErrors.Add("имя вида повторяется в таблице");
                    }

                    if (_existingViewNames.Contains(viewName))
                    {
                        rowErrors.Add("вид с таким именем уже существует в документе");
                    }
                }

                if (!string.IsNullOrWhiteSpace(sheetNumber))
                {
                    if (!tableSheetNumbers.Add(sheetNumber))
                    {
                        rowErrors.Add("номер листа повторяется в таблице");
                    }

                    if (_existingSheetNumbers.Contains(sheetNumber))
                    {
                        rowErrors.Add("лист с таким номером уже существует в документе");
                    }
                }

                if (rowErrors.Count > 0)
                {
                    string rowError = "Строка " + row.RowNumber + ": " + string.Join("; ", rowErrors);
                    row.RowError = rowError;
                    state.Errors.Add(rowError);
                }
            }

            if (filledCount == 0)
            {
                state.Errors.Add("Нет заполненных строк для создания.");
            }
        }

        private void ApplyValidationState(ValidationState state)
        {
            ValidationSummary = BuildValidationText(state.Errors, state.Warnings);
            CanCreate = state.Errors.Count == 0;
        }

        private string BuildValidationText(IList<string> errors, IList<string> warnings)
        {
            StringBuilder builder = new StringBuilder();

            if (errors != null && errors.Count > 0)
            {
                builder.AppendLine("Ошибки:");
                for (int i = 0; i < errors.Count; i++)
                {
                    builder.AppendLine("- " + errors[i]);
                }
            }

            if (warnings != null && warnings.Count > 0)
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.AppendLine("Предупреждения:");
                for (int i = 0; i < warnings.Count; i++)
                {
                    builder.AppendLine("- " + warnings[i]);
                }
            }

            if (builder.Length == 0)
            {
                builder.Append("Проверка пройдена. Можно создавать виды и листы.");
            }

            return builder.ToString().Trim();
        }

        private void UpdateStatusText(ValidationState state)
        {
            int filledCount = 0;
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i] != null && Rows[i].IsFilled)
                {
                    filledCount++;
                }
            }

            StatusText = "Строк: " + Rows.Count +
                         " | Заполнено: " + filledCount +
                         " | Ошибок: " + state.Errors.Count;
        }

        private List<SheetCreationItem> BuildItems()
        {
            List<SheetCreationItem> result = new List<SheetCreationItem>();
            for (int i = 0; i < Rows.Count; i++)
            {
                SheetCreationRowViewModel row = Rows[i];
                if (row == null || !row.IsFilled)
                {
                    continue;
                }

                int scale;
                TryParseScale(row.ViewScaleText, out scale);

                SheetCreationItem item = new SheetCreationItem();
                item.RowNumber = row.RowNumber;
                item.PlanKind = row.PlanKind;
                item.FloorId = ElementId.InvalidElementId;
                item.FloorName = (row.FloorName ?? string.Empty).Trim();
                item.ViewName = (row.ViewName ?? string.Empty).Trim();
                item.ViewScale = scale;
                item.ViewTemplateId = row.SelectedViewTemplate != null ? row.SelectedViewTemplate.Id : ElementId.InvalidElementId;
                item.SheetNumber = (row.SheetNumber ?? string.Empty).Trim();
                item.SheetName = (row.SheetName ?? string.Empty).Trim();
                item.SheetBrowserParameterValue = (row.SheetBrowserParameterValue ?? string.Empty).Trim();
                item.SheetBrowserParameterValues = BuildSheetBrowserParameterValues(row);
                result.Add(item);
            }

            return result;
        }

        private List<SheetCreationSessionRow> BuildSessionRows()
        {
            List<SheetCreationSessionRow> result = new List<SheetCreationSessionRow>();
            for (int i = 0; i < Rows.Count; i++)
            {
                SheetCreationRowViewModel row = Rows[i];
                if (row == null || !row.IsFilled)
                {
                    continue;
                }

                SheetCreationSessionRow sessionRow = new SheetCreationSessionRow();
                sessionRow.PlanKind = row.PlanKind;
                sessionRow.FloorName = row.FloorName ?? string.Empty;
                sessionRow.ViewName = row.ViewName ?? string.Empty;
                sessionRow.ViewScaleText = row.ViewScaleText ?? string.Empty;
                sessionRow.ViewTemplateId = row.SelectedViewTemplate != null ? row.SelectedViewTemplate.Id : ElementId.InvalidElementId;
                sessionRow.SheetNumber = row.SheetNumber ?? string.Empty;
                sessionRow.SheetName = row.SheetName ?? string.Empty;
                sessionRow.SheetBrowserParameterValue = row.SheetBrowserParameterValue ?? string.Empty;
                sessionRow.SheetBrowserParameterValues = BuildSheetBrowserParameterValues(row);
                result.Add(sessionRow);
            }

            return result;
        }

        private bool HasSessionRowData(SheetCreationRowViewModel row)
        {
            if (row == null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(row.FloorName) ||
                !string.IsNullOrWhiteSpace(row.ViewName) ||
                !string.IsNullOrWhiteSpace(row.SheetNumber) ||
                !string.IsNullOrWhiteSpace(row.SheetName) ||
                !string.IsNullOrWhiteSpace(row.SheetBrowserParameterValue))
            {
                return true;
            }

            for (int i = 0; i < row.SheetBrowserParameterValues.Count; i++)
            {
                SheetBrowserParameterValueViewModel parameterValue = row.SheetBrowserParameterValues[i];
                if (parameterValue != null && !string.IsNullOrWhiteSpace(parameterValue.Value))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasFilledRowsForPlanKind(SheetPlanKind planKind)
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                SheetCreationRowViewModel row = Rows[i];
                if (row != null && row.PlanKind == planKind && HasSessionRowData(row))
                {
                    return true;
                }
            }

            return false;
        }

        private void ClearRowErrors()
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i] != null)
                {
                    Rows[i].RowError = string.Empty;
                }
            }
        }

        private bool IsTemplateAvailable(RevitElementItem template)
        {
            if (template == null)
            {
                return false;
            }

            for (int i = 0; i < ViewTemplates.Count; i++)
            {
                RevitElementItem available = ViewTemplates[i];
                if (available != null && RevitElementIdUtils.AreEqual(available.Id, template.Id))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsTemplateCompatibleWithSource(RevitElementItem template, RevitElementItem sourceView)
        {
            if (template == null || template.Id == ElementId.InvalidElementId)
            {
                return false;
            }

            if (sourceView == null)
            {
                return true;
            }

            return template.ViewType == sourceView.ViewType;
        }

        private bool ContainsText(IEnumerable<string> items, string text)
        {
            if (items == null)
            {
                return false;
            }

            string cleanText = (text ?? string.Empty).Trim();
            foreach (string item in items)
            {
                if (string.Equals((item ?? string.Empty).Trim(), cleanText, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private RevitElementItem FindById(IEnumerable<RevitElementItem> items, ElementId id)
        {
            if (items == null || id == null || id == ElementId.InvalidElementId)
            {
                return null;
            }

            foreach (RevitElementItem item in items)
            {
                if (item != null && RevitElementIdUtils.AreEqual(item.Id, id))
                {
                    return item;
                }
            }

            return null;
        }

        private void FillCollection(ObservableCollection<RevitElementItem> target, IList<RevitElementItem> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                target.Add(source[i]);
            }
        }

        private void PopulateSourceViewGroups(IList<RevitElementItem> source)
        {
            StandardSourceViews.Clear();
            CeilingSourceViews.Clear();

            if (source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                RevitElementItem item = source[i];
                if (item == null)
                {
                    continue;
                }

                if (IsCeilingPlanSourceView(item))
                {
                    CeilingSourceViews.Add(item);
                    continue;
                }

                if (IsLevelBasedStandardSourceView(item))
                {
                    StandardSourceViews.Add(item);
                }
            }
        }

        private bool IsCeilingPlanSourceView(RevitElementItem item)
        {
            return item != null && item.ViewType == ViewType.CeilingPlan;
        }

        private bool IsLevelBasedStandardSourceView(RevitElementItem item)
        {
            if (item == null)
            {
                return false;
            }

            string viewTypeName = item.ViewType.ToString();
            return item.ViewType == ViewType.FloorPlan ||
                   item.ViewType == ViewType.EngineeringPlan ||
                   item.ViewType == ViewType.AreaPlan ||
                   string.Equals(viewTypeName, "StructuralPlan", StringComparison.OrdinalIgnoreCase);
        }

        private void FillParameterValuesMap(
            Dictionary<long, List<string>> target,
            Dictionary<long, List<string>> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            foreach (KeyValuePair<long, List<string>> pair in source)
            {
                List<string> values = new List<string>();
                if (pair.Value != null)
                {
                    for (int i = 0; i < pair.Value.Count; i++)
                    {
                        values.Add(pair.Value[i]);
                    }
                }

                target[pair.Key] = values;
            }
        }

        private void InitializeSheetBrowserParameterLevels(IList<RevitElementItem> sheetBrowserParameters)
        {
            SheetBrowserParameterLevels.Clear();
            SheetBrowserParameterValues.Clear();

            if (sheetBrowserParameters == null)
            {
                return;
            }

            for (int i = 0; i < sheetBrowserParameters.Count; i++)
            {
                RevitElementItem parameterItem = sheetBrowserParameters[i];
                if (parameterItem == null || parameterItem.Id == null || parameterItem.Id == ElementId.InvalidElementId)
                {
                    continue;
                }

                long key = RevitElementIdUtils.GetElementIdValue(parameterItem.Id);
                List<string> values;
                if (!_sheetBrowserParameterValuesById.TryGetValue(key, out values))
                {
                    values = new List<string>();
                }

                SheetBrowserParameterLevelViewModel level = new SheetBrowserParameterLevelViewModel(i + 1, parameterItem, values);
                SheetBrowserParameterLevels.Add(level);
            }
        }

        private List<ElementId> BuildSheetBrowserParameterIds()
        {
            List<ElementId> result = new List<ElementId>();
            for (int i = 0; i < SheetBrowserParameterLevels.Count; i++)
            {
                SheetBrowserParameterLevelViewModel level = SheetBrowserParameterLevels[i];
                if (level == null || level.ParameterId == null || level.ParameterId == ElementId.InvalidElementId)
                {
                    continue;
                }

                result.Add(level.ParameterId);
            }

            return result;
        }

        private void CopySheetBrowserParameterValues(SheetCreationRowViewModel sourceRow, SheetCreationRowViewModel targetRow)
        {
            if (sourceRow == null || targetRow == null)
            {
                return;
            }

            targetRow.EnsureSheetBrowserParameterValues(SheetBrowserParameterLevels);
            for (int i = 0; i < sourceRow.SheetBrowserParameterValues.Count && i < targetRow.SheetBrowserParameterValues.Count; i++)
            {
                SheetBrowserParameterValueViewModel sourceValue = sourceRow.SheetBrowserParameterValues[i];
                SheetBrowserParameterValueViewModel targetValue = targetRow.SheetBrowserParameterValues[i];
                if (sourceValue != null && targetValue != null)
                {
                    targetValue.Value = sourceValue.Value;
                }
            }
        }

        private List<SheetBrowserParameterValueItem> BuildSheetBrowserParameterValues(SheetCreationRowViewModel row)
        {
            List<SheetBrowserParameterValueItem> result = new List<SheetBrowserParameterValueItem>();
            if (row == null)
            {
                return result;
            }

            row.EnsureSheetBrowserParameterValues(SheetBrowserParameterLevels);
            for (int i = 0; i < row.SheetBrowserParameterValues.Count; i++)
            {
                SheetBrowserParameterValueViewModel rowValue = row.SheetBrowserParameterValues[i];
                if (rowValue == null || rowValue.ParameterId == null || rowValue.ParameterId == ElementId.InvalidElementId)
                {
                    continue;
                }

                SheetBrowserParameterValueItem item = new SheetBrowserParameterValueItem();
                item.ParameterId = rowValue.ParameterId;
                item.ParameterName = rowValue.ParameterName;
                item.Value = (rowValue.Value ?? string.Empty).Trim();
                result.Add(item);
            }

            return result;
        }

        private void RefreshSheetBrowserParameterValues()
        {
            if (SheetBrowserParameterValues == null)
            {
                return;
            }

            SheetBrowserParameterValues.Clear();
            if (!IsSheetBrowserParameterSelected)
            {
                return;
            }

            long key = RevitElementIdUtils.GetElementIdValue(SelectedSheetBrowserParameter.Id);
            List<string> values;
            if (!_sheetBrowserParameterValuesById.TryGetValue(key, out values) || values == null)
            {
                return;
            }

            for (int i = 0; i < values.Count; i++)
            {
                SheetBrowserParameterValues.Add(values[i]);
            }
        }

        private void FillList(List<RevitElementItem> target, IList<RevitElementItem> source)
        {
            if (target == null || source == null)
            {
                return;
            }

            for (int i = 0; i < source.Count; i++)
            {
                target.Add(source[i]);
            }
        }

        private bool TryParseDouble(string text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out value)
                   || double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out value);
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

        private bool TryParseScale(string text, out int value)
        {
            string cleanText = (text ?? string.Empty).Trim();
            if (cleanText.StartsWith("1:", StringComparison.OrdinalIgnoreCase))
            {
                cleanText = cleanText.Substring(2).Trim();
            }

            return int.TryParse(cleanText, NumberStyles.Integer, CultureInfo.CurrentCulture, out value)
                   || int.TryParse(cleanText, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private string FormatDouble(double value)
        {
            return value.ToString("0.###", CultureInfo.CurrentCulture);
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private class ValidationState
        {
            public ValidationState()
            {
                Errors = new List<string>();
                Warnings = new List<string>();
            }

            public List<string> Errors { get; private set; }

            public List<string> Warnings { get; private set; }
        }
    }

    public enum PlacementPointTarget
    {
        ViewCenter,
        ViewTitle
    }

    public class PlacementPointSelectionRequestEventArgs : EventArgs
    {
        public PlacementPointSelectionRequestEventArgs(PlacementPointTarget target, string prompt)
        {
            Target = target;
            Prompt = prompt ?? string.Empty;
        }

        public PlacementPointTarget Target { get; private set; }

        public string Prompt { get; private set; }
    }

    public class SheetBrowserParameterLevelViewModel
    {
        public SheetBrowserParameterLevelViewModel(int levelNumber, RevitElementItem parameterItem, IList<string> values)
        {
            LevelNumber = levelNumber;
            ParameterId = parameterItem != null ? parameterItem.Id : ElementId.InvalidElementId;
            ParameterName = parameterItem != null ? parameterItem.Name : string.Empty;
            Values = new ObservableCollection<string>();

            if (values != null)
            {
                for (int i = 0; i < values.Count; i++)
                {
                    Values.Add(values[i]);
                }
            }
        }

        public int LevelNumber { get; private set; }

        public ElementId ParameterId { get; private set; }

        public string ParameterName { get; private set; }

        public string LevelTitle
        {
            get { return "Уровень " + LevelNumber; }
        }

        public ObservableCollection<string> Values { get; private set; }
    }

    public class PlanKindOptionViewModel
    {
        public PlanKindOptionViewModel(SheetPlanKind kind, string displayName)
        {
            Kind = kind;
            DisplayName = displayName ?? string.Empty;
        }

        public SheetPlanKind Kind { get; private set; }

        public string DisplayName { get; private set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}

