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
        private readonly HashSet<string> _existingViewNames;
        private readonly HashSet<string> _existingSheetNumbers;

        private RevitElementItem _selectedSourceView;
        private RevitElementItem _selectedSourceSheet;
        private RevitElementItem _selectedViewportType;
        private RevitElementItem _selectedTitleBlockType;

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
        private bool _isViewCenterManualMode;
        private bool _isViewTitleManualMode;

        public CreateViewsAndSheetsViewModel(
            IList<RevitElementItem> sourceViews,
            IList<RevitElementItem> sourceSheets,
            IList<RevitElementItem> viewportTypes,
            IList<RevitElementItem> titleBlockTypes,
            IList<RevitElementItem> viewTemplates,
            HashSet<string> existingViewNames,
            HashSet<string> existingSheetNumbers,
            CreateViewsAndSheetsSettings initialSettings)
        {
            SourceViews = new ObservableCollection<RevitElementItem>();
            SourceSheets = new ObservableCollection<RevitElementItem>();
            ViewportTypes = new ObservableCollection<RevitElementItem>();
            TitleBlockTypes = new ObservableCollection<RevitElementItem>();
            ViewTemplates = new ObservableCollection<RevitElementItem>();
            Rows = new ObservableCollection<SheetCreationRowViewModel>();
            ScaleOptions = new ObservableCollection<string>();

            _allViewTemplates = new List<RevitElementItem>();
            _existingViewNames = existingViewNames ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _existingSheetNumbers = existingSheetNumbers ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            FillCollection(SourceViews, sourceViews);
            FillCollection(SourceSheets, sourceSheets);
            FillCollection(ViewportTypes, viewportTypes);
            FillCollection(TitleBlockTypes, titleBlockTypes);
            FillList(_allViewTemplates, viewTemplates);

            ScaleOptions.Add("20");
            ScaleOptions.Add("25");
            ScaleOptions.Add("50");
            ScaleOptions.Add("75");
            ScaleOptions.Add("100");
            ScaleOptions.Add("200");

            PlacementSettings placement = initialSettings != null && initialSettings.Placement != null
                ? initialSettings.Placement
                : new PlacementSettings();

            _viewCenterXText = FormatDouble(placement.ViewCenterXmm);
            _viewCenterYText = FormatDouble(placement.ViewCenterYmm);
            _viewTitleXText = FormatDouble(placement.ViewTitleXmm);
            _viewTitleYText = FormatDouble(placement.ViewTitleYmm);
            _titleLineLengthText = FormatDouble(DefaultTitleLineLengthMm);
            _isViewCenterManualMode = !placement.UsePointSelectionForViewCenter;
            _isViewTitleManualMode = !placement.UsePointSelectionForViewTitle;
            _saveSettings = placement.SaveSettings;

            AddRowInternal(null);

            ValidateCommand = new RelayCommand(delegate { RefreshValidation(); });
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
            CreateCommand = new RelayCommand(delegate { AcceptWindow(); }, delegate { return CanCreate; });
            CancelCommand = new RelayCommand(delegate { CancelWindow(); });

            ApplyInitialSelections(initialSettings);
            RefreshValidation();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public event EventHandler RequestClose;

        public event EventHandler<PlacementPointSelectionRequestEventArgs> RequestPointSelection;

        public ObservableCollection<RevitElementItem> SourceViews { get; private set; }

        public ObservableCollection<RevitElementItem> SourceSheets { get; private set; }

        public ObservableCollection<RevitElementItem> ViewportTypes { get; private set; }

        public ObservableCollection<RevitElementItem> TitleBlockTypes { get; private set; }

        public ObservableCollection<RevitElementItem> ViewTemplates { get; private set; }

        public ObservableCollection<SheetCreationRowViewModel> Rows { get; private set; }

        public ObservableCollection<string> ScaleOptions { get; private set; }

        public ICommand ValidateCommand { get; private set; }

        public ICommand PickViewCenterPointCommand { get; private set; }

        public ICommand PickViewTitlePointCommand { get; private set; }

        public ICommand MoveRowUpCommand { get; private set; }

        public ICommand MoveRowDownCommand { get; private set; }

        public ICommand InsertRowAfterCommand { get; private set; }

        public ICommand DeleteRowCommand { get; private set; }

        public ICommand CreateCommand { get; private set; }

        public ICommand CancelCommand { get; private set; }

        public bool IsAccepted
        {
            get { return _isAccepted; }
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
            settings.SourceViewId = SelectedSourceView.Id;
            settings.SourceSheetId = SelectedSourceSheet.Id;
            settings.ViewportTypeId = SelectedViewportType.Id;
            settings.TitleBlockTypeId = SelectedTitleBlockType.Id;
            settings.SheetBounds = ResolveCurrentSheetBounds();
            settings.Placement = new PlacementSettings();
            settings.Placement.CoordinateUnits = "мм";
            settings.Placement.ViewCenterXmm = centerX;
            settings.Placement.ViewCenterYmm = centerY;
            settings.Placement.ViewTitleXmm = titleX;
            settings.Placement.ViewTitleYmm = titleY;
            settings.Placement.TitleLineLengthMm = lineLength;
            settings.Placement.UsePointSelectionForViewCenter = IsViewCenterPointMode;
            settings.Placement.UsePointSelectionForViewTitle = IsViewTitlePointMode;
            settings.Placement.SaveSettings = SaveSettings;

            items = BuildItems();
            return true;
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
            SelectedSourceView = FindById(SourceViews, initialSettings != null ? initialSettings.SourceViewId : ElementId.InvalidElementId)
                                 ?? (SourceViews.Count > 0 ? SourceViews[0] : null);

            SelectedSourceSheet = FindById(SourceSheets, initialSettings != null ? initialSettings.SourceSheetId : ElementId.InvalidElementId)
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

            if (Rows.Count > 0)
            {
                AssignDefaultTemplate(Rows[0]);
            }
        }

        private void ReloadCompatibleViewTemplates()
        {
            ViewTemplates.Clear();

            if (SelectedSourceView == null)
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

                if (template.Id == ElementId.InvalidElementId || template.ViewType == SelectedSourceView.ViewType)
                {
                    ViewTemplates.Add(template);
                }
            }

            for (int rowIndex = 0; rowIndex < Rows.Count; rowIndex++)
            {
                SheetCreationRowViewModel row = Rows[rowIndex];
                if (row == null)
                {
                    continue;
                }

                if (!IsTemplateAvailable(row.SelectedViewTemplate))
                {
                    AssignDefaultTemplate(row);
                }
            }
        }

        private void ApplyTitleBlockFromSourceSheet()
        {
            if (SelectedSourceSheet == null || SelectedSourceSheet.RelatedElementId == null ||
                SelectedSourceSheet.RelatedElementId == ElementId.InvalidElementId)
            {
                return;
            }

            RevitElementItem titleBlockType = FindById(TitleBlockTypes, SelectedSourceSheet.RelatedElementId);
            if (titleBlockType != null)
            {
                _selectedTitleBlockType = titleBlockType;
                OnPropertyChanged("SelectedTitleBlockType");
            }
        }

        private SheetBounds ResolveCurrentSheetBounds()
        {
            if (SelectedSourceSheet != null && SelectedSourceSheet.SheetBounds != null)
            {
                return SelectedSourceSheet.SheetBounds;
            }

            if (SelectedTitleBlockType != null && SelectedTitleBlockType.SheetBounds != null)
            {
                return SelectedTitleBlockType.SheetBounds;
            }

            return null;
        }

        private void AddRowAtEnd()
        {
            AddRowInternal(null);
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
                row.ViewScaleText = sourceRow.ViewScaleText;
                row.SelectedViewTemplate = sourceRow.SelectedViewTemplate;
            }

            Rows.Add(row);
            RenumberRows();
        }

        private SheetCreationRowViewModel CreateRow()
        {
            SheetCreationRowViewModel row = new SheetCreationRowViewModel();
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

            RevitElementItem firstRealTemplate = null;
            for (int i = 0; i < ViewTemplates.Count; i++)
            {
                RevitElementItem item = ViewTemplates[i];
                if (item != null && item.Id != ElementId.InvalidElementId)
                {
                    firstRealTemplate = item;
                    break;
                }
            }

            row.SelectedViewTemplate = firstRealTemplate;
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

            if (SelectedSourceView == null)
            {
                state.Errors.Add("Не выбран вид-образец.");
            }

            if (SelectedSourceSheet == null)
            {
                state.Errors.Add("Не выбран лист-образец.");
            }

            if (SelectedViewportType == null)
            {
                state.Errors.Add("Не выбран тип видового экрана.");
            }

            if (SelectedTitleBlockType == null)
            {
                state.Errors.Add("Не выбрана основная надпись.");
            }

            SheetBounds bounds = ResolveCurrentSheetBounds();
            if (bounds == null)
            {
                state.Errors.Add("Не удалось определить габарит листа по выбранной основной надписи или листу-образцу.");
            }

            ValidatePlacementValues(bounds, state);
            ValidateRows(state);
            UpdateStatusText(state);
            return state;
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
                if (row == null || !row.IsFilled)
                {
                    continue;
                }

                filledCount++;
                List<string> rowErrors = new List<string>();

                string viewName = (row.ViewName ?? string.Empty).Trim();
                string sheetNumber = (row.SheetNumber ?? string.Empty).Trim();
                string sheetName = (row.SheetName ?? string.Empty).Trim();

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
                item.ViewName = (row.ViewName ?? string.Empty).Trim();
                item.ViewScale = scale;
                item.ViewTemplateId = row.SelectedViewTemplate != null ? row.SelectedViewTemplate.Id : ElementId.InvalidElementId;
                item.SheetNumber = (row.SheetNumber ?? string.Empty).Trim();
                item.SheetName = (row.SheetName ?? string.Empty).Trim();
                result.Add(item);
            }

            return result;
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
}
