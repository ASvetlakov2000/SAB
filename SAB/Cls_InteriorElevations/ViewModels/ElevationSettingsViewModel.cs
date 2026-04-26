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

    public class ElevationSettingsViewModel : INotifyPropertyChanged
    {
        private readonly Document _document;

        private RevitElementOption _selectedElevationViewFamilyType;
        private RevitElementOption _selectedViewTemplate;
        private RevitElementOption _selectedTitleBlockType;
        private RevitElementOption _selectedPlanCornerMarkType;
        private RevitElementOption _selectedSheetCornerMarkType;
        private bool _createSheet;

        public ElevationSettingsViewModel(Document document, ElevationSettings initialSettings = null)
        {
            _document = document;

            ElevationViewFamilyTypes = new ObservableCollection<RevitElementOption>();
            ViewTemplates = new ObservableCollection<RevitElementOption>();
            TitleBlockTypes = new ObservableCollection<RevitElementOption>();
            PlanCornerMarkTypes = new ObservableCollection<RevitElementOption>();
            SheetCornerMarkTypes = new ObservableCollection<RevitElementOption>();

            // Ð‘Ð»Ð¾Ðº Ð·Ð½Ð°Ñ‡ÐµÐ½Ð¸Ð¹ Ð¿Ð¾ ÑƒÐ¼Ð¾Ð»Ñ‡Ð°Ð½Ð¸ÑŽ, ÐºÐ¾Ñ‚Ð¾Ñ€Ñ‹Ðµ Ð¿Ð¾Ð»ÑŒÐ·Ð¾Ð²Ð°Ñ‚ÐµÐ»ÑŒ Ð¼Ð¾Ð¶ÐµÑ‚ Ð¼ÐµÐ½ÑÑ‚ÑŒ Ð² Ð¾ÐºÐ½Ðµ.
            ViewScaleText = "50";
            TopOffsetMmText = "3000";
            BottomOffsetMmText = "0";
            LeftOffsetMmText = "100";
            RightOffsetMmText = "100";
            ViewDepthMmText = "3000";
            MarkerOffsetMmText = "250";

            ColumnsCountText = "2";
            StartXmmText = "150";
            StartYmmText = "200";
            StepXmmText = "180";
            StepYmmText = "140";
            SheetFormatAText = "3";

            LoadElevationViewFamilyTypes();
            LoadViewTemplates();
            LoadTitleBlockTypes();
            LoadCornerMarkTypes(PlanCornerMarkTypes, CornerMarkConstants.PlanFamilyName);
            LoadCornerMarkTypes(SheetCornerMarkTypes, CornerMarkConstants.SheetFamilyName);

            _createSheet = TitleBlockTypes.Count > 0;

            // Ð‘Ð»Ð¾Ðº Ð²Ð¾ÑÑÑ‚Ð°Ð½Ð¾Ð²Ð»ÐµÐ½Ð¸Ñ Ð¿Ð¾ÑÐ»ÐµÐ´Ð½Ð¸Ñ… ÑÐ¾Ñ…Ñ€Ð°Ð½ÐµÐ½Ð½Ñ‹Ñ… Ð·Ð½Ð°Ñ‡ÐµÐ½Ð¸Ð¹ Ð¸Ð· Ð¿Ñ€ÐµÐ´Ñ‹Ð´ÑƒÑ‰ÐµÐ¹ ÑÐµÑÑÐ¸Ð¸.
            ApplyInitialSettings(initialSettings);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<RevitElementOption> ElevationViewFamilyTypes { get; private set; }

        public ObservableCollection<RevitElementOption> ViewTemplates { get; private set; }

        public ObservableCollection<RevitElementOption> TitleBlockTypes { get; private set; }

        public ObservableCollection<RevitElementOption> PlanCornerMarkTypes { get; private set; }

        public ObservableCollection<RevitElementOption> SheetCornerMarkTypes { get; private set; }

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

        public string ViewScaleText { get; set; }

        public string TopOffsetMmText { get; set; }

        public string BottomOffsetMmText { get; set; }

        public string LeftOffsetMmText { get; set; }

        public string RightOffsetMmText { get; set; }

        public string ViewDepthMmText { get; set; }

        public string MarkerOffsetMmText { get; set; }

        public bool CreateSheet
        {
            get { return _createSheet; }
            set
            {
                _createSheet = value;
                OnPropertyChanged("CreateSheet");
            }
        }

        public string ColumnsCountText { get; set; }

        public string StartXmmText { get; set; }

        public string StartYmmText { get; set; }

        public string StepXmmText { get; set; }

        public string StepYmmText { get; set; }

        public string SheetFormatAText { get; set; }

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

            elevationSettings.CreateSheet = CreateSheet;
            elevationSettings.TitleBlockTypeId = CreateSheet && SelectedTitleBlockType != null
                ? SelectedTitleBlockType.Id
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

            settings = elevationSettings;
            return true;
        }

        private string ValidateInput()
        {
            if (SelectedElevationViewFamilyType == null)
            {
                return "ÐÐµ Ð²Ñ‹Ð±Ñ€Ð°Ð½ Ñ‚Ð¸Ð¿ Ð²Ð¸Ð´Ð° Ñ€Ð°Ð·Ð²ÐµÑ€Ñ‚ÐºÐ¸.";
            }

            if (SelectedPlanCornerMarkType == null)
            {
                return "ÐÐµ Ð²Ñ‹Ð±Ñ€Ð°Ð½ Ñ‚Ð¸Ð¿ ÑÐµÐ¼ÐµÐ¹ÑÑ‚Ð²Ð° Ð¼Ð°Ñ€ÐºÐ¸ ÑƒÐ³Ð»Ð° Ð½Ð° Ð¿Ð»Ð°Ð½Ðµ.";
            }

            int viewScale;
            if (!TryParseInt(ViewScaleText, out viewScale) || viewScale <= 0)
            {
                return "ÐœÐ°ÑÑˆÑ‚Ð°Ð± Ð²Ð¸Ð´Ð° Ð´Ð¾Ð»Ð¶ÐµÐ½ Ð±Ñ‹Ñ‚ÑŒ Ð¿Ð¾Ð»Ð¾Ð¶Ð¸Ñ‚ÐµÐ»ÑŒÐ½Ñ‹Ð¼ Ñ†ÐµÐ»Ñ‹Ð¼ Ñ‡Ð¸ÑÐ»Ð¾Ð¼.";
            }

            double top;
            if (!TryParseDouble(TopOffsetMmText, out top) || top < 0)
            {
                return "Ð’ÐµÑ€Ñ…Ð½Ð¸Ð¹ Ð¾Ñ‚ÑÑ‚ÑƒÐ¿ Ð´Ð¾Ð»Ð¶ÐµÐ½ Ð±Ñ‹Ñ‚ÑŒ Ð½ÐµÐ¾Ñ‚Ñ€Ð¸Ñ†Ð°Ñ‚ÐµÐ»ÑŒÐ½Ñ‹Ð¼ Ñ‡Ð¸ÑÐ»Ð¾Ð¼ (Ð¼Ð¼).";
            }

            double bottom;
            if (!TryParseDouble(BottomOffsetMmText, out bottom) || bottom < 0)
            {
                return "ÐÐ¸Ð¶Ð½Ð¸Ð¹ Ð¾Ñ‚ÑÑ‚ÑƒÐ¿ Ð´Ð¾Ð»Ð¶ÐµÐ½ Ð±Ñ‹Ñ‚ÑŒ Ð½ÐµÐ¾Ñ‚Ñ€Ð¸Ñ†Ð°Ñ‚ÐµÐ»ÑŒÐ½Ñ‹Ð¼ Ñ‡Ð¸ÑÐ»Ð¾Ð¼ (Ð¼Ð¼).";
            }

            double left;
            if (!TryParseDouble(LeftOffsetMmText, out left) || left < 0)
            {
                return "Ð›ÐµÐ²Ñ‹Ð¹ Ð¾Ñ‚ÑÑ‚ÑƒÐ¿ Ð´Ð¾Ð»Ð¶ÐµÐ½ Ð±Ñ‹Ñ‚ÑŒ Ð½ÐµÐ¾Ñ‚Ñ€Ð¸Ñ†Ð°Ñ‚ÐµÐ»ÑŒÐ½Ñ‹Ð¼ Ñ‡Ð¸ÑÐ»Ð¾Ð¼ (Ð¼Ð¼).";
            }

            double right;
            if (!TryParseDouble(RightOffsetMmText, out right) || right < 0)
            {
                return "ÐŸÑ€Ð°Ð²Ñ‹Ð¹ Ð¾Ñ‚ÑÑ‚ÑƒÐ¿ Ð´Ð¾Ð»Ð¶ÐµÐ½ Ð±Ñ‹Ñ‚ÑŒ Ð½ÐµÐ¾Ñ‚Ñ€Ð¸Ñ†Ð°Ñ‚ÐµÐ»ÑŒÐ½Ñ‹Ð¼ Ñ‡Ð¸ÑÐ»Ð¾Ð¼ (Ð¼Ð¼).";
            }

            double depth;
            if (!TryParseDouble(ViewDepthMmText, out depth) || depth <= 0)
            {
                return "Глубина проекции должна быть положительным числом (мм).";
            }

            double markerOffset;
            if (!TryParseDouble(MarkerOffsetMmText, out markerOffset) || markerOffset < 0)
            {
                return "ÐžÑ‚ÑÑ‚ÑƒÐ¿ Ð²Ð¸Ð´Ð° Ð¾Ñ‚ Ð»Ð¸Ð½Ð¸Ð¸ Ð´Ð¾Ð»Ð¶ÐµÐ½ Ð±Ñ‹Ñ‚ÑŒ Ð½ÐµÐ¾Ñ‚Ñ€Ð¸Ñ†Ð°Ñ‚ÐµÐ»ÑŒÐ½Ñ‹Ð¼ Ñ‡Ð¸ÑÐ»Ð¾Ð¼ (Ð¼Ð¼).";
            }

            if (CreateSheet)
            {
                if (SelectedTitleBlockType == null)
                {
                    return "Ð’ÐºÐ»ÑŽÑ‡ÐµÐ½Ð¾ ÑÐ¾Ð·Ð´Ð°Ð½Ð¸Ðµ Ð»Ð¸ÑÑ‚Ð°, Ð½Ð¾ Ð½Ðµ Ð²Ñ‹Ð±Ñ€Ð°Ð½ Ñ‚Ð¸Ð¿ Ð¾ÑÐ½Ð¾Ð²Ð½Ð¾Ð¹ Ð½Ð°Ð´Ð¿Ð¸ÑÐ¸.";
                }

                if (SelectedSheetCornerMarkType == null)
                {
                    return "Ð’ÐºÐ»ÑŽÑ‡ÐµÐ½Ð¾ ÑÐ¾Ð·Ð´Ð°Ð½Ð¸Ðµ Ð»Ð¸ÑÑ‚Ð°, Ð½Ð¾ Ð½Ðµ Ð²Ñ‹Ð±Ñ€Ð°Ð½ Ñ‚Ð¸Ð¿ ÑÐµÐ¼ÐµÐ¹ÑÑ‚Ð²Ð° Ð¼Ð°Ñ€ÐºÐ¸ ÑƒÐ³Ð»Ð° Ð½Ð° Ð»Ð¸ÑÑ‚Ðµ.";
                }

                int columns;
                if (!TryParseInt(ColumnsCountText, out columns) || columns <= 0)
                {
                    return "ÐšÐ¾Ð»Ð¸Ñ‡ÐµÑÑ‚Ð²Ð¾ ÐºÐ¾Ð»Ð¾Ð½Ð¾Ðº Ð´Ð¾Ð»Ð¶Ð½Ð¾ Ð±Ñ‹Ñ‚ÑŒ Ð¿Ð¾Ð»Ð¾Ð¶Ð¸Ñ‚ÐµÐ»ÑŒÐ½Ñ‹Ð¼ Ñ†ÐµÐ»Ñ‹Ð¼ Ñ‡Ð¸ÑÐ»Ð¾Ð¼.";
                }

                double startX;
                if (!TryParseDouble(StartXmmText, out startX))
                {
                    return "ÐÐ°Ñ‡Ð°Ð»ÑŒÐ½Ð°Ñ ÐºÐ¾Ð¾Ñ€Ð´Ð¸Ð½Ð°Ñ‚Ð° X Ð½Ð° Ð»Ð¸ÑÑ‚Ðµ Ð´Ð¾Ð»Ð¶Ð½Ð° Ð±Ñ‹Ñ‚ÑŒ Ñ‡Ð¸ÑÐ»Ð¾Ð¼ (Ð¼Ð¼).";
                }

                double startY;
                if (!TryParseDouble(StartYmmText, out startY))
                {
                    return "ÐÐ°Ñ‡Ð°Ð»ÑŒÐ½Ð°Ñ ÐºÐ¾Ð¾Ñ€Ð´Ð¸Ð½Ð°Ñ‚Ð° Y Ð½Ð° Ð»Ð¸ÑÑ‚Ðµ Ð´Ð¾Ð»Ð¶Ð½Ð° Ð±Ñ‹Ñ‚ÑŒ Ñ‡Ð¸ÑÐ»Ð¾Ð¼ (Ð¼Ð¼).";
                }

                double stepX;
                if (!TryParseDouble(StepXmmText, out stepX) || stepX <= 0)
                {
                    return "Ð¨Ð°Ð³ Ð¿Ð¾ X Ð´Ð¾Ð»Ð¶ÐµÐ½ Ð±Ñ‹Ñ‚ÑŒ Ð¿Ð¾Ð»Ð¾Ð¶Ð¸Ñ‚ÐµÐ»ÑŒÐ½Ñ‹Ð¼ Ñ‡Ð¸ÑÐ»Ð¾Ð¼ (Ð¼Ð¼).";
                }

                double stepY;
                if (!TryParseDouble(StepYmmText, out stepY) || stepY <= 0)
                {
                    return "Ð¨Ð°Ð³ Ð¿Ð¾ Y Ð´Ð¾Ð»Ð¶ÐµÐ½ Ð±Ñ‹Ñ‚ÑŒ Ð¿Ð¾Ð»Ð¾Ð¶Ð¸Ñ‚ÐµÐ»ÑŒÐ½Ñ‹Ð¼ Ñ‡Ð¸ÑÐ»Ð¾Ð¼ (Ð¼Ð¼).";
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

                if (view.ViewType != ViewType.Elevation)
                {
                    continue;
                }

                RevitElementOption option = new RevitElementOption();
                option.Id = view.Id;
                option.DisplayName = view.Name;
                options.Add(option);
            }

            // Ð•ÑÐ»Ð¸ ÑˆÐ°Ð±Ð»Ð¾Ð½Ð¾Ð² Ñ€Ð°Ð·Ð²ÐµÑ€Ñ‚Ð¾Ðº Ð½ÐµÑ‚, Ð´Ð°ÐµÐ¼ Ð²Ñ‹Ð±Ñ€Ð°Ñ‚ÑŒ Ð»ÑŽÐ±Ð¾Ð¹ ÑˆÐ°Ð±Ð»Ð¾Ð½ Ð²Ð¸Ð´Ð°.
            if (options.Count == 0)
            {
                FilteredElementCollector fallbackCollector = new FilteredElementCollector(_document).OfClass(typeof(View));
                foreach (Element element in fallbackCollector)
                {
                    View view = element as View;
                    if (view == null || !view.IsTemplate)
                    {
                        continue;
                    }

                    RevitElementOption option = new RevitElementOption();
                    option.Id = view.Id;
                    option.DisplayName = view.Name;
                    options.Add(option);
                }
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

        private void LoadCornerMarkTypes(ObservableCollection<RevitElementOption> targetCollection, string requiredFamilyName)
        {
            List<RevitElementOption> options = new List<RevitElementOption>();

            FilteredElementCollector collector = new FilteredElementCollector(_document).OfClass(typeof(FamilySymbol));
            foreach (Element element in collector)
            {
                FamilySymbol familySymbol = element as FamilySymbol;
                if (familySymbol == null)
                {
                    continue;
                }

                string familyName = familySymbol.Family != null ? familySymbol.Family.Name : familySymbol.FamilyName;
                if (!string.Equals(familyName, requiredFamilyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

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

            // Ð’Ð¾ÑÑÑ‚Ð°Ð½Ð°Ð²Ð»Ð¸Ð²Ð°ÐµÐ¼ Ñ‚Ð¸Ð¿ Ð²Ð¸Ð´Ð° Ñ€Ð°Ð·Ð²ÐµÑ€Ñ‚ÐºÐ¸ Ñ‚Ð¾Ð»ÑŒÐºÐ¾ ÐµÑÐ»Ð¸ Ñ‚Ð¸Ð¿ Ð´Ð¾ÑÑ‚ÑƒÐ¿ÐµÐ½ Ð² Ñ‚ÐµÐºÑƒÑ‰ÐµÐ¼ Ð´Ð¾ÐºÑƒÐ¼ÐµÐ½Ñ‚Ðµ.
            RevitElementOption elevationTypeOption = FindOptionById(ElevationViewFamilyTypes, initialSettings.ElevationViewFamilyTypeId);
            if (elevationTypeOption != null)
            {
                SelectedElevationViewFamilyType = elevationTypeOption;
            }

            // Ð•ÑÐ»Ð¸ Ñ€Ð°Ð½ÐµÐµ ÑˆÐ°Ð±Ð»Ð¾Ð½ Ð½Ðµ Ð²Ñ‹Ð±Ð¸Ñ€Ð°Ð»Ð¸, Ð¾ÑÑ‚Ð°Ð²Ð»ÑÐµÐ¼ Ð²Ð°Ñ€Ð¸Ð°Ð½Ñ‚ "<ÐÐµ Ð²Ñ‹Ð±Ñ€Ð°Ð½>".
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

            if (initialSettings.SheetFormatAValue.HasValue)
            {
                SheetFormatAText = initialSettings.SheetFormatAValue.Value.ToString(CultureInfo.CurrentCulture);
            }

            // Ð’ÐºÐ»ÑŽÑ‡Ð°ÐµÐ¼ ÑÐ¾Ð·Ð´Ð°Ð½Ð¸Ðµ Ð»Ð¸ÑÑ‚Ð° Ñ‚Ð¾Ð»ÑŒÐºÐ¾ ÐµÑÐ»Ð¸ Ð² Ð¿Ñ€Ð¾ÐµÐºÑ‚Ðµ ÐµÑÑ‚ÑŒ Ð´Ð¾ÑÑ‚ÑƒÐ¿Ð½Ñ‹Ðµ Ñ‚Ð¸Ð¿Ñ‹ Ð¾ÑÐ½Ð¾Ð²Ð½Ð¾Ð¹ Ð½Ð°Ð´Ð¿Ð¸ÑÐ¸.
            CreateSheet = initialSettings.CreateSheet && TitleBlockTypes.Count > 0;
            RevitElementOption titleBlockOption = FindOptionById(TitleBlockTypes, initialSettings.TitleBlockTypeId);
            if (titleBlockOption != null)
            {
                SelectedTitleBlockType = titleBlockOption;
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
            if (savedLayout == null)
            {
                return;
            }

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

