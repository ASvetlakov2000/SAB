using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;

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
        private bool _createSheet;
        private string _viewNamePrefix;
        private bool _useRoomNumberInViewName;
        private bool _useRoomNameInViewName;

        public ElevationSettingsViewModel(Document document)
        {
            _document = document;

            ElevationViewFamilyTypes = new ObservableCollection<RevitElementOption>();
            ViewTemplates = new ObservableCollection<RevitElementOption>();
            TitleBlockTypes = new ObservableCollection<RevitElementOption>();

            // Block with default user-editable parameters for MVP.
            ViewScaleText = "50";
            TopOffsetMmText = "3000";
            BottomOffsetMmText = "0";
            LeftOffsetMmText = "100";
            RightOffsetMmText = "100";
            ViewDepthMmText = "3000";
            MarkerOffsetMmText = "250";

            _viewNamePrefix = string.Empty;
            _useRoomNumberInViewName = true;
            _useRoomNameInViewName = true;

            ColumnsCountText = "2";
            StartXmmText = "150";
            StartYmmText = "200";
            StepXmmText = "180";
            StepYmmText = "140";

            LoadElevationViewFamilyTypes();
            LoadViewTemplates();
            LoadTitleBlockTypes();

            _createSheet = TitleBlockTypes.Count > 0;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<RevitElementOption> ElevationViewFamilyTypes { get; private set; }

        public ObservableCollection<RevitElementOption> ViewTemplates { get; private set; }

        public ObservableCollection<RevitElementOption> TitleBlockTypes { get; private set; }

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

        public string ViewNamePrefix
        {
            get { return _viewNamePrefix; }
            set
            {
                _viewNamePrefix = value;
                OnPropertyChanged("ViewNamePrefix");
            }
        }

        public bool UseRoomNumberInViewName
        {
            get { return _useRoomNumberInViewName; }
            set
            {
                _useRoomNumberInViewName = value;
                OnPropertyChanged("UseRoomNumberInViewName");
            }
        }

        public bool UseRoomNameInViewName
        {
            get { return _useRoomNameInViewName; }
            set
            {
                _useRoomNameInViewName = value;
                OnPropertyChanged("UseRoomNameInViewName");
            }
        }

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

            elevationSettings.SheetLayoutSettings = new SheetLayoutSettings();
            elevationSettings.SheetLayoutSettings.ColumnsCount = ParseInt(ColumnsCountText);
            elevationSettings.SheetLayoutSettings.StartXmm = ParseDouble(StartXmmText);
            elevationSettings.SheetLayoutSettings.StartYmm = ParseDouble(StartYmmText);
            elevationSettings.SheetLayoutSettings.StepXmm = ParseDouble(StepXmmText);
            elevationSettings.SheetLayoutSettings.StepYmm = ParseDouble(StepYmmText);

            elevationSettings.ViewNamePrefix = ViewNamePrefix;
            elevationSettings.UseRoomNumberInViewName = UseRoomNumberInViewName;
            elevationSettings.UseRoomNameInViewName = UseRoomNameInViewName;

            settings = elevationSettings;
            return true;
        }

        private string ValidateInput()
        {
            if (SelectedElevationViewFamilyType == null)
            {
                return "Elevation ViewFamilyType is not selected.";
            }

            int viewScale;
            if (!TryParseInt(ViewScaleText, out viewScale) || viewScale <= 0)
            {
                return "View scale must be a positive integer.";
            }

            double top;
            if (!TryParseDouble(TopOffsetMmText, out top) || top < 0)
            {
                return "Top offset must be a non-negative number (mm).";
            }

            double bottom;
            if (!TryParseDouble(BottomOffsetMmText, out bottom) || bottom < 0)
            {
                return "Bottom offset must be a non-negative number (mm).";
            }

            double left;
            if (!TryParseDouble(LeftOffsetMmText, out left) || left < 0)
            {
                return "Left offset must be a non-negative number (mm).";
            }

            double right;
            if (!TryParseDouble(RightOffsetMmText, out right) || right < 0)
            {
                return "Right offset must be a non-negative number (mm).";
            }

            double depth;
            if (!TryParseDouble(ViewDepthMmText, out depth) || depth <= 0)
            {
                return "View depth must be a positive number (mm).";
            }

            double markerOffset;
            if (!TryParseDouble(MarkerOffsetMmText, out markerOffset) || markerOffset <= 0)
            {
                return "Marker offset must be a positive number (mm).";
            }

            if (CreateSheet)
            {
                if (SelectedTitleBlockType == null)
                {
                    return "Create sheet is enabled, but title block type is not selected.";
                }

                int columns;
                if (!TryParseInt(ColumnsCountText, out columns) || columns <= 0)
                {
                    return "Columns count must be a positive integer.";
                }

                double startX;
                if (!TryParseDouble(StartXmmText, out startX))
                {
                    return "Sheet start X must be a valid number (mm).";
                }

                double startY;
                if (!TryParseDouble(StartYmmText, out startY))
                {
                    return "Sheet start Y must be a valid number (mm).";
                }

                double stepX;
                if (!TryParseDouble(StepXmmText, out stepX) || stepX <= 0)
                {
                    return "Sheet step X must be a positive number (mm).";
                }

                double stepY;
                if (!TryParseDouble(StepYmmText, out stepY) || stepY <= 0)
                {
                    return "Sheet step Y must be a positive number (mm).";
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
            emptyOption.DisplayName = "<None>";
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

            // Fallback block: if no elevation templates were found, list all templates.
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

        private int ParseInt(string text)
        {
            int value;
            if (!TryParseInt(text, out value))
            {
                return 0;
            }

            return value;
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
