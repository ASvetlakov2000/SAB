using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace SAB.ViewTemplateGraphics.Models
{
    public abstract class NotifyPropertyChangedBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

    public class NamedElementOption
    {
        public NamedElementOption(int idValue, string name)
        {
            IdValue = idValue;
            Name = name ?? string.Empty;
        }

        public int IdValue { get; private set; }

        public string Name { get; private set; }

        public override string ToString()
        {
            return Name;
        }
    }

    public class NamedIntegerOption
    {
        public NamedIntegerOption(int value, string name)
        {
            Value = value;
            Name = name ?? string.Empty;
        }

        public int Value { get; private set; }

        public string Name { get; private set; }

        public override string ToString()
        {
            return Name;
        }
    }

    public class NamedDetailLevelOption
    {
        public NamedDetailLevelOption(ViewDetailLevel value, string name)
        {
            Value = value;
            Name = name ?? string.Empty;
        }

        public ViewDetailLevel Value { get; private set; }

        public string Name { get; private set; }

        public override string ToString()
        {
            return Name;
        }
    }

    public class NamedStringOption
    {
        public NamedStringOption(string value, string name)
        {
            Value = value ?? string.Empty;
            Name = name ?? string.Empty;
        }

        public string Value { get; private set; }

        public string Name { get; private set; }

        public override string ToString()
        {
            return Name;
        }
    }

    public enum CategoryGraphicsGroup
    {
        Model,
        Annotation,
        AnalyticalModel,
        Imported
    }

    public enum GraphicOverrideEditorSection
    {
        All,
        ProjectionLines,
        SurfacePatterns,
        Transparency,
        CutLines,
        CutPatterns
    }

    public class TemplateSectionState : NotifyPropertyChangedBase
    {
        private bool _isIncluded;
        private bool _isMixed;
        private bool _originalIsIncluded;
        private bool _originalWasMixed;
        private bool _isModified;
        private bool _isTrackingChanges;

        public TemplateSectionState(int parameterIdValue, string title)
        {
            ParameterIdValue = parameterIdValue;
            Title = title ?? string.Empty;
        }

        public int ParameterIdValue { get; private set; }

        public string Title { get; private set; }

        public bool IsIncluded
        {
            get { return _isIncluded; }
            set
            {
                if (SetField(ref _isIncluded, value, "IsIncluded") && _isTrackingChanges)
                {
                    UpdateModificationState();
                }
            }
        }

        public bool? IncludedState
        {
            get { return _isMixed ? (bool?)null : _isIncluded; }
            set
            {
                if (!value.HasValue)
                {
                    return;
                }

                bool wasMixed = _isMixed;
                _isMixed = false;
                if (wasMixed)
                {
                    OnPropertyChanged("IncludedState");
                }

                if (_isIncluded != value.Value)
                {
                    IsIncluded = value.Value;
                }
                else if (wasMixed && _isTrackingChanges)
                {
                    UpdateModificationState();
                }
            }
        }

        public bool IsMixed
        {
            get { return _isMixed; }
        }

        public bool IsModified
        {
            get { return _isModified; }
        }

        public void StartTrackingChanges()
        {
            _originalIsIncluded = _isIncluded;
            _originalWasMixed = _isMixed;
            _isModified = false;
            _isTrackingChanges = true;
            OnPropertyChanged("IsModified");
        }

        public void MergeValue(bool value)
        {
            if (_isIncluded != value)
            {
                _isMixed = true;
                OnPropertyChanged("IncludedState");
                OnPropertyChanged("IsMixed");
            }
        }

        private void UpdateModificationState()
        {
            bool isModified = _originalWasMixed
                ? !_isMixed
                : _isMixed || _isIncluded != _originalIsIncluded;
            if (_isModified != isModified)
            {
                _isModified = isModified;
                OnPropertyChanged("IsModified");
            }
        }
    }

    public class GraphicOverrideData : NotifyPropertyChangedBase
    {
        public const int NoColorValue = -1;
        public const int MixedIntegerValue = int.MinValue + 120;
        public const ViewDetailLevel MixedDetailLevelValue = (ViewDetailLevel)(-120);

        public const string ProjectionLineWeightProperty = "ProjectionLineWeight";
        public const string ProjectionLineColorValueProperty = "ProjectionLineColorValue";
        public const string ProjectionLinePatternIdProperty = "ProjectionLinePatternId";
        public const string SurfaceForegroundPatternVisibleProperty = "SurfaceForegroundPatternVisible";
        public const string SurfaceForegroundPatternIdProperty = "SurfaceForegroundPatternId";
        public const string SurfaceForegroundPatternColorValueProperty = "SurfaceForegroundPatternColorValue";
        public const string SurfaceBackgroundPatternVisibleProperty = "SurfaceBackgroundPatternVisible";
        public const string SurfaceBackgroundPatternIdProperty = "SurfaceBackgroundPatternId";
        public const string SurfaceBackgroundPatternColorValueProperty = "SurfaceBackgroundPatternColorValue";
        public const string TransparencyProperty = "Transparency";
        public const string CutLineWeightProperty = "CutLineWeight";
        public const string CutLineColorValueProperty = "CutLineColorValue";
        public const string CutLinePatternIdProperty = "CutLinePatternId";
        public const string CutForegroundPatternVisibleProperty = "CutForegroundPatternVisible";
        public const string CutForegroundPatternIdProperty = "CutForegroundPatternId";
        public const string CutForegroundPatternColorValueProperty = "CutForegroundPatternColorValue";
        public const string CutBackgroundPatternVisibleProperty = "CutBackgroundPatternVisible";
        public const string CutBackgroundPatternIdProperty = "CutBackgroundPatternId";
        public const string CutBackgroundPatternColorValueProperty = "CutBackgroundPatternColorValue";
        public const string HalftoneProperty = "Halftone";
        public const string DetailLevelProperty = "DetailLevel";

        private readonly HashSet<string> _modifiedPropertyNames;
        private readonly HashSet<string> _mixedPropertyNames;
        private readonly Dictionary<string, object> _originalPropertyValues;
        private readonly HashSet<string> _originalMixedPropertyNames;
        private bool _isTrackingChanges;

        private int _projectionLineWeight;
        private int _projectionLineColorValue;
        private int _projectionLinePatternId;
        private bool _surfaceForegroundPatternVisible;
        private int _surfaceForegroundPatternId;
        private int _surfaceForegroundPatternColorValue;
        private bool _surfaceBackgroundPatternVisible;
        private int _surfaceBackgroundPatternId;
        private int _surfaceBackgroundPatternColorValue;
        private int _transparency;
        private int _cutLineWeight;
        private int _cutLineColorValue;
        private int _cutLinePatternId;
        private bool _cutForegroundPatternVisible;
        private int _cutForegroundPatternId;
        private int _cutForegroundPatternColorValue;
        private bool _cutBackgroundPatternVisible;
        private int _cutBackgroundPatternId;
        private int _cutBackgroundPatternColorValue;
        private bool _halftone;
        private ViewDetailLevel _detailLevel;

        public GraphicOverrideData()
        {
            _modifiedPropertyNames = new HashSet<string>(StringComparer.Ordinal);
            _mixedPropertyNames = new HashSet<string>(StringComparer.Ordinal);
            _originalPropertyValues = new Dictionary<string, object>(StringComparer.Ordinal);
            _originalMixedPropertyNames = new HashSet<string>(StringComparer.Ordinal);
            _projectionLineWeight = OverrideGraphicSettings.InvalidPenNumber;
            _projectionLineColorValue = NoColorValue;
            _projectionLinePatternId = ElementId.InvalidElementId.IntegerValue;
            _surfaceForegroundPatternId = ElementId.InvalidElementId.IntegerValue;
            _surfaceForegroundPatternColorValue = NoColorValue;
            _surfaceBackgroundPatternId = ElementId.InvalidElementId.IntegerValue;
            _surfaceBackgroundPatternColorValue = NoColorValue;
            _cutLineWeight = OverrideGraphicSettings.InvalidPenNumber;
            _cutLineColorValue = NoColorValue;
            _cutLinePatternId = ElementId.InvalidElementId.IntegerValue;
            _cutForegroundPatternId = ElementId.InvalidElementId.IntegerValue;
            _cutForegroundPatternColorValue = NoColorValue;
            _cutBackgroundPatternId = ElementId.InvalidElementId.IntegerValue;
            _cutBackgroundPatternColorValue = NoColorValue;
            _detailLevel = ViewDetailLevel.Undefined;
        }

        public int ProjectionLineWeight
        {
            get { return _projectionLineWeight; }
            set { SetTrackedField(ref _projectionLineWeight, value, ProjectionLineWeightProperty); }
        }

        public int ProjectionLineWeightState
        {
            get { return IsPropertyMixed(ProjectionLineWeightProperty) ? MixedIntegerValue : _projectionLineWeight; }
            set { if (value != MixedIntegerValue) ProjectionLineWeight = value; }
        }

        public int ProjectionLineColorValue
        {
            get { return _projectionLineColorValue; }
            set
            {
                if (SetTrackedField(ref _projectionLineColorValue, value, ProjectionLineColorValueProperty))
                {
                    OnPropertyChanged("ProjectionLineColorText");
                }
            }
        }

        public string ProjectionLineColorText
        {
            get { return IsPropertyMixed(ProjectionLineColorValueProperty) ? "Разные значения" : FormatColor(_projectionLineColorValue); }
        }

        public int ProjectionLinePatternId
        {
            get { return _projectionLinePatternId; }
            set { SetTrackedField(ref _projectionLinePatternId, value, ProjectionLinePatternIdProperty); }
        }

        public int ProjectionLinePatternIdState
        {
            get { return IsPropertyMixed(ProjectionLinePatternIdProperty) ? MixedIntegerValue : _projectionLinePatternId; }
            set { if (value != MixedIntegerValue) ProjectionLinePatternId = value; }
        }

        public bool SurfaceForegroundPatternVisible
        {
            get { return _surfaceForegroundPatternVisible; }
            set { SetTrackedField(ref _surfaceForegroundPatternVisible, value, SurfaceForegroundPatternVisibleProperty); }
        }

        public bool? SurfaceForegroundPatternVisibleState
        {
            get { return IsPropertyMixed(SurfaceForegroundPatternVisibleProperty) ? (bool?)null : _surfaceForegroundPatternVisible; }
            set { if (value.HasValue) SurfaceForegroundPatternVisible = value.Value; }
        }

        public int SurfaceForegroundPatternId
        {
            get { return _surfaceForegroundPatternId; }
            set
            {
                if (SetTrackedField(ref _surfaceForegroundPatternId, value, SurfaceForegroundPatternIdProperty) && _isTrackingChanges)
                {
                    SurfaceForegroundPatternVisible = value != ElementId.InvalidElementId.IntegerValue;
                    if (value == ElementId.InvalidElementId.IntegerValue)
                    {
                        SurfaceForegroundPatternColorValue = NoColorValue;
                    }
                }
            }
        }

        public int SurfaceForegroundPatternIdState
        {
            get { return IsPropertyMixed(SurfaceForegroundPatternIdProperty) ? MixedIntegerValue : _surfaceForegroundPatternId; }
            set { if (value != MixedIntegerValue) SurfaceForegroundPatternId = value; }
        }

        public int SurfaceForegroundPatternColorValue
        {
            get { return _surfaceForegroundPatternColorValue; }
            set
            {
                if (SetTrackedField(ref _surfaceForegroundPatternColorValue, value, SurfaceForegroundPatternColorValueProperty))
                {
                    OnPropertyChanged("SurfaceForegroundPatternColorText");
                }
            }
        }

        public string SurfaceForegroundPatternColorText
        {
            get { return IsPropertyMixed(SurfaceForegroundPatternColorValueProperty) ? "Разные значения" : FormatColor(_surfaceForegroundPatternColorValue); }
        }

        public bool SurfaceBackgroundPatternVisible
        {
            get { return _surfaceBackgroundPatternVisible; }
            set { SetTrackedField(ref _surfaceBackgroundPatternVisible, value, SurfaceBackgroundPatternVisibleProperty); }
        }

        public bool? SurfaceBackgroundPatternVisibleState
        {
            get { return IsPropertyMixed(SurfaceBackgroundPatternVisibleProperty) ? (bool?)null : _surfaceBackgroundPatternVisible; }
            set { if (value.HasValue) SurfaceBackgroundPatternVisible = value.Value; }
        }

        public int SurfaceBackgroundPatternId
        {
            get { return _surfaceBackgroundPatternId; }
            set
            {
                if (SetTrackedField(ref _surfaceBackgroundPatternId, value, SurfaceBackgroundPatternIdProperty) && _isTrackingChanges)
                {
                    SurfaceBackgroundPatternVisible = value != ElementId.InvalidElementId.IntegerValue;
                    if (value == ElementId.InvalidElementId.IntegerValue)
                    {
                        SurfaceBackgroundPatternColorValue = NoColorValue;
                    }
                }
            }
        }

        public int SurfaceBackgroundPatternIdState
        {
            get { return IsPropertyMixed(SurfaceBackgroundPatternIdProperty) ? MixedIntegerValue : _surfaceBackgroundPatternId; }
            set { if (value != MixedIntegerValue) SurfaceBackgroundPatternId = value; }
        }

        public int SurfaceBackgroundPatternColorValue
        {
            get { return _surfaceBackgroundPatternColorValue; }
            set
            {
                if (SetTrackedField(ref _surfaceBackgroundPatternColorValue, value, SurfaceBackgroundPatternColorValueProperty))
                {
                    OnPropertyChanged("SurfaceBackgroundPatternColorText");
                }
            }
        }

        public string SurfaceBackgroundPatternColorText
        {
            get { return IsPropertyMixed(SurfaceBackgroundPatternColorValueProperty) ? "Разные значения" : FormatColor(_surfaceBackgroundPatternColorValue); }
        }

        public int Transparency
        {
            get { return _transparency; }
            set
            {
                int safeValue = value;
                if (safeValue < 0)
                {
                    safeValue = 0;
                }
                else if (safeValue > 100)
                {
                    safeValue = 100;
                }

                SetTrackedField(ref _transparency, safeValue, TransparencyProperty);
            }
        }

        public string TransparencyEditorText
        {
            get
            {
                return IsPropertyMixed(TransparencyProperty)
                    ? "Разные значения"
                    : _transparency.ToString();
            }
            set
            {
                int parsedValue;
                if (int.TryParse(value, out parsedValue))
                {
                    Transparency = parsedValue;
                }
            }
        }

        public int CutLineWeight
        {
            get { return _cutLineWeight; }
            set { SetTrackedField(ref _cutLineWeight, value, CutLineWeightProperty); }
        }

        public int CutLineWeightState
        {
            get { return IsPropertyMixed(CutLineWeightProperty) ? MixedIntegerValue : _cutLineWeight; }
            set { if (value != MixedIntegerValue) CutLineWeight = value; }
        }

        public int CutLineColorValue
        {
            get { return _cutLineColorValue; }
            set
            {
                if (SetTrackedField(ref _cutLineColorValue, value, CutLineColorValueProperty))
                {
                    OnPropertyChanged("CutLineColorText");
                }
            }
        }

        public string CutLineColorText
        {
            get { return IsPropertyMixed(CutLineColorValueProperty) ? "Разные значения" : FormatColor(_cutLineColorValue); }
        }

        public int CutLinePatternId
        {
            get { return _cutLinePatternId; }
            set { SetTrackedField(ref _cutLinePatternId, value, CutLinePatternIdProperty); }
        }

        public int CutLinePatternIdState
        {
            get { return IsPropertyMixed(CutLinePatternIdProperty) ? MixedIntegerValue : _cutLinePatternId; }
            set { if (value != MixedIntegerValue) CutLinePatternId = value; }
        }

        public bool CutForegroundPatternVisible
        {
            get { return _cutForegroundPatternVisible; }
            set { SetTrackedField(ref _cutForegroundPatternVisible, value, CutForegroundPatternVisibleProperty); }
        }

        public bool? CutForegroundPatternVisibleState
        {
            get { return IsPropertyMixed(CutForegroundPatternVisibleProperty) ? (bool?)null : _cutForegroundPatternVisible; }
            set { if (value.HasValue) CutForegroundPatternVisible = value.Value; }
        }

        public int CutForegroundPatternId
        {
            get { return _cutForegroundPatternId; }
            set
            {
                if (SetTrackedField(ref _cutForegroundPatternId, value, CutForegroundPatternIdProperty) && _isTrackingChanges)
                {
                    CutForegroundPatternVisible = value != ElementId.InvalidElementId.IntegerValue;
                    if (value == ElementId.InvalidElementId.IntegerValue)
                    {
                        CutForegroundPatternColorValue = NoColorValue;
                    }
                }
            }
        }

        public int CutForegroundPatternIdState
        {
            get { return IsPropertyMixed(CutForegroundPatternIdProperty) ? MixedIntegerValue : _cutForegroundPatternId; }
            set { if (value != MixedIntegerValue) CutForegroundPatternId = value; }
        }

        public int CutForegroundPatternColorValue
        {
            get { return _cutForegroundPatternColorValue; }
            set
            {
                if (SetTrackedField(ref _cutForegroundPatternColorValue, value, CutForegroundPatternColorValueProperty))
                {
                    OnPropertyChanged("CutForegroundPatternColorText");
                }
            }
        }

        public string CutForegroundPatternColorText
        {
            get { return IsPropertyMixed(CutForegroundPatternColorValueProperty) ? "Разные значения" : FormatColor(_cutForegroundPatternColorValue); }
        }

        public bool CutBackgroundPatternVisible
        {
            get { return _cutBackgroundPatternVisible; }
            set { SetTrackedField(ref _cutBackgroundPatternVisible, value, CutBackgroundPatternVisibleProperty); }
        }

        public bool? CutBackgroundPatternVisibleState
        {
            get { return IsPropertyMixed(CutBackgroundPatternVisibleProperty) ? (bool?)null : _cutBackgroundPatternVisible; }
            set { if (value.HasValue) CutBackgroundPatternVisible = value.Value; }
        }

        public int CutBackgroundPatternId
        {
            get { return _cutBackgroundPatternId; }
            set
            {
                if (SetTrackedField(ref _cutBackgroundPatternId, value, CutBackgroundPatternIdProperty) && _isTrackingChanges)
                {
                    CutBackgroundPatternVisible = value != ElementId.InvalidElementId.IntegerValue;
                    if (value == ElementId.InvalidElementId.IntegerValue)
                    {
                        CutBackgroundPatternColorValue = NoColorValue;
                    }
                }
            }
        }

        public int CutBackgroundPatternIdState
        {
            get { return IsPropertyMixed(CutBackgroundPatternIdProperty) ? MixedIntegerValue : _cutBackgroundPatternId; }
            set { if (value != MixedIntegerValue) CutBackgroundPatternId = value; }
        }

        public int CutBackgroundPatternColorValue
        {
            get { return _cutBackgroundPatternColorValue; }
            set
            {
                if (SetTrackedField(ref _cutBackgroundPatternColorValue, value, CutBackgroundPatternColorValueProperty))
                {
                    OnPropertyChanged("CutBackgroundPatternColorText");
                }
            }
        }

        public string CutBackgroundPatternColorText
        {
            get { return IsPropertyMixed(CutBackgroundPatternColorValueProperty) ? "Разные значения" : FormatColor(_cutBackgroundPatternColorValue); }
        }

        public bool Halftone
        {
            get { return _halftone; }
            set { SetTrackedField(ref _halftone, value, HalftoneProperty); }
        }

        public bool? HalftoneState
        {
            get { return IsPropertyMixed(HalftoneProperty) ? (bool?)null : _halftone; }
            set { if (value.HasValue) Halftone = value.Value; }
        }

        public ViewDetailLevel DetailLevel
        {
            get { return _detailLevel; }
            set { SetTrackedField(ref _detailLevel, value, DetailLevelProperty); }
        }

        public ViewDetailLevel DetailLevelState
        {
            get { return IsPropertyMixed(DetailLevelProperty) ? MixedDetailLevelValue : _detailLevel; }
            set { if (value != MixedDetailLevelValue) DetailLevel = value; }
        }

        public bool IsModified
        {
            get { return _modifiedPropertyNames.Count > 0; }
        }

        public bool HasMixedValues
        {
            get { return _mixedPropertyNames.Count > 0; }
        }

        public string Summary
        {
            get
            {
                if (HasMixedValues)
                {
                    return IsModified ? "Разные значения •" : "Разные значения";
                }

                List<string> parts = new List<string>();
                if (HasProjectionOverride())
                {
                    parts.Add("Проекция");
                }

                if (HasSurfaceOverride())
                {
                    parts.Add("Поверхность");
                }

                if (HasCutOverride())
                {
                    parts.Add("Разрез");
                }

                if (_halftone)
                {
                    parts.Add("Полутона");
                }

                if (_detailLevel != ViewDetailLevel.Undefined)
                {
                    parts.Add("Детализация");
                }

                string result = parts.Count == 0 ? "Без переопределений" : string.Join(", ", parts.ToArray());
                return IsModified ? result + " •" : result;
            }
        }

        public string ProjectionLinesSummary
        {
            get
            {
                string[] properties =
                {
                    ProjectionLineWeightProperty,
                    ProjectionLineColorValueProperty,
                    ProjectionLinePatternIdProperty
                };
                bool hasOverride = _projectionLineWeight != OverrideGraphicSettings.InvalidPenNumber ||
                                   _projectionLineColorValue != NoColorValue ||
                                   _projectionLinePatternId != ElementId.InvalidElementId.IntegerValue;
                return FormatGroupSummary(properties, hasOverride);
            }
        }

        public string SurfacePatternsSummary
        {
            get
            {
                string[] properties =
                {
                    SurfaceForegroundPatternVisibleProperty,
                    SurfaceForegroundPatternIdProperty,
                    SurfaceForegroundPatternColorValueProperty,
                    SurfaceBackgroundPatternVisibleProperty,
                    SurfaceBackgroundPatternIdProperty,
                    SurfaceBackgroundPatternColorValueProperty
                };
                bool hasOverride = _surfaceForegroundPatternId != ElementId.InvalidElementId.IntegerValue ||
                                   _surfaceForegroundPatternColorValue != NoColorValue ||
                                   _surfaceBackgroundPatternId != ElementId.InvalidElementId.IntegerValue ||
                                   _surfaceBackgroundPatternColorValue != NoColorValue;
                return FormatGroupSummary(properties, hasOverride);
            }
        }

        public string TransparencySummary
        {
            get
            {
                if (IsPropertyMixed(TransparencyProperty))
                {
                    return "Разные";
                }

                return _transparency > 0 ? _transparency + "%" : "0%";
            }
        }

        public string CutLinesSummary
        {
            get
            {
                string[] properties =
                {
                    CutLineWeightProperty,
                    CutLineColorValueProperty,
                    CutLinePatternIdProperty
                };
                bool hasOverride = _cutLineWeight != OverrideGraphicSettings.InvalidPenNumber ||
                                   _cutLineColorValue != NoColorValue ||
                                   _cutLinePatternId != ElementId.InvalidElementId.IntegerValue;
                return FormatGroupSummary(properties, hasOverride);
            }
        }

        public string CutPatternsSummary
        {
            get
            {
                string[] properties =
                {
                    CutForegroundPatternVisibleProperty,
                    CutForegroundPatternIdProperty,
                    CutForegroundPatternColorValueProperty,
                    CutBackgroundPatternVisibleProperty,
                    CutBackgroundPatternIdProperty,
                    CutBackgroundPatternColorValueProperty
                };
                bool hasOverride = _cutForegroundPatternId != ElementId.InvalidElementId.IntegerValue ||
                                   _cutForegroundPatternColorValue != NoColorValue ||
                                   _cutBackgroundPatternId != ElementId.InvalidElementId.IntegerValue ||
                                   _cutBackgroundPatternColorValue != NoColorValue;
                return FormatGroupSummary(properties, hasOverride);
            }
        }

        public IEnumerable<string> ModifiedPropertyNames
        {
            get { return _modifiedPropertyNames; }
        }

        public bool IsPropertyModified(string propertyName)
        {
            return !string.IsNullOrEmpty(propertyName) && _modifiedPropertyNames.Contains(propertyName);
        }

        public bool IsPropertyMixed(string propertyName)
        {
            return !string.IsNullOrEmpty(propertyName) && _mixedPropertyNames.Contains(propertyName);
        }

        public void StartTrackingChanges()
        {
            _modifiedPropertyNames.Clear();
            CaptureOriginalValues();
            _originalMixedPropertyNames.Clear();
            foreach (string propertyName in _mixedPropertyNames)
            {
                _originalMixedPropertyNames.Add(propertyName);
            }

            _isTrackingChanges = true;
            RaiseSummaryPropertiesChanged();
        }

        public GraphicOverrideData CloneForEditing()
        {
            GraphicOverrideData clone = new GraphicOverrideData();
            clone._projectionLineWeight = _projectionLineWeight;
            clone._projectionLineColorValue = _projectionLineColorValue;
            clone._projectionLinePatternId = _projectionLinePatternId;
            clone._surfaceForegroundPatternVisible = _surfaceForegroundPatternVisible;
            clone._surfaceForegroundPatternId = _surfaceForegroundPatternId;
            clone._surfaceForegroundPatternColorValue = _surfaceForegroundPatternColorValue;
            clone._surfaceBackgroundPatternVisible = _surfaceBackgroundPatternVisible;
            clone._surfaceBackgroundPatternId = _surfaceBackgroundPatternId;
            clone._surfaceBackgroundPatternColorValue = _surfaceBackgroundPatternColorValue;
            clone._transparency = _transparency;
            clone._cutLineWeight = _cutLineWeight;
            clone._cutLineColorValue = _cutLineColorValue;
            clone._cutLinePatternId = _cutLinePatternId;
            clone._cutForegroundPatternVisible = _cutForegroundPatternVisible;
            clone._cutForegroundPatternId = _cutForegroundPatternId;
            clone._cutForegroundPatternColorValue = _cutForegroundPatternColorValue;
            clone._cutBackgroundPatternVisible = _cutBackgroundPatternVisible;
            clone._cutBackgroundPatternId = _cutBackgroundPatternId;
            clone._cutBackgroundPatternColorValue = _cutBackgroundPatternColorValue;
            clone._halftone = _halftone;
            clone._detailLevel = _detailLevel;
            foreach (string propertyName in _mixedPropertyNames)
            {
                clone._mixedPropertyNames.Add(propertyName);
            }

            clone.StartTrackingChanges();
            return clone;
        }

        public void ApplyEditedValues(GraphicOverrideData editedData)
        {
            if (editedData == null)
            {
                return;
            }

            List<string> modifiedProperties = new List<string>(editedData.ModifiedPropertyNames);
            for (int i = 0; i < modifiedProperties.Count; i++)
            {
                CopyEditedProperty(editedData, modifiedProperties[i]);
            }
        }

        public void MergeValues(GraphicOverrideData other)
        {
            if (other == null)
            {
                MarkAllPropertiesMixed();
            }
            else
            {
                MarkMixedWhenDifferent(ProjectionLineWeightProperty, _projectionLineWeight, other._projectionLineWeight);
                MarkMixedWhenDifferent(ProjectionLineColorValueProperty, _projectionLineColorValue, other._projectionLineColorValue);
                MarkMixedWhenDifferent(ProjectionLinePatternIdProperty, _projectionLinePatternId, other._projectionLinePatternId);
                MarkMixedWhenDifferent(SurfaceForegroundPatternVisibleProperty, _surfaceForegroundPatternVisible, other._surfaceForegroundPatternVisible);
                MarkMixedWhenDifferent(SurfaceForegroundPatternIdProperty, _surfaceForegroundPatternId, other._surfaceForegroundPatternId);
                MarkMixedWhenDifferent(SurfaceForegroundPatternColorValueProperty, _surfaceForegroundPatternColorValue, other._surfaceForegroundPatternColorValue);
                MarkMixedWhenDifferent(SurfaceBackgroundPatternVisibleProperty, _surfaceBackgroundPatternVisible, other._surfaceBackgroundPatternVisible);
                MarkMixedWhenDifferent(SurfaceBackgroundPatternIdProperty, _surfaceBackgroundPatternId, other._surfaceBackgroundPatternId);
                MarkMixedWhenDifferent(SurfaceBackgroundPatternColorValueProperty, _surfaceBackgroundPatternColorValue, other._surfaceBackgroundPatternColorValue);
                MarkMixedWhenDifferent(TransparencyProperty, _transparency, other._transparency);
                MarkMixedWhenDifferent(CutLineWeightProperty, _cutLineWeight, other._cutLineWeight);
                MarkMixedWhenDifferent(CutLineColorValueProperty, _cutLineColorValue, other._cutLineColorValue);
                MarkMixedWhenDifferent(CutLinePatternIdProperty, _cutLinePatternId, other._cutLinePatternId);
                MarkMixedWhenDifferent(CutForegroundPatternVisibleProperty, _cutForegroundPatternVisible, other._cutForegroundPatternVisible);
                MarkMixedWhenDifferent(CutForegroundPatternIdProperty, _cutForegroundPatternId, other._cutForegroundPatternId);
                MarkMixedWhenDifferent(CutForegroundPatternColorValueProperty, _cutForegroundPatternColorValue, other._cutForegroundPatternColorValue);
                MarkMixedWhenDifferent(CutBackgroundPatternVisibleProperty, _cutBackgroundPatternVisible, other._cutBackgroundPatternVisible);
                MarkMixedWhenDifferent(CutBackgroundPatternIdProperty, _cutBackgroundPatternId, other._cutBackgroundPatternId);
                MarkMixedWhenDifferent(CutBackgroundPatternColorValueProperty, _cutBackgroundPatternColorValue, other._cutBackgroundPatternColorValue);
                MarkMixedWhenDifferent(HalftoneProperty, _halftone, other._halftone);
                MarkMixedWhenDifferent(DetailLevelProperty, _detailLevel, other._detailLevel);
            }

            OnPropertyChanged("HasMixedValues");
            RaiseSummaryPropertiesChanged();
        }

        private void CopyEditedProperty(GraphicOverrideData source, string propertyName)
        {
            switch (propertyName)
            {
                case ProjectionLineWeightProperty:
                    ProjectionLineWeight = source.ProjectionLineWeight;
                    break;
                case ProjectionLineColorValueProperty:
                    ProjectionLineColorValue = source.ProjectionLineColorValue;
                    break;
                case ProjectionLinePatternIdProperty:
                    ProjectionLinePatternId = source.ProjectionLinePatternId;
                    break;
                case SurfaceForegroundPatternVisibleProperty:
                    SurfaceForegroundPatternVisible = source.SurfaceForegroundPatternVisible;
                    break;
                case SurfaceForegroundPatternIdProperty:
                    SurfaceForegroundPatternId = source.SurfaceForegroundPatternId;
                    break;
                case SurfaceForegroundPatternColorValueProperty:
                    SurfaceForegroundPatternColorValue = source.SurfaceForegroundPatternColorValue;
                    break;
                case SurfaceBackgroundPatternVisibleProperty:
                    SurfaceBackgroundPatternVisible = source.SurfaceBackgroundPatternVisible;
                    break;
                case SurfaceBackgroundPatternIdProperty:
                    SurfaceBackgroundPatternId = source.SurfaceBackgroundPatternId;
                    break;
                case SurfaceBackgroundPatternColorValueProperty:
                    SurfaceBackgroundPatternColorValue = source.SurfaceBackgroundPatternColorValue;
                    break;
                case TransparencyProperty:
                    Transparency = source.Transparency;
                    break;
                case CutLineWeightProperty:
                    CutLineWeight = source.CutLineWeight;
                    break;
                case CutLineColorValueProperty:
                    CutLineColorValue = source.CutLineColorValue;
                    break;
                case CutLinePatternIdProperty:
                    CutLinePatternId = source.CutLinePatternId;
                    break;
                case CutForegroundPatternVisibleProperty:
                    CutForegroundPatternVisible = source.CutForegroundPatternVisible;
                    break;
                case CutForegroundPatternIdProperty:
                    CutForegroundPatternId = source.CutForegroundPatternId;
                    break;
                case CutForegroundPatternColorValueProperty:
                    CutForegroundPatternColorValue = source.CutForegroundPatternColorValue;
                    break;
                case CutBackgroundPatternVisibleProperty:
                    CutBackgroundPatternVisible = source.CutBackgroundPatternVisible;
                    break;
                case CutBackgroundPatternIdProperty:
                    CutBackgroundPatternId = source.CutBackgroundPatternId;
                    break;
                case CutBackgroundPatternColorValueProperty:
                    CutBackgroundPatternColorValue = source.CutBackgroundPatternColorValue;
                    break;
                case HalftoneProperty:
                    Halftone = source.Halftone;
                    break;
                case DetailLevelProperty:
                    DetailLevel = source.DetailLevel;
                    break;
            }
        }

        private bool SetTrackedField<T>(ref T field, T value, string propertyName)
        {
            bool wasMixed = _mixedPropertyNames.Remove(propertyName);
            bool valueChanged = SetField(ref field, value, propertyName);
            if (!valueChanged && !wasMixed)
            {
                return false;
            }

            if (wasMixed)
            {
                OnPropertyChanged(GetStatePropertyName(propertyName));
                OnPropertyChanged("HasMixedValues");
            }

            if (!wasMixed)
            {
                OnPropertyChanged(GetStatePropertyName(propertyName));
            }

            if (_isTrackingChanges)
            {
                object originalValue;
                bool originalWasMixed = _originalMixedPropertyNames.Contains(propertyName);
                bool isCurrentlyMixed = _mixedPropertyNames.Contains(propertyName);
                bool isModified = originalWasMixed != isCurrentlyMixed;
                if (!isModified && !isCurrentlyMixed && _originalPropertyValues.TryGetValue(propertyName, out originalValue))
                {
                    isModified = !object.Equals(originalValue, value);
                }

                if (isModified)
                {
                    _modifiedPropertyNames.Add(propertyName);
                }
                else
                {
                    _modifiedPropertyNames.Remove(propertyName);
                }
            }

            RaiseSummaryPropertiesChanged();
            return true;
        }

        private void CaptureOriginalValues()
        {
            _originalPropertyValues.Clear();
            _originalPropertyValues[ProjectionLineWeightProperty] = _projectionLineWeight;
            _originalPropertyValues[ProjectionLineColorValueProperty] = _projectionLineColorValue;
            _originalPropertyValues[ProjectionLinePatternIdProperty] = _projectionLinePatternId;
            _originalPropertyValues[SurfaceForegroundPatternVisibleProperty] = _surfaceForegroundPatternVisible;
            _originalPropertyValues[SurfaceForegroundPatternIdProperty] = _surfaceForegroundPatternId;
            _originalPropertyValues[SurfaceForegroundPatternColorValueProperty] = _surfaceForegroundPatternColorValue;
            _originalPropertyValues[SurfaceBackgroundPatternVisibleProperty] = _surfaceBackgroundPatternVisible;
            _originalPropertyValues[SurfaceBackgroundPatternIdProperty] = _surfaceBackgroundPatternId;
            _originalPropertyValues[SurfaceBackgroundPatternColorValueProperty] = _surfaceBackgroundPatternColorValue;
            _originalPropertyValues[TransparencyProperty] = _transparency;
            _originalPropertyValues[CutLineWeightProperty] = _cutLineWeight;
            _originalPropertyValues[CutLineColorValueProperty] = _cutLineColorValue;
            _originalPropertyValues[CutLinePatternIdProperty] = _cutLinePatternId;
            _originalPropertyValues[CutForegroundPatternVisibleProperty] = _cutForegroundPatternVisible;
            _originalPropertyValues[CutForegroundPatternIdProperty] = _cutForegroundPatternId;
            _originalPropertyValues[CutForegroundPatternColorValueProperty] = _cutForegroundPatternColorValue;
            _originalPropertyValues[CutBackgroundPatternVisibleProperty] = _cutBackgroundPatternVisible;
            _originalPropertyValues[CutBackgroundPatternIdProperty] = _cutBackgroundPatternId;
            _originalPropertyValues[CutBackgroundPatternColorValueProperty] = _cutBackgroundPatternColorValue;
            _originalPropertyValues[HalftoneProperty] = _halftone;
            _originalPropertyValues[DetailLevelProperty] = _detailLevel;
        }

        private void MarkMixedWhenDifferent<T>(string propertyName, T first, T second)
        {
            if (!EqualityComparer<T>.Default.Equals(first, second))
            {
                MarkPropertyMixed(propertyName);
            }
        }

        private void MarkPropertyMixed(string propertyName)
        {
            if (_mixedPropertyNames.Add(propertyName))
            {
                OnPropertyChanged(GetStatePropertyName(propertyName));
                if (propertyName.IndexOf("ColorValue", StringComparison.Ordinal) >= 0)
                {
                    OnPropertyChanged(propertyName.Replace("Value", "Text"));
                }

                if (string.Equals(propertyName, TransparencyProperty, StringComparison.Ordinal))
                {
                    OnPropertyChanged("TransparencyEditorText");
                }
            }
        }

        private void MarkAllPropertiesMixed()
        {
            string[] propertyNames =
            {
                ProjectionLineWeightProperty,
                ProjectionLineColorValueProperty,
                ProjectionLinePatternIdProperty,
                SurfaceForegroundPatternVisibleProperty,
                SurfaceForegroundPatternIdProperty,
                SurfaceForegroundPatternColorValueProperty,
                SurfaceBackgroundPatternVisibleProperty,
                SurfaceBackgroundPatternIdProperty,
                SurfaceBackgroundPatternColorValueProperty,
                TransparencyProperty,
                CutLineWeightProperty,
                CutLineColorValueProperty,
                CutLinePatternIdProperty,
                CutForegroundPatternVisibleProperty,
                CutForegroundPatternIdProperty,
                CutForegroundPatternColorValueProperty,
                CutBackgroundPatternVisibleProperty,
                CutBackgroundPatternIdProperty,
                CutBackgroundPatternColorValueProperty,
                HalftoneProperty,
                DetailLevelProperty
            };

            for (int i = 0; i < propertyNames.Length; i++)
            {
                MarkPropertyMixed(propertyNames[i]);
            }
        }

        private static string GetStatePropertyName(string propertyName)
        {
            if (string.Equals(propertyName, TransparencyProperty, StringComparison.Ordinal))
            {
                return "TransparencyEditorText";
            }

            if (propertyName.IndexOf("ColorValue", StringComparison.Ordinal) >= 0)
            {
                return propertyName.Replace("Value", "Text");
            }

            return propertyName + "State";
        }

        private bool HasProjectionOverride()
        {
            return _projectionLineWeight != OverrideGraphicSettings.InvalidPenNumber ||
                   _projectionLineColorValue != NoColorValue ||
                   _projectionLinePatternId != ElementId.InvalidElementId.IntegerValue;
        }

        private bool HasSurfaceOverride()
        {
            return _surfaceForegroundPatternId != ElementId.InvalidElementId.IntegerValue ||
                   _surfaceForegroundPatternColorValue != NoColorValue ||
                   _surfaceBackgroundPatternId != ElementId.InvalidElementId.IntegerValue ||
                   _surfaceBackgroundPatternColorValue != NoColorValue ||
                   _transparency > 0;
        }

        private bool HasCutOverride()
        {
            return _cutLineWeight != OverrideGraphicSettings.InvalidPenNumber ||
                   _cutLineColorValue != NoColorValue ||
                   _cutLinePatternId != ElementId.InvalidElementId.IntegerValue ||
                   _cutForegroundPatternId != ElementId.InvalidElementId.IntegerValue ||
                   _cutForegroundPatternColorValue != NoColorValue ||
                   _cutBackgroundPatternId != ElementId.InvalidElementId.IntegerValue ||
                   _cutBackgroundPatternColorValue != NoColorValue;
        }

        private void RaiseSummaryPropertiesChanged()
        {
            OnPropertyChanged("IsModified");
            OnPropertyChanged("Summary");
            OnPropertyChanged("ProjectionLinesSummary");
            OnPropertyChanged("SurfacePatternsSummary");
            OnPropertyChanged("TransparencySummary");
            OnPropertyChanged("CutLinesSummary");
            OnPropertyChanged("CutPatternsSummary");
        }

        private string FormatGroupSummary(string[] propertyNames, bool hasOverride)
        {
            for (int i = 0; i < propertyNames.Length; i++)
            {
                if (IsPropertyMixed(propertyNames[i]))
                {
                    return "Разные";
                }
            }

            return hasOverride ? "Переопределено" : "По стилю";
        }

        private static string FormatColor(int colorValue)
        {
            if (colorValue == NoColorValue)
            {
                return "Без переопределения";
            }

            int red = (colorValue >> 16) & 255;
            int green = (colorValue >> 8) & 255;
            int blue = colorValue & 255;
            return red + ", " + green + ", " + blue;
        }
    }

    public class CategoryOverrideRow : NotifyPropertyChangedBase
    {
        private bool _isVisible;
        private bool _isVisibilityMixed;
        private bool _originalIsVisible;
        private bool _originalVisibilityWasMixed;
        private bool _isExpanded;
        private bool _isVisibleInTree;
        private bool _isComparisonActive;
        private bool _isVisibilityModified;
        private bool _isTrackingChanges;

        public CategoryOverrideRow()
        {
            _isExpanded = false;
            _isVisibleInTree = true;
            Graphics = new GraphicOverrideData();
            Graphics.PropertyChanged += Graphics_PropertyChanged;
        }

        public int CategoryIdValue { get; set; }

        public string Name { get; set; }

        public int IndentLevel { get; set; }

        public int ParentCategoryIdValue { get; set; }

        public bool HasChildren { get; set; }

        public bool AllowsVisibilityControl { get; set; }

        public bool SupportsCut { get; set; }

        public bool SupportsSurfacePatterns { get; set; }

        public bool SupportsTransparency { get; set; }

        public bool SupportsDetailLevel { get; set; }

        public GraphicOverrideData Graphics { get; private set; }

        public string DisplayName
        {
            get { return Name ?? string.Empty; }
        }

        public double NameIndent
        {
            get { return Math.Max(0, IndentLevel) * 18.0; }
        }

        public string HierarchyGuide
        {
            get { return IndentLevel > 0 ? "└─" : string.Empty; }
        }

        public bool IsExpanded
        {
            get { return _isExpanded; }
            set
            {
                if (SetField(ref _isExpanded, value, "IsExpanded"))
                {
                    OnPropertyChanged("ExpansionGlyph");
                }
            }
        }

        public string ExpansionGlyph
        {
            get { return _isExpanded ? "−" : "+"; }
        }

        public bool IsVisibleInTree
        {
            get { return _isVisibleInTree; }
            set { SetField(ref _isVisibleInTree, value, "IsVisibleInTree"); }
        }

        public bool IsComparisonActive
        {
            get { return _isComparisonActive; }
            set { SetField(ref _isComparisonActive, value, "IsComparisonActive"); }
        }

        public bool HasMixedTemplateValues
        {
            get { return _isVisibilityMixed || Graphics.HasMixedValues; }
        }

        public bool IsVisible
        {
            get { return _isVisible; }
            set
            {
                if (SetField(ref _isVisible, value, "IsVisible") && _isTrackingChanges)
                {
                    UpdateVisibilityModificationState();
                }
            }
        }

        public bool? VisibilityState
        {
            get { return _isVisibilityMixed ? (bool?)null : _isVisible; }
            set
            {
                if (!value.HasValue)
                {
                    return;
                }

                bool wasMixed = _isVisibilityMixed;
                _isVisibilityMixed = false;
                OnPropertyChanged("IsVisibilityMixed");
                OnPropertyChanged("HasMixedTemplateValues");
                if (_isVisible != value.Value)
                {
                    IsVisible = value.Value;
                }
                else if (wasMixed && _isTrackingChanges)
                {
                    UpdateVisibilityModificationState();
                }

                OnPropertyChanged("VisibilityState");
            }
        }

        public bool IsVisibilityMixed
        {
            get { return _isVisibilityMixed; }
        }

        public bool IsVisibilityModified
        {
            get { return _isVisibilityModified; }
        }

        public bool IsModified
        {
            get { return _isVisibilityModified || Graphics.IsModified; }
        }

        public void StartTrackingChanges()
        {
            _originalIsVisible = _isVisible;
            _originalVisibilityWasMixed = _isVisibilityMixed;
            _isVisibilityModified = false;
            _isTrackingChanges = true;
            Graphics.StartTrackingChanges();
            OnPropertyChanged("IsModified");
        }

        public void MergeVisibility(bool isVisible)
        {
            if (_isVisible != isVisible)
            {
                _isVisibilityMixed = true;
                OnPropertyChanged("VisibilityState");
                OnPropertyChanged("IsVisibilityMixed");
                OnPropertyChanged("HasMixedTemplateValues");
            }
        }

        private void UpdateVisibilityModificationState()
        {
            bool isModified = _originalVisibilityWasMixed
                ? !_isVisibilityMixed
                : _isVisibilityMixed || _isVisible != _originalIsVisible;
            if (_isVisibilityModified != isModified)
            {
                _isVisibilityModified = isModified;
                OnPropertyChanged("IsModified");
            }
        }

        private void Graphics_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, "IsModified", StringComparison.Ordinal) ||
                string.Equals(e.PropertyName, "Summary", StringComparison.Ordinal) ||
                string.Equals(e.PropertyName, "HasMixedValues", StringComparison.Ordinal))
            {
                OnPropertyChanged("IsModified");
                OnPropertyChanged("HasMixedTemplateValues");
            }
        }
    }

    public class CategoryTabData : NotifyPropertyChangedBase
    {
        private bool _isGroupVisible;
        private bool _isGroupVisibilityMixed;
        private bool _originalIsGroupVisible;
        private bool _originalGroupVisibilityWasMixed;
        private bool _isGroupVisibilityModified;
        private bool _isTrackingChanges;
        private string _searchText;
        private bool _isComparisonActive;

        public CategoryTabData(CategoryGraphicsGroup group, string title, int templateParameterIdValue)
        {
            Group = group;
            Title = title ?? string.Empty;
            Section = new TemplateSectionState(templateParameterIdValue, title);
            Rows = new ObservableCollection<CategoryOverrideRow>();
        }

        public CategoryGraphicsGroup Group { get; private set; }

        public string Title { get; private set; }

        public ObservableCollection<CategoryOverrideRow> Rows { get; private set; }

        public TemplateSectionState Section { get; private set; }

        public bool IsComparisonActive
        {
            get { return _isComparisonActive; }
            set { SetField(ref _isComparisonActive, value, "IsComparisonActive"); }
        }

        public bool IsGroupVisible
        {
            get { return _isGroupVisible; }
            set
            {
                if (SetField(ref _isGroupVisible, value, "IsGroupVisible") && _isTrackingChanges)
                {
                    UpdateGroupVisibilityModificationState();
                }
            }
        }

        public bool? GroupVisibilityState
        {
            get { return _isGroupVisibilityMixed ? (bool?)null : _isGroupVisible; }
            set
            {
                if (!value.HasValue)
                {
                    return;
                }

                bool wasMixed = _isGroupVisibilityMixed;
                _isGroupVisibilityMixed = false;
                if (_isGroupVisible != value.Value)
                {
                    IsGroupVisible = value.Value;
                }
                else if (wasMixed && _isTrackingChanges)
                {
                    UpdateGroupVisibilityModificationState();
                }

                OnPropertyChanged("GroupVisibilityState");
            }
        }

        public string SearchText
        {
            get { return _searchText ?? string.Empty; }
            set
            {
                if (SetField(ref _searchText, value ?? string.Empty, "SearchText"))
                {
                    RefreshRowVisibility();
                }
            }
        }

        public bool IsGroupVisibilityModified
        {
            get { return _isGroupVisibilityModified; }
        }

        public bool IsModified
        {
            get
            {
                if (_isGroupVisibilityModified || Section.IsModified)
                {
                    return true;
                }

                for (int i = 0; i < Rows.Count; i++)
                {
                    if (Rows[i].IsModified)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void StartTrackingChanges()
        {
            _originalIsGroupVisible = _isGroupVisible;
            _originalGroupVisibilityWasMixed = _isGroupVisibilityMixed;
            _isGroupVisibilityModified = false;
            _isTrackingChanges = true;
            Section.StartTrackingChanges();
            for (int i = 0; i < Rows.Count; i++)
            {
                Rows[i].StartTrackingChanges();
            }

            OnPropertyChanged("IsModified");
        }

        public void MergeGroupVisibility(bool isVisible)
        {
            if (_isGroupVisible != isVisible)
            {
                _isGroupVisibilityMixed = true;
                OnPropertyChanged("GroupVisibilityState");
            }
        }

        private void UpdateGroupVisibilityModificationState()
        {
            bool isModified = _originalGroupVisibilityWasMixed
                ? !_isGroupVisibilityMixed
                : _isGroupVisibilityMixed || _isGroupVisible != _originalIsGroupVisible;
            if (_isGroupVisibilityModified != isModified)
            {
                _isGroupVisibilityModified = isModified;
                OnPropertyChanged("IsModified");
            }
        }

        public void ToggleCategoryExpansion(CategoryOverrideRow row)
        {
            if (row == null || !row.HasChildren)
            {
                return;
            }

            row.IsExpanded = !row.IsExpanded;
            RefreshRowVisibility();
        }

        public void RefreshRowVisibility()
        {
            string search = SearchText.Trim();
            for (int i = 0; i < Rows.Count; i++)
            {
                CategoryOverrideRow row = Rows[i];
                if (search.Length > 0)
                {
                    row.IsVisibleInTree = MatchesSearch(row, search) ||
                                          HasMatchingAncestor(row, search) ||
                                          HasMatchingDescendant(row, search);
                }
                else
                {
                    row.IsVisibleInTree = !HasCollapsedAncestor(row);
                }
            }
        }

        private bool MatchesSearch(CategoryOverrideRow row, string search)
        {
            return row != null &&
                   (row.Name ?? string.Empty).IndexOf(search, StringComparison.CurrentCultureIgnoreCase) >= 0;
        }

        private bool HasMatchingAncestor(CategoryOverrideRow row, string search)
        {
            int parentIdValue = row.ParentCategoryIdValue;
            while (parentIdValue != ElementId.InvalidElementId.IntegerValue)
            {
                CategoryOverrideRow parent = FindRow(parentIdValue);
                if (parent == null)
                {
                    break;
                }

                if (MatchesSearch(parent, search))
                {
                    return true;
                }

                parentIdValue = parent.ParentCategoryIdValue;
            }

            return false;
        }

        private bool HasMatchingDescendant(CategoryOverrideRow row, string search)
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                CategoryOverrideRow candidate = Rows[i];
                int parentIdValue = candidate.ParentCategoryIdValue;
                while (parentIdValue != ElementId.InvalidElementId.IntegerValue)
                {
                    if (parentIdValue == row.CategoryIdValue)
                    {
                        if (MatchesSearch(candidate, search))
                        {
                            return true;
                        }

                        break;
                    }

                    CategoryOverrideRow parent = FindRow(parentIdValue);
                    if (parent == null)
                    {
                        break;
                    }

                    parentIdValue = parent.ParentCategoryIdValue;
                }
            }

            return false;
        }

        private bool HasCollapsedAncestor(CategoryOverrideRow row)
        {
            int parentIdValue = row.ParentCategoryIdValue;
            while (parentIdValue != ElementId.InvalidElementId.IntegerValue)
            {
                CategoryOverrideRow parent = FindRow(parentIdValue);
                if (parent == null)
                {
                    break;
                }

                if (!parent.IsExpanded)
                {
                    return true;
                }

                parentIdValue = parent.ParentCategoryIdValue;
            }

            return false;
        }

        private CategoryOverrideRow FindRow(int categoryIdValue)
        {
            for (int i = 0; i < Rows.Count; i++)
            {
                if (Rows[i].CategoryIdValue == categoryIdValue)
                {
                    return Rows[i];
                }
            }

            return null;
        }
    }

    public class FilterOverrideRow : NotifyPropertyChangedBase
    {
        private bool _isIncluded;
        private bool _isEnabled;
        private bool _isVisible;
        private bool _isIncludedMixed;
        private bool _isEnabledMixed;
        private bool _isVisibilityMixed;
        private bool _originalIsIncluded;
        private bool _originalIsEnabled;
        private bool _originalIsVisible;
        private bool _originalIncludedWasMixed;
        private bool _originalEnabledWasMixed;
        private bool _originalVisibilityWasMixed;
        private bool _isPresentInTable;
        private bool _matchesSearch;
        private bool _isIncludedModified;
        private bool _isEnabledModified;
        private bool _isVisibilityModified;
        private bool _isTrackingChanges;

        public FilterOverrideRow()
        {
            _isPresentInTable = true;
            _matchesSearch = true;
            Graphics = new GraphicOverrideData();
            Graphics.PropertyChanged += Graphics_PropertyChanged;
        }

        public int FilterIdValue { get; set; }

        public string Name { get; set; }

        public GraphicOverrideData Graphics { get; private set; }

        public bool IsIncluded
        {
            get { return _isIncluded; }
            set { SetTrackedBoolean(ref _isIncluded, value, "IsIncluded", ref _isIncludedModified); }
        }

        public bool? IncludedState
        {
            get { return _isIncludedMixed ? (bool?)null : _isIncluded; }
            set { SetMixedBoolean(value, ref _isIncluded, ref _isIncludedMixed, "IsIncluded", "IncludedState", ref _isIncludedModified); }
        }

        public bool IsEnabled
        {
            get { return _isEnabled; }
            set { SetTrackedBoolean(ref _isEnabled, value, "IsEnabled", ref _isEnabledModified); }
        }

        public bool? EnabledState
        {
            get { return _isEnabledMixed ? (bool?)null : _isEnabled; }
            set { SetMixedBoolean(value, ref _isEnabled, ref _isEnabledMixed, "IsEnabled", "EnabledState", ref _isEnabledModified); }
        }

        public bool IsVisible
        {
            get { return _isVisible; }
            set { SetTrackedBoolean(ref _isVisible, value, "IsVisible", ref _isVisibilityModified); }
        }

        public bool? VisibilityState
        {
            get { return _isVisibilityMixed ? (bool?)null : _isVisible; }
            set { SetMixedBoolean(value, ref _isVisible, ref _isVisibilityMixed, "IsVisible", "VisibilityState", ref _isVisibilityModified); }
        }

        public bool IsVisibleInList
        {
            get { return _isPresentInTable && _matchesSearch; }
            set
            {
                if (_matchesSearch != value)
                {
                    _matchesSearch = value;
                    OnPropertyChanged("IsVisibleInList");
                }
            }
        }

        public bool IsPresentInTable
        {
            get { return _isPresentInTable; }
            set
            {
                if (SetField(ref _isPresentInTable, value, "IsPresentInTable"))
                {
                    OnPropertyChanged("IsVisibleInList");
                }
            }
        }

        public bool CanEditGraphics
        {
            get { return _isIncluded || _isIncludedMixed; }
        }

        public bool IsIncludedModified
        {
            get { return _isIncludedModified; }
        }

        public bool IsEnabledModified
        {
            get { return _isEnabledModified; }
        }

        public bool IsVisibilityModified
        {
            get { return _isVisibilityModified; }
        }

        public bool IsModified
        {
            get { return _isIncludedModified || _isEnabledModified || _isVisibilityModified || Graphics.IsModified; }
        }

        public bool IsNewlyAdded
        {
            get
            {
                return _isIncludedModified &&
                       (!_originalIsIncluded || _originalIncludedWasMixed) &&
                       _isIncluded;
            }
        }

        public string StatusText
        {
            get
            {
                if (IsNewlyAdded)
                {
                    return "Добавлено";
                }

                return IsModified ? "Изменено" : string.Empty;
            }
        }

        public void StartTrackingChanges()
        {
            _originalIsIncluded = _isIncluded;
            _originalIsEnabled = _isEnabled;
            _originalIsVisible = _isVisible;
            _originalIncludedWasMixed = _isIncludedMixed;
            _originalEnabledWasMixed = _isEnabledMixed;
            _originalVisibilityWasMixed = _isVisibilityMixed;
            _isIncludedModified = false;
            _isEnabledModified = false;
            _isVisibilityModified = false;
            _isTrackingChanges = true;
            Graphics.StartTrackingChanges();
            OnPropertyChanged("IsModified");
            OnPropertyChanged("IsNewlyAdded");
            OnPropertyChanged("StatusText");
        }

        public void MarkIncludedAsModified()
        {
            _isIncludedModified = true;
            OnPropertyChanged("IsModified");
        }

        public void MergeValues(bool isIncluded, bool isEnabled, bool isVisible, GraphicOverrideData graphics)
        {
            if (_isIncluded != isIncluded)
            {
                _isIncludedMixed = true;
                OnPropertyChanged("IncludedState");
                OnPropertyChanged("CanEditGraphics");
            }

            if (_isEnabled != isEnabled || _isIncluded != isIncluded)
            {
                _isEnabledMixed = true;
                OnPropertyChanged("EnabledState");
            }

            if (_isVisible != isVisible || _isIncluded != isIncluded)
            {
                _isVisibilityMixed = true;
                OnPropertyChanged("VisibilityState");
            }

            Graphics.MergeValues(_isIncluded == isIncluded ? graphics : null);
        }

        private void SetTrackedBoolean(ref bool field, bool value, string propertyName, ref bool modificationFlag)
        {
            if (SetField(ref field, value, propertyName) && _isTrackingChanges)
            {
                UpdateBooleanModificationState(propertyName, ref modificationFlag);
            }
        }

        private void SetMixedBoolean(
            bool? value,
            ref bool field,
            ref bool mixedFlag,
            string propertyName,
            string statePropertyName,
            ref bool modificationFlag)
        {
            if (!value.HasValue)
            {
                return;
            }

            bool wasMixed = mixedFlag;
            mixedFlag = false;
            if (field != value.Value)
            {
                SetTrackedBoolean(ref field, value.Value, propertyName, ref modificationFlag);
            }
            else if (wasMixed && _isTrackingChanges)
            {
                UpdateBooleanModificationState(propertyName, ref modificationFlag);
            }

            OnPropertyChanged(statePropertyName);
            if (string.Equals(statePropertyName, "IncludedState", StringComparison.Ordinal))
            {
                OnPropertyChanged("CanEditGraphics");
            }
        }

        private void UpdateBooleanModificationState(string propertyName, ref bool modificationFlag)
        {
            bool currentValue;
            bool originalValue;
            bool currentIsMixed;
            bool originalWasMixed;

            if (string.Equals(propertyName, "IsIncluded", StringComparison.Ordinal))
            {
                currentValue = _isIncluded;
                originalValue = _originalIsIncluded;
                currentIsMixed = _isIncludedMixed;
                originalWasMixed = _originalIncludedWasMixed;
            }
            else if (string.Equals(propertyName, "IsEnabled", StringComparison.Ordinal))
            {
                currentValue = _isEnabled;
                originalValue = _originalIsEnabled;
                currentIsMixed = _isEnabledMixed;
                originalWasMixed = _originalEnabledWasMixed;
            }
            else
            {
                currentValue = _isVisible;
                originalValue = _originalIsVisible;
                currentIsMixed = _isVisibilityMixed;
                originalWasMixed = _originalVisibilityWasMixed;
            }

            modificationFlag = originalWasMixed
                ? !currentIsMixed
                : currentIsMixed || currentValue != originalValue;
            OnPropertyChanged("IsModified");
            OnPropertyChanged("IsNewlyAdded");
            OnPropertyChanged("StatusText");
        }

        private void Graphics_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, "IsModified", StringComparison.Ordinal))
            {
                OnPropertyChanged("IsModified");
                OnPropertyChanged("StatusText");
            }
        }
    }

    public class FilterSelectionDialogItem : NotifyPropertyChangedBase
    {
        private bool _isVisibleInList;
        private bool _isSelected;

        public FilterSelectionDialogItem()
        {
            _isVisibleInList = true;
        }

        public int FilterIdValue { get; set; }

        public string Name { get; set; }

        public bool IsAlreadyInTemplate { get; set; }

        public bool IsPartiallyIncluded { get; set; }

        public string Status
        {
            get
            {
                if (IsAlreadyInTemplate)
                {
                    return "Уже добавлен";
                }

                return IsPartiallyIncluded ? "Добавлен не во все" : string.Empty;
            }
        }

        public bool IsVisibleInList
        {
            get { return _isVisibleInList; }
            set { SetField(ref _isVisibleInList, value, "IsVisibleInList"); }
        }

        public bool IsSelected
        {
            get { return _isSelected; }
            set { SetField(ref _isSelected, value, "IsSelected"); }
        }
    }

    public class WorksetOverrideRow : NotifyPropertyChangedBase
    {
        public const int MixedVisibilityValue = int.MinValue + 121;

        private WorksetVisibility _visibility;
        private bool _isMixed;
        private WorksetVisibility _originalVisibility;
        private bool _originalWasMixed;
        private bool _isVisibleInList;
        private bool _isModified;
        private bool _isTrackingChanges;

        public WorksetOverrideRow()
        {
            _isVisibleInList = true;
        }

        public int WorksetIdValue { get; set; }

        public string Name { get; set; }

        public WorksetVisibility Visibility
        {
            get { return _visibility; }
            set
            {
                if (SetField(ref _visibility, value, "Visibility") && _isTrackingChanges)
                {
                    UpdateModificationState();
                    OnPropertyChanged("VisibilityValue");
                }
            }
        }

        public int VisibilityValue
        {
            get { return _isMixed ? MixedVisibilityValue : (int)_visibility; }
            set
            {
                if (value == MixedVisibilityValue)
                {
                    return;
                }

                bool wasMixed = _isMixed;
                _isMixed = false;
                if ((int)_visibility != value)
                {
                    Visibility = (WorksetVisibility)value;
                }
                else if (wasMixed && _isTrackingChanges)
                {
                    UpdateModificationState();
                }

                OnPropertyChanged("VisibilityValue");
            }
        }

        public bool IsVisibleInList
        {
            get { return _isVisibleInList; }
            set { SetField(ref _isVisibleInList, value, "IsVisibleInList"); }
        }

        public bool IsModified
        {
            get { return _isModified; }
        }

        public bool IsMixed
        {
            get { return _isMixed; }
        }

        public void StartTrackingChanges()
        {
            _originalVisibility = _visibility;
            _originalWasMixed = _isMixed;
            _isModified = false;
            _isTrackingChanges = true;
            OnPropertyChanged("IsModified");
        }

        public void MergeVisibility(WorksetVisibility visibility)
        {
            if (_visibility != visibility)
            {
                _isMixed = true;
                OnPropertyChanged("VisibilityValue");
            }
        }

        private void UpdateModificationState()
        {
            bool isModified = _originalWasMixed
                ? !_isMixed
                : _isMixed || _visibility != _originalVisibility;
            if (_isModified != isModified)
            {
                _isModified = isModified;
                OnPropertyChanged("IsModified");
            }
        }
    }

    public class RevitLinkInfo : NotifyPropertyChangedBase
    {
        public const string MixedVisibilityTypeName = "__SAB_MIXED__";
        public const int MixedLinkedViewIdValue = int.MinValue + 122;

        private string _visibilityTypeName;
        private int _linkedViewIdValue;
        private bool _isVisibilityTypeMixed;
        private bool _isLinkedViewMixed;
        private string _originalVisibilityTypeName;
        private int _originalLinkedViewIdValue;
        private bool _originalVisibilityTypeWasMixed;
        private bool _originalLinkedViewWasMixed;
        private bool _isVisibleInList;
        private bool _isVisibilityTypeModified;
        private bool _isLinkedViewModified;
        private bool _isTrackingChanges;

        public RevitLinkInfo()
        {
            _isVisibleInList = true;
            LinkedViews = new ObservableCollection<NamedElementOption>();
            _linkedViewIdValue = ElementId.InvalidElementId.IntegerValue;
        }

        public int LinkElementIdValue { get; set; }

        public string Name { get; set; }

        public string Status { get; set; }

        public int InstanceCount { get; set; }

        public bool IsInstance { get; set; }

        public int IndentLevel { get; set; }

        public bool IsApiSupported { get; set; }

        public ObservableCollection<NamedElementOption> LinkedViews { get; private set; }

        public string DisplayName
        {
            get { return new string(' ', Math.Max(0, IndentLevel) * 4) + (Name ?? string.Empty); }
        }

        public string VisibilityTypeName
        {
            get { return _isVisibilityTypeMixed ? MixedVisibilityTypeName : _visibilityTypeName; }
            set
            {
                if (string.Equals(value, MixedVisibilityTypeName, StringComparison.Ordinal))
                {
                    return;
                }

                bool wasMixed = _isVisibilityTypeMixed;
                _isVisibilityTypeMixed = false;
                if (SetField(ref _visibilityTypeName, value, "VisibilityTypeName") && _isTrackingChanges)
                {
                    UpdateVisibilityTypeModificationState();
                    OnPropertyChanged("CanSelectLinkedView");
                }
                else if (wasMixed && _isTrackingChanges)
                {
                    UpdateVisibilityTypeModificationState();
                }
            }
        }

        public bool CanSelectLinkedView
        {
            get
            {
                return IsApiSupported &&
                       !_isVisibilityTypeMixed &&
                       !string.IsNullOrWhiteSpace(_visibilityTypeName) &&
                       _visibilityTypeName.IndexOf("Linked", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        public int LinkedViewIdValue
        {
            get { return _isLinkedViewMixed ? MixedLinkedViewIdValue : _linkedViewIdValue; }
            set
            {
                if (value == MixedLinkedViewIdValue)
                {
                    return;
                }

                bool wasMixed = _isLinkedViewMixed;
                _isLinkedViewMixed = false;
                if (SetField(ref _linkedViewIdValue, value, "LinkedViewIdValue") && _isTrackingChanges)
                {
                    UpdateLinkedViewModificationState();
                }
                else if (wasMixed && _isTrackingChanges)
                {
                    UpdateLinkedViewModificationState();
                }
            }
        }

        public bool IsVisibleInList
        {
            get { return _isVisibleInList; }
            set { SetField(ref _isVisibleInList, value, "IsVisibleInList"); }
        }

        public bool IsVisibilityTypeModified
        {
            get { return _isVisibilityTypeModified; }
        }

        public bool IsLinkedViewModified
        {
            get { return _isLinkedViewModified; }
        }

        public bool IsModified
        {
            get { return _isVisibilityTypeModified || _isLinkedViewModified; }
        }

        public bool IsVisibilityTypeMixed
        {
            get { return _isVisibilityTypeMixed; }
        }

        public bool IsLinkedViewMixed
        {
            get { return _isLinkedViewMixed; }
        }

        public void StartTrackingChanges()
        {
            _originalVisibilityTypeName = _visibilityTypeName;
            _originalLinkedViewIdValue = _linkedViewIdValue;
            _originalVisibilityTypeWasMixed = _isVisibilityTypeMixed;
            _originalLinkedViewWasMixed = _isLinkedViewMixed;
            _isVisibilityTypeModified = false;
            _isLinkedViewModified = false;
            _isTrackingChanges = true;
            OnPropertyChanged("IsModified");
        }

        public void MergeValues(string visibilityTypeName, int linkedViewIdValue)
        {
            if (!string.Equals(_visibilityTypeName, visibilityTypeName, StringComparison.Ordinal))
            {
                _isVisibilityTypeMixed = true;
                OnPropertyChanged("VisibilityTypeName");
                OnPropertyChanged("CanSelectLinkedView");
            }

            if (_linkedViewIdValue != linkedViewIdValue || _isVisibilityTypeMixed)
            {
                _isLinkedViewMixed = true;
                OnPropertyChanged("LinkedViewIdValue");
            }
        }

        private void UpdateVisibilityTypeModificationState()
        {
            _isVisibilityTypeModified = _originalVisibilityTypeWasMixed
                ? !_isVisibilityTypeMixed
                : _isVisibilityTypeMixed || !string.Equals(
                    _visibilityTypeName,
                    _originalVisibilityTypeName,
                    StringComparison.Ordinal);
            OnPropertyChanged("IsModified");
        }

        private void UpdateLinkedViewModificationState()
        {
            _isLinkedViewModified = _originalLinkedViewWasMixed
                ? !_isLinkedViewMixed
                : _isLinkedViewMixed || _linkedViewIdValue != _originalLinkedViewIdValue;
            OnPropertyChanged("IsModified");
        }
    }

    public class TemplateSelectionItem : NotifyPropertyChangedBase
    {
        private bool _isTarget;
        private bool _isSource;
        private bool _isVisibleInList;

        public TemplateSelectionItem()
        {
            _isVisibleInList = true;
        }

        public int TemplateIdValue { get; set; }

        public string Name { get; set; }

        public string ViewTypeName { get; set; }

        public bool IsTarget
        {
            get { return _isTarget; }
            set { SetField(ref _isTarget, value, "IsTarget"); }
        }

        public bool IsSource
        {
            get { return _isSource; }
            set { SetField(ref _isSource, value, "IsSource"); }
        }

        public bool IsVisibleInList
        {
            get { return _isVisibleInList; }
            set { SetField(ref _isVisibleInList, value, "IsVisibleInList"); }
        }
    }

    public class ViewTemplateGraphicsData
    {
        private readonly List<int> _originalFilterOrder;

        public ViewTemplateGraphicsData()
        {
            _originalFilterOrder = new List<int>();
            ModelCategories = new CategoryTabData(
                CategoryGraphicsGroup.Model,
                "Категории модели",
                (int)BuiltInParameter.VIS_GRAPHICS_MODEL);
            AnnotationCategories = new CategoryTabData(
                CategoryGraphicsGroup.Annotation,
                "Категории аннотаций",
                (int)BuiltInParameter.VIS_GRAPHICS_ANNOTATION);
            AnalyticalCategories = new CategoryTabData(
                CategoryGraphicsGroup.AnalyticalModel,
                "Категории аналитической модели",
                (int)BuiltInParameter.VIS_GRAPHICS_ANALYTICAL_MODEL);
            ImportedCategories = new CategoryTabData(
                CategoryGraphicsGroup.Imported,
                "Импортированные категории",
                (int)BuiltInParameter.VIS_GRAPHICS_IMPORT);
            FiltersSection = new TemplateSectionState((int)BuiltInParameter.VIS_GRAPHICS_FILTERS, "Фильтры");
            WorksetsSection = new TemplateSectionState((int)BuiltInParameter.VIS_GRAPHICS_WORKSETS, "Рабочие наборы");
            RevitLinksSection = new TemplateSectionState((int)BuiltInParameter.VIS_GRAPHICS_RVT_LINKS, "Связанные файлы");
            Filters = new ObservableCollection<FilterOverrideRow>();
            Worksets = new ObservableCollection<WorksetOverrideRow>();
            RevitLinks = new ObservableCollection<RevitLinkInfo>();
            RevitLinkVisibilityTypes = new ObservableCollection<NamedStringOption>();
            LinePatterns = new ObservableCollection<NamedElementOption>();
            FillPatterns = new ObservableCollection<NamedElementOption>();
            LineWeights = new ObservableCollection<NamedIntegerOption>();
            DetailLevels = new ObservableCollection<NamedDetailLevelOption>();
            WorksetVisibilities = new ObservableCollection<NamedIntegerOption>();
        }

        public int SourceTemplateIdValue { get; set; }

        public string SourceTemplateName { get; set; }

        public CategoryTabData ModelCategories { get; private set; }

        public CategoryTabData AnnotationCategories { get; private set; }

        public CategoryTabData AnalyticalCategories { get; private set; }

        public CategoryTabData ImportedCategories { get; private set; }

        public ObservableCollection<FilterOverrideRow> Filters { get; private set; }

        public TemplateSectionState FiltersSection { get; private set; }

        public ObservableCollection<WorksetOverrideRow> Worksets { get; private set; }

        public TemplateSectionState WorksetsSection { get; private set; }

        public ObservableCollection<RevitLinkInfo> RevitLinks { get; private set; }

        public TemplateSectionState RevitLinksSection { get; private set; }

        public ObservableCollection<NamedStringOption> RevitLinkVisibilityTypes { get; private set; }

        public bool SupportsRevitLinkOverrides { get; set; }

        public ObservableCollection<NamedElementOption> LinePatterns { get; private set; }

        public ObservableCollection<NamedElementOption> FillPatterns { get; private set; }

        public ObservableCollection<NamedIntegerOption> LineWeights { get; private set; }

        public ObservableCollection<NamedDetailLevelOption> DetailLevels { get; private set; }

        public ObservableCollection<NamedIntegerOption> WorksetVisibilities { get; private set; }

        public bool IsFilterOrderModified { get; set; }

        public void UpdateFilterOrderModificationState()
        {
            if (_originalFilterOrder.Count != Filters.Count)
            {
                IsFilterOrderModified = true;
                return;
            }

            for (int i = 0; i < Filters.Count; i++)
            {
                if (_originalFilterOrder[i] != Filters[i].FilterIdValue)
                {
                    IsFilterOrderModified = true;
                    return;
                }
            }

            IsFilterOrderModified = false;
        }

        public bool IsDirty
        {
            get
            {
                if (ModelCategories.IsModified ||
                    AnnotationCategories.IsModified ||
                    AnalyticalCategories.IsModified ||
                    ImportedCategories.IsModified ||
                    IsFilterOrderModified ||
                    FiltersSection.IsModified ||
                    WorksetsSection.IsModified ||
                    RevitLinksSection.IsModified)
                {
                    return true;
                }

                for (int i = 0; i < Filters.Count; i++)
                {
                    if (Filters[i].IsModified)
                    {
                        return true;
                    }
                }

                for (int i = 0; i < Worksets.Count; i++)
                {
                    if (Worksets[i].IsModified)
                    {
                        return true;
                    }
                }

                for (int i = 0; i < RevitLinks.Count; i++)
                {
                    if (RevitLinks[i].IsModified)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public int CountModifiedRows()
        {
            int count = 0;
            CategoryTabData[] categoryTabs =
            {
                ModelCategories,
                AnnotationCategories,
                AnalyticalCategories,
                ImportedCategories
            };

            for (int tabIndex = 0; tabIndex < categoryTabs.Length; tabIndex++)
            {
                if (categoryTabs[tabIndex].IsGroupVisibilityModified)
                {
                    count++;
                }

                if (categoryTabs[tabIndex].Section.IsModified)
                {
                    count++;
                }

                for (int rowIndex = 0; rowIndex < categoryTabs[tabIndex].Rows.Count; rowIndex++)
                {
                    if (categoryTabs[tabIndex].Rows[rowIndex].IsModified)
                    {
                        count++;
                    }
                }
            }

            if (IsFilterOrderModified)
            {
                count++;
            }

            for (int filterIndex = 0; filterIndex < Filters.Count; filterIndex++)
            {
                if (Filters[filterIndex].IsModified)
                {
                    count++;
                }
            }

            if (FiltersSection.IsModified)
            {
                count++;
            }

            if (WorksetsSection.IsModified)
            {
                count++;
            }

            if (RevitLinksSection.IsModified)
            {
                count++;
            }

            for (int worksetIndex = 0; worksetIndex < Worksets.Count; worksetIndex++)
            {
                if (Worksets[worksetIndex].IsModified)
                {
                    count++;
                }
            }

            for (int linkIndex = 0; linkIndex < RevitLinks.Count; linkIndex++)
            {
                if (RevitLinks[linkIndex].IsModified)
                {
                    count++;
                }
            }

            return count;
        }

        public void StartTrackingChanges()
        {
            ModelCategories.StartTrackingChanges();
            AnnotationCategories.StartTrackingChanges();
            AnalyticalCategories.StartTrackingChanges();
            ImportedCategories.StartTrackingChanges();
            FiltersSection.StartTrackingChanges();
            WorksetsSection.StartTrackingChanges();
            RevitLinksSection.StartTrackingChanges();

            for (int i = 0; i < Filters.Count; i++)
            {
                Filters[i].StartTrackingChanges();
            }

            for (int i = 0; i < Worksets.Count; i++)
            {
                Worksets[i].StartTrackingChanges();
            }

            for (int i = 0; i < RevitLinks.Count; i++)
            {
                RevitLinks[i].StartTrackingChanges();
            }

            _originalFilterOrder.Clear();
            for (int i = 0; i < Filters.Count; i++)
            {
                _originalFilterOrder.Add(Filters[i].FilterIdValue);
            }

            IsFilterOrderModified = false;
        }
    }

    public class ApplyViewTemplateGraphicsResult
    {
        public ApplyViewTemplateGraphicsResult()
        {
            Warnings = new List<string>();
        }

        public int ProcessedTemplateCount { get; set; }

        public int ChangedSettingCount { get; set; }

        public List<string> Warnings { get; private set; }
    }
}
