using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
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

    public class ElevationSettingsViewModel : INotifyPropertyChanged
    {
        private readonly Document _document;

        private RevitElementOption _selectedElevationViewFamilyType;
        private RevitElementOption _selectedViewTemplate;
        private RevitElementOption _selectedTitleBlockType;
        private RevitElementOption _selectedViewportType;
        private RevitElementOption _selectedPlanCornerMarkType;
        private RevitElementOption _selectedSheetCornerMarkType;
        private RevitElementOption _selectedRoomPlanViewTemplate;
        private RevitElementOption _selectedRoomPlanRoomTagType;
        private ViewTitleAnchorOption _selectedViewTitleAnchor;
        private bool _createSheet;
        private bool _multipleRoomsOnSheet;
        private bool _isCropManualMode;
        private bool _isSheetPointManualMode;
        private string _startXmmText;
        private string _startYmmText;

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
            ViewTitleAnchors = new ObservableCollection<ViewTitleAnchorOption>();

            // Блок значений по умолчанию, которые пользователь может менять в окне.
            ViewScaleText = "50";
            TopOffsetMmText = "3000";
            BottomOffsetMmText = "0";
            LeftOffsetMmText = "100";
            RightOffsetMmText = "100";
            _isCropManualMode = true;
            ViewDepthMmText = "3000";
            MarkerOffsetMmText = "250";
            ElevationNamePart1Text = "ELV_r";
            ElevationNamePart2Text = "{Номер помещения}_{Имя помещения}";
            ElevationNamePart3Text = "_Elev_{Начальный угол}-{Конечный угол}";

            ColumnsCountText = "2";
            _isSheetPointManualMode = true;
            StartXmmText = "150";
            StartYmmText = "200";
            StepXmmText = "180";
            StepYmmText = "140";
            SheetFormatAText = "3";
            SheetNamePart1Text = "Развертки стен пом. ";
            SheetNamePart2Text = "{Помещения}";
            SheetNamePart3Text = string.Empty;
            ViewTitleOffsetXmmText = "0";
            ViewTitleOffsetYmmText = "-5";

            // Блок настроек план-схемы, интегрированный в окно разверток.
            RoomPlanNamePart1Text = "План-схема разверток пом. ";
            RoomPlanNamePart2Text = "{Номер помещения}";
            RoomPlanNamePart3Text = string.Empty;
            RoomPlanViewScaleText = "20";
            RoomPlanCropOffsetMmText = "0";

            LoadElevationViewFamilyTypes();
            LoadViewTemplates();
            LoadTitleBlockTypes();
            LoadViewportTypes();
            LoadCornerMarkTypes(PlanCornerMarkTypes);
            LoadCornerMarkTypes(SheetCornerMarkTypes);
            LoadRoomPlanViewTemplates();
            LoadRoomPlanRoomTagTypes();
            LoadViewTitleAnchors();

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

        public ObservableCollection<ViewTitleAnchorOption> ViewTitleAnchors { get; private set; }

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

        public bool IsCropManualMode
        {
            get { return _isCropManualMode; }
            set
            {
                if (_isCropManualMode == value)
                {
                    return;
                }

                _isCropManualMode = value;
                OnPropertyChanged("IsCropManualMode");
                OnPropertyChanged("IsCropByExampleMode");
            }
        }

        public bool IsCropByExampleMode
        {
            get { return !IsCropManualMode; }
            set
            {
                if (value)
                {
                    IsCropManualMode = false;
                }
            }
        }

        public string ViewDepthMmText { get; set; }

        public string MarkerOffsetMmText { get; set; }

        public string ElevationNamePart1Text { get; set; }

        public string ElevationNamePart2Text { get; set; }

        public string ElevationNamePart3Text { get; set; }

        public bool CreateSheet
        {
            get { return _createSheet; }
            set
            {
                _createSheet = value;
                OnPropertyChanged("CreateSheet");
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

        public bool IsSheetPointManualMode
        {
            get { return _isSheetPointManualMode; }
            set
            {
                if (_isSheetPointManualMode == value)
                {
                    return;
                }

                _isSheetPointManualMode = value;
                OnPropertyChanged("IsSheetPointManualMode");
                OnPropertyChanged("IsSheetPointPointMode");
            }
        }

        public bool IsSheetPointPointMode
        {
            get { return !IsSheetPointManualMode; }
            set
            {
                if (value)
                {
                    IsSheetPointManualMode = false;
                }
            }
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

        public string SheetFormatAText { get; set; }

        public string SheetNamePart1Text { get; set; }

        public string SheetNamePart2Text { get; set; }

        public string SheetNamePart3Text { get; set; }

        public string ViewTitleOffsetXmmText { get; set; }

        public string ViewTitleOffsetYmmText { get; set; }

        public string RoomPlanNamePart1Text { get; set; }

        public string RoomPlanNamePart2Text { get; set; }

        public string RoomPlanNamePart3Text { get; set; }

        public string RoomPlanViewScaleText { get; set; }

        public string RoomPlanCropOffsetMmText { get; set; }

        public bool TryBuildSettings(out ElevationSettings settings, out string validationMessage)
        {
            settings = null;
            validationMessage = ValidateInput();
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
            elevationSettings.ElevationNamePart1 = ElevationNamePart1Text ?? string.Empty;
            elevationSettings.ElevationNamePart2 = ElevationNamePart2Text ?? string.Empty;
            elevationSettings.ElevationNamePart3 = ElevationNamePart3Text ?? string.Empty;

            elevationSettings.CreateSheet = CreateSheet;
            elevationSettings.MultipleRoomsOnSheet = IsMultipleRoomsOnSheet;
            elevationSettings.TitleBlockTypeId = CreateSheet && SelectedTitleBlockType != null
                ? SelectedTitleBlockType.Id
                : ElementId.InvalidElementId;
            elevationSettings.ViewportTypeId = CreateSheet && SelectedViewportType != null
                ? SelectedViewportType.Id
                : ElementId.InvalidElementId;

            elevationSettings.PlanCornerMarkTypeId = SelectedPlanCornerMarkType != null
                ? SelectedPlanCornerMarkType.Id
                : ElementId.InvalidElementId;

            elevationSettings.SheetCornerMarkTypeId = CreateSheet && SelectedSheetCornerMarkType != null
                ? SelectedSheetCornerMarkType.Id
                : ElementId.InvalidElementId;
            elevationSettings.SheetFormatAValue = ParseNullableInt(SheetFormatAText);

            elevationSettings.SheetLayoutSettings = new SheetLayoutSettings();
            elevationSettings.SheetLayoutSettings.ColumnsCount = ParseInt(ColumnsCountText);
            elevationSettings.SheetLayoutSettings.StartXmm = ParseDouble(StartXmmText);
            elevationSettings.SheetLayoutSettings.StartYmm = ParseDouble(StartYmmText);
            elevationSettings.SheetLayoutSettings.StepXmm = ParseDouble(StepXmmText);
            elevationSettings.SheetLayoutSettings.StepYmm = ParseDouble(StepYmmText);
            elevationSettings.SheetLayoutSettings.ViewTitleAnchor = SelectedViewTitleAnchor != null
                ? SelectedViewTitleAnchor.Value
                : ViewTitleAnchor.BottomLeft;
            elevationSettings.SheetLayoutSettings.ViewTitleOffsetXmm = ParseDouble(ViewTitleOffsetXmmText);
            elevationSettings.SheetLayoutSettings.ViewTitleOffsetYmm = ParseDouble(ViewTitleOffsetYmmText);

            elevationSettings.SheetNamePart1 = SheetNamePart1Text ?? string.Empty;
            elevationSettings.SheetNamePart2 = SheetNamePart2Text ?? string.Empty;
            elevationSettings.SheetNamePart3 = SheetNamePart3Text ?? string.Empty;

            // Блок параметров план-схемы помещения.
            elevationSettings.RoomPlanNamePart1 = RoomPlanNamePart1Text ?? string.Empty;
            elevationSettings.RoomPlanNamePart2 = RoomPlanNamePart2Text ?? string.Empty;
            elevationSettings.RoomPlanNamePart3 = RoomPlanNamePart3Text ?? string.Empty;
            elevationSettings.RoomPlanViewTemplateId = SelectedRoomPlanViewTemplate != null
                ? SelectedRoomPlanViewTemplate.Id
                : ElementId.InvalidElementId;
            elevationSettings.RoomPlanRoomTagTypeId = SelectedRoomPlanRoomTagType != null
                ? SelectedRoomPlanRoomTagType.Id
                : ElementId.InvalidElementId;
            elevationSettings.RoomPlanViewScale = ParseInt(RoomPlanViewScaleText);
            elevationSettings.RoomPlanCropOffsetMm = ParseDouble(RoomPlanCropOffsetMmText);

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
            IsSheetPointManualMode = false;
        }

        public void SetCropHeightOffsetsFromExample(double topOffsetMm, double bottomOffsetMm)
        {
            // Блок записи значений из вида-примера в те же поля, которые использует основная команда.
            TopOffsetMmText = FormatDouble(Math.Max(0.0, topOffsetMm));
            BottomOffsetMmText = FormatDouble(Math.Max(0.0, bottomOffsetMm));
            IsCropManualMode = true;
            OnPropertyChanged("TopOffsetMmText");
            OnPropertyChanged("BottomOffsetMmText");
        }

        private string ValidateInput()
        {
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

            if (string.IsNullOrWhiteSpace(ElevationNamePart1Text) &&
                string.IsNullOrWhiteSpace(ElevationNamePart2Text) &&
                string.IsNullOrWhiteSpace(ElevationNamePart3Text))
            {
                return "Формула имени развертки не может быть пустой.";
            }

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

            if (CreateSheet)
            {
                if (SelectedTitleBlockType == null)
                {
                    return "Включено создание листа, но не выбран тип основной надписи.";
                }

                if (SelectedViewTitleAnchor == null)
                {
                    return "Не выбрана привязка размещения заголовка развертки.";
                }

                if (string.IsNullOrWhiteSpace(SheetNamePart1Text) &&
                    string.IsNullOrWhiteSpace(SheetNamePart2Text) &&
                    string.IsNullOrWhiteSpace(SheetNamePart3Text))
                {
                    return "Формула имени листа не может быть пустой.";
                }

                if (SelectedSheetCornerMarkType == null)
                {
                    return "Включено создание листа, но не выбран тип семейства марки угла на листе.";
                }

                int columns;
                if (!TryParseInt(ColumnsCountText, out columns) || columns <= 0)
                {
                    return "Количество колонок должно быть положительным целым числом.";
                }

                double startX;
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
                Value = ViewTitleAnchor.TopCenter,
                DisplayName = "По центру над видом"
            });

            SelectedViewTitleAnchor = ViewTitleAnchors[0];
        }

        private void LoadCornerMarkTypes(ObservableCollection<RevitElementOption> targetCollection)
        {
            List<RevitElementOption> options = new List<RevitElementOption>();

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
                RevitElementOption option = new RevitElementOption();
                option.Id = familySymbol.Id;
                option.DisplayName = familyName + " : " + familySymbol.Name;
                options.Add(option);
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

            ElevationNamePart1Text = initialSettings.ElevationNamePart1 ?? string.Empty;
            ElevationNamePart2Text = initialSettings.ElevationNamePart2 ?? string.Empty;
            ElevationNamePart3Text = initialSettings.ElevationNamePart3 ?? string.Empty;

            if (initialSettings.SheetFormatAValue.HasValue)
            {
                SheetFormatAText = initialSettings.SheetFormatAValue.Value.ToString(CultureInfo.CurrentCulture);
            }

            // Включаем создание листа только если в проекте есть доступные типы основной надписи.
            CreateSheet = initialSettings.CreateSheet && TitleBlockTypes.Count > 0 && ViewportTypes.Count > 0;
            IsMultipleRoomsOnSheet = initialSettings.MultipleRoomsOnSheet;
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

            SheetNamePart1Text = initialSettings.SheetNamePart1 ?? string.Empty;
            SheetNamePart2Text = initialSettings.SheetNamePart2 ?? string.Empty;
            SheetNamePart3Text = initialSettings.SheetNamePart3 ?? string.Empty;

            // Блок восстановления настроек план-схемы.
            if (!string.IsNullOrWhiteSpace(initialSettings.RoomPlanNamePart1))
            {
                RoomPlanNamePart1Text = initialSettings.RoomPlanNamePart1;
            }

            if (!string.IsNullOrWhiteSpace(initialSettings.RoomPlanNamePart2))
            {
                RoomPlanNamePart2Text = initialSettings.RoomPlanNamePart2;
            }

            RoomPlanNamePart3Text = initialSettings.RoomPlanNamePart3 ?? string.Empty;
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
