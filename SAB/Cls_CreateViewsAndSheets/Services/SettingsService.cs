using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using SAB.CreateViewsAndSheets.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.CreateViewsAndSheets.Services
{
    public class SettingsService
    {
        private const int CurrentSchemaVersion = 1;
        private readonly string _settingsFilePath;

        public SettingsService()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string settingsDirectory = Path.Combine(appDataPath, "SAB", "CreateViewsAndSheets");
            _settingsFilePath = Path.Combine(settingsDirectory, "settings.json");
        }

        public CreateViewsAndSheetsSettings LoadSettings(Document document, IList<string> warnings)
        {
            try
            {
                if (!File.Exists(_settingsFilePath))
                {
                    return null;
                }

                string json = File.ReadAllText(_settingsFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                PersistedSettings persisted = JsonConvert.DeserializeObject<PersistedSettings>(json);
                if (persisted == null || persisted.SchemaVersion != CurrentSchemaVersion)
                {
                    return null;
                }

                return ConvertToSettings(document, persisted, warnings);
            }
            catch (Exception exception)
            {
                AddWarning(warnings, "Не удалось загрузить настройки команды: " + exception.Message);
                return null;
            }
        }

        public void SaveSettings(CreateViewsAndSheetsSettings settings)
        {
            if (settings == null || settings.Placement == null)
            {
                return;
            }

            try
            {
                PersistedSettings persisted = ConvertFromSettings(settings);

                string settingsDirectory = Path.GetDirectoryName(_settingsFilePath);
                if (!Directory.Exists(settingsDirectory))
                {
                    Directory.CreateDirectory(settingsDirectory);
                }

                string json = JsonConvert.SerializeObject(persisted, Formatting.Indented);
                File.WriteAllText(_settingsFilePath, json);
            }
            catch
            {
                // Ошибка сохранения настроек не должна отменять уже выполненную операцию Revit.
            }
        }

        private CreateViewsAndSheetsSettings ConvertToSettings(Document document, PersistedSettings persisted, IList<string> warnings)
        {
            CreateViewsAndSheetsSettings settings = new CreateViewsAndSheetsSettings();
            settings.StructureMode = persisted.StructureModeValue == (int)CreateViewsAndSheetsStructureMode.MultiStory
                ? CreateViewsAndSheetsStructureMode.MultiStory
                : CreateViewsAndSheetsStructureMode.SingleStory;
            settings.SourceViewId = RestoreElementId(document, persisted.SourceViewIdValue, "эталонный вид", warnings);
            settings.SourceSheetId = RestoreElementId(document, persisted.SourceSheetIdValue, "эталонный лист", warnings);
            settings.CeilingSourceViewId = RestoreElementId(document, persisted.CeilingSourceViewIdValue, "вид-образец плана потолков", warnings);
            settings.CeilingSourceSheetId = RestoreElementId(document, persisted.CeilingSourceSheetIdValue, "лист-образец плана потолков", warnings);
            settings.ViewportTypeId = RestoreElementId(document, persisted.ViewportTypeIdValue, "тип Viewport", warnings);
            settings.TitleBlockTypeId = RestoreElementId(document, persisted.TitleBlockTypeIdValue, "тип основной надписи", warnings);
            settings.SheetBrowserParameterId = RestoreParameterId(persisted.SheetBrowserParameterIdValue);

            settings.FloorMappings = RestoreFloorMappings(document, persisted.FloorMappings, warnings);
            settings.SessionRows = RestoreSessionRows(document, persisted.SessionRows, warnings);

            settings.Placement = new PlacementSettings();
            settings.Placement.CoordinateUnits = string.IsNullOrWhiteSpace(persisted.CoordinateUnits)
                ? "мм"
                : persisted.CoordinateUnits;
            settings.Placement.ViewCenterXmm = persisted.ViewCenterXmm;
            settings.Placement.ViewCenterYmm = persisted.ViewCenterYmm;
            settings.Placement.ViewTitleXmm = persisted.ViewTitleXmm;
            settings.Placement.ViewTitleYmm = persisted.ViewTitleYmm;
            settings.Placement.TitleLineLengthMm = persisted.TitleLineLengthMm;
            settings.Placement.UsePointSelectionForViewCenter = persisted.UsePointSelectionForViewCenter;
            settings.Placement.UsePointSelectionForViewTitle = persisted.UsePointSelectionForViewTitle;
            settings.Placement.SaveSettings = persisted.SaveSettings;
            settings.DetailCopy = new SheetDetailCopySettings();
            settings.DetailCopy.CopySheetWithDetailing = persisted.CopySheetWithDetailing;
            settings.DetailCopy.CopySchedules = GetNullableBoolValue(persisted.CopySchedules, true);
            settings.DetailCopy.CopyLegends = GetNullableBoolValue(persisted.CopyLegends, true);
            settings.DetailCopy.CopyDraftingViews = GetNullableBoolValue(persisted.CopyDraftingViews, true);
            settings.DetailCopy.CopyDetailLines = GetNullableBoolValue(persisted.CopyDetailLines, true);
            settings.DetailCopy.CopyFilledRegions = GetNullableBoolValue(persisted.CopyFilledRegions, true);
            settings.DetailCopy.CopyTextNotes = GetNullableBoolValue(persisted.CopyTextNotes, true);
            settings.DetailCopy.CopyGenericAnnotations = GetNullableBoolValue(persisted.CopyGenericAnnotations, true);
            settings.DetailCopy.CopyImages = GetNullableBoolValue(persisted.CopyImages, true);

            return settings;
        }

        private PersistedSettings ConvertFromSettings(CreateViewsAndSheetsSettings settings)
        {
            PlacementSettings placement = settings.Placement ?? new PlacementSettings();
            SheetDetailCopySettings detailCopy = settings.DetailCopy ?? new SheetDetailCopySettings();

            PersistedSettings persisted = new PersistedSettings();
            persisted.SchemaVersion = CurrentSchemaVersion;
            persisted.StructureModeValue = (int)settings.StructureMode;
            persisted.SourceViewIdValue = RevitElementIdUtils.GetElementIdValue(settings.SourceViewId);
            persisted.SourceSheetIdValue = RevitElementIdUtils.GetElementIdValue(settings.SourceSheetId);
            persisted.CeilingSourceViewIdValue = RevitElementIdUtils.GetElementIdValue(settings.CeilingSourceViewId);
            persisted.CeilingSourceSheetIdValue = RevitElementIdUtils.GetElementIdValue(settings.CeilingSourceSheetId);
            persisted.ViewportTypeIdValue = RevitElementIdUtils.GetElementIdValue(settings.ViewportTypeId);
            persisted.TitleBlockTypeIdValue = RevitElementIdUtils.GetElementIdValue(settings.TitleBlockTypeId);
            persisted.SheetBrowserParameterIdValue = RevitElementIdUtils.GetElementIdValue(settings.SheetBrowserParameterId);
            persisted.CoordinateUnits = placement.CoordinateUnits;
            persisted.ViewCenterXmm = placement.ViewCenterXmm;
            persisted.ViewCenterYmm = placement.ViewCenterYmm;
            persisted.ViewTitleXmm = placement.ViewTitleXmm;
            persisted.ViewTitleYmm = placement.ViewTitleYmm;
            persisted.TitleLineLengthMm = placement.TitleLineLengthMm;
            persisted.UsePointSelectionForViewCenter = placement.UsePointSelectionForViewCenter;
            persisted.UsePointSelectionForViewTitle = placement.UsePointSelectionForViewTitle;
            persisted.SaveSettings = placement.SaveSettings;
            persisted.CopySheetWithDetailing = detailCopy.CopySheetWithDetailing;
            persisted.CopySchedules = detailCopy.CopySchedules;
            persisted.CopyLegends = detailCopy.CopyLegends;
            persisted.CopyDraftingViews = detailCopy.CopyDraftingViews;
            persisted.CopyDetailLines = detailCopy.CopyDetailLines;
            persisted.CopyFilledRegions = detailCopy.CopyFilledRegions;
            persisted.CopyTextNotes = detailCopy.CopyTextNotes;
            persisted.CopyGenericAnnotations = detailCopy.CopyGenericAnnotations;
            persisted.CopyImages = detailCopy.CopyImages;
            persisted.FloorMappings = ConvertFloorMappings(settings.FloorMappings);
            persisted.SessionRows = ConvertSessionRows(settings.SessionRows);
            return persisted;
        }

        private List<FloorSourceMapping> RestoreFloorMappings(
            Document document,
            IList<PersistedFloorMapping> persistedMappings,
            IList<string> warnings)
        {
            List<FloorSourceMapping> result = new List<FloorSourceMapping>();
            if (persistedMappings == null)
            {
                return result;
            }

            for (int i = 0; i < persistedMappings.Count; i++)
            {
                PersistedFloorMapping persistedMapping = persistedMappings[i];
                if (persistedMapping == null)
                {
                    continue;
                }

                FloorSourceMapping mapping = new FloorSourceMapping();
                mapping.FloorName = persistedMapping.FloorName ?? string.Empty;
                mapping.FloorId = ElementId.InvalidElementId;
                mapping.SourceViewId = RestoreElementId(document, persistedMapping.SourceViewIdValue, "вид-образец этажа " + mapping.FloorName, warnings);
                mapping.SourceSheetId = RestoreElementId(document, persistedMapping.SourceSheetIdValue, "лист-образец этажа " + mapping.FloorName, warnings);
                mapping.CeilingSourceViewId = RestoreElementId(document, persistedMapping.CeilingSourceViewIdValue, "ceiling source view for floor " + mapping.FloorName, warnings);
                mapping.CeilingSourceSheetId = RestoreElementId(document, persistedMapping.CeilingSourceSheetIdValue, "ceiling source sheet for floor " + mapping.FloorName, warnings);
                result.Add(mapping);
            }

            return result;
        }

        private List<PersistedFloorMapping> ConvertFloorMappings(IList<FloorSourceMapping> mappings)
        {
            List<PersistedFloorMapping> result = new List<PersistedFloorMapping>();
            if (mappings == null)
            {
                return result;
            }

            for (int i = 0; i < mappings.Count; i++)
            {
                FloorSourceMapping mapping = mappings[i];
                if (mapping == null)
                {
                    continue;
                }

                PersistedFloorMapping persistedMapping = new PersistedFloorMapping();
                persistedMapping.FloorName = mapping.FloorName ?? string.Empty;
                persistedMapping.SourceViewIdValue = RevitElementIdUtils.GetElementIdValue(mapping.SourceViewId);
                persistedMapping.SourceSheetIdValue = RevitElementIdUtils.GetElementIdValue(mapping.SourceSheetId);
                persistedMapping.CeilingSourceViewIdValue = RevitElementIdUtils.GetElementIdValue(mapping.CeilingSourceViewId);
                persistedMapping.CeilingSourceSheetIdValue = RevitElementIdUtils.GetElementIdValue(mapping.CeilingSourceSheetId);
                result.Add(persistedMapping);
            }

            return result;
        }

        private List<SheetCreationSessionRow> RestoreSessionRows(
            Document document,
            IList<PersistedSessionRow> persistedRows,
            IList<string> warnings)
        {
            List<SheetCreationSessionRow> result = new List<SheetCreationSessionRow>();
            if (persistedRows == null)
            {
                return result;
            }

            for (int i = 0; i < persistedRows.Count; i++)
            {
                PersistedSessionRow persistedRow = persistedRows[i];
                if (persistedRow == null || IsPersistedSessionRowEmpty(persistedRow))
                {
                    continue;
                }

                SheetCreationSessionRow row = new SheetCreationSessionRow();
                row.PlanKind = persistedRow.PlanKindValue == (int)SheetPlanKind.CeilingPlan
                    ? SheetPlanKind.CeilingPlan
                    : SheetPlanKind.StandardPlan;
                row.FloorName = persistedRow.FloorName ?? string.Empty;
                row.ViewName = persistedRow.ViewName ?? string.Empty;
                row.ViewScaleText = string.IsNullOrWhiteSpace(persistedRow.ViewScaleText) ? "50" : persistedRow.ViewScaleText;
                row.ViewTemplateId = RestoreElementId(document, persistedRow.ViewTemplateIdValue, "view template for saved row " + (i + 1), warnings);
                row.SheetNumber = persistedRow.SheetNumber ?? string.Empty;
                row.SheetName = persistedRow.SheetName ?? string.Empty;
                row.SheetBrowserParameterValue = persistedRow.SheetBrowserParameterValue ?? string.Empty;
                row.SheetBrowserParameterValues = RestoreSessionParameterValues(persistedRow.SheetBrowserParameterValues);
                result.Add(row);
            }

            return result;
        }

        private List<SheetBrowserParameterValueItem> RestoreSessionParameterValues(IList<PersistedSheetBrowserParameterValue> persistedValues)
        {
            List<SheetBrowserParameterValueItem> result = new List<SheetBrowserParameterValueItem>();
            if (persistedValues == null)
            {
                return result;
            }

            for (int i = 0; i < persistedValues.Count; i++)
            {
                PersistedSheetBrowserParameterValue persistedValue = persistedValues[i];
                if (persistedValue == null)
                {
                    continue;
                }

                SheetBrowserParameterValueItem item = new SheetBrowserParameterValueItem();
                item.ParameterId = RestoreParameterId(persistedValue.ParameterIdValue);
                item.ParameterName = persistedValue.ParameterName ?? string.Empty;
                item.Value = persistedValue.Value ?? string.Empty;
                result.Add(item);
            }

            return result;
        }

        private List<PersistedSessionRow> ConvertSessionRows(IList<SheetCreationSessionRow> rows)
        {
            List<PersistedSessionRow> result = new List<PersistedSessionRow>();
            if (rows == null)
            {
                return result;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                SheetCreationSessionRow row = rows[i];
                if (row == null || !IsSessionRowFilled(row))
                {
                    continue;
                }

                PersistedSessionRow persistedRow = new PersistedSessionRow();
                persistedRow.PlanKindValue = (int)row.PlanKind;
                persistedRow.FloorName = row.FloorName ?? string.Empty;
                persistedRow.ViewName = row.ViewName ?? string.Empty;
                persistedRow.ViewScaleText = row.ViewScaleText ?? string.Empty;
                persistedRow.ViewTemplateIdValue = RevitElementIdUtils.GetElementIdValue(row.ViewTemplateId);
                persistedRow.SheetNumber = row.SheetNumber ?? string.Empty;
                persistedRow.SheetName = row.SheetName ?? string.Empty;
                persistedRow.SheetBrowserParameterValue = row.SheetBrowserParameterValue ?? string.Empty;
                persistedRow.SheetBrowserParameterValues = ConvertSessionParameterValues(row.SheetBrowserParameterValues);
                result.Add(persistedRow);
            }

            return result;
        }

        private List<PersistedSheetBrowserParameterValue> ConvertSessionParameterValues(IList<SheetBrowserParameterValueItem> values)
        {
            List<PersistedSheetBrowserParameterValue> result = new List<PersistedSheetBrowserParameterValue>();
            if (values == null)
            {
                return result;
            }

            for (int i = 0; i < values.Count; i++)
            {
                SheetBrowserParameterValueItem value = values[i];
                if (value == null)
                {
                    continue;
                }

                PersistedSheetBrowserParameterValue persistedValue = new PersistedSheetBrowserParameterValue();
                persistedValue.ParameterIdValue = RevitElementIdUtils.GetElementIdValue(value.ParameterId);
                persistedValue.ParameterName = value.ParameterName ?? string.Empty;
                persistedValue.Value = value.Value ?? string.Empty;
                result.Add(persistedValue);
            }

            return result;
        }

        private bool IsSessionRowFilled(SheetCreationSessionRow row)
        {
            if (row == null)
            {
                return false;
            }

            return !string.IsNullOrWhiteSpace(row.ViewName) ||
                   !string.IsNullOrWhiteSpace(row.SheetNumber) ||
                   !string.IsNullOrWhiteSpace(row.SheetName) ||
                   !string.IsNullOrWhiteSpace(row.FloorName) ||
                   !string.IsNullOrWhiteSpace(row.SheetBrowserParameterValue) ||
                   HasSessionParameterValue(row.SheetBrowserParameterValues);
        }

        private bool IsPersistedSessionRowEmpty(PersistedSessionRow row)
        {
            if (row == null)
            {
                return true;
            }

            return string.IsNullOrWhiteSpace(row.ViewName) &&
                   string.IsNullOrWhiteSpace(row.SheetNumber) &&
                   string.IsNullOrWhiteSpace(row.SheetName) &&
                   string.IsNullOrWhiteSpace(row.FloorName) &&
                   string.IsNullOrWhiteSpace(row.SheetBrowserParameterValue) &&
                   !HasPersistedSessionParameterValue(row.SheetBrowserParameterValues);
        }

        private bool HasSessionParameterValue(IList<SheetBrowserParameterValueItem> values)
        {
            if (values == null)
            {
                return false;
            }

            for (int i = 0; i < values.Count; i++)
            {
                SheetBrowserParameterValueItem value = values[i];
                if (value != null && !string.IsNullOrWhiteSpace(value.Value))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasPersistedSessionParameterValue(IList<PersistedSheetBrowserParameterValue> values)
        {
            if (values == null)
            {
                return false;
            }

            for (int i = 0; i < values.Count; i++)
            {
                PersistedSheetBrowserParameterValue value = values[i];
                if (value != null && !string.IsNullOrWhiteSpace(value.Value))
                {
                    return true;
                }
            }

            return false;
        }

        private bool GetNullableBoolValue(bool? value, bool defaultValue)
        {
            return value.HasValue ? value.Value : defaultValue;
        }

        private ElementId RestoreElementId(Document document, long value, string elementDescription, IList<string> warnings)
        {
            if (value <= 0)
            {
                return ElementId.InvalidElementId;
            }

            ElementId elementId = RevitElementIdUtils.CreateElementIdFromLong(value);
            if (document == null || elementId == null || elementId == ElementId.InvalidElementId)
            {
                return ElementId.InvalidElementId;
            }

            Element element = document.GetElement(elementId);
            if (element != null)
            {
                return elementId;
            }

            AddWarning(warnings, "Сохраненный элемент \"" + elementDescription + "\" больше не найден в документе.");
            return ElementId.InvalidElementId;
        }

        private ElementId RestoreParameterId(long? value)
        {
            if (!value.HasValue || value.Value == -1)
            {
                return ElementId.InvalidElementId;
            }

            // ID параметров браузера могут быть отрицательными BuiltInParameter, поэтому нельзя использовать восстановление обычного ElementId.
            ConstructorInfo longConstructor = typeof(ElementId).GetConstructor(new[] { typeof(long) });
            if (longConstructor != null)
            {
                return (ElementId)longConstructor.Invoke(new object[] { value.Value });
            }

            if (value.Value < int.MinValue || value.Value > int.MaxValue)
            {
                return ElementId.InvalidElementId;
            }

            ConstructorInfo intConstructor = typeof(ElementId).GetConstructor(new[] { typeof(int) });
            if (intConstructor != null)
            {
                return (ElementId)intConstructor.Invoke(new object[] { Convert.ToInt32(value.Value) });
            }

            return ElementId.InvalidElementId;
        }

        private void AddWarning(IList<string> warnings, string text)
        {
            if (warnings == null || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            warnings.Add(text);
        }

        private class PersistedSettings
        {
            public int SchemaVersion { get; set; }

            public int StructureModeValue { get; set; }

            public long SourceViewIdValue { get; set; }

            public long SourceSheetIdValue { get; set; }

            public long CeilingSourceViewIdValue { get; set; }

            public long CeilingSourceSheetIdValue { get; set; }

            public long ViewportTypeIdValue { get; set; }

            public long TitleBlockTypeIdValue { get; set; }

            public long? SheetBrowserParameterIdValue { get; set; }

            public string CoordinateUnits { get; set; }

            public double ViewCenterXmm { get; set; }

            public double ViewCenterYmm { get; set; }

            public double ViewTitleXmm { get; set; }

            public double ViewTitleYmm { get; set; }

            public double TitleLineLengthMm { get; set; }

            public bool UsePointSelectionForViewCenter { get; set; }

            public bool UsePointSelectionForViewTitle { get; set; }

            public bool SaveSettings { get; set; }

            public bool CopySheetWithDetailing { get; set; }

            public bool? CopySchedules { get; set; }

            public bool? CopyLegends { get; set; }

            public bool? CopyDraftingViews { get; set; }

            public bool? CopyDetailLines { get; set; }

            public bool? CopyFilledRegions { get; set; }

            public bool? CopyTextNotes { get; set; }

            public bool? CopyGenericAnnotations { get; set; }

            public bool? CopyImages { get; set; }

            public List<PersistedFloorMapping> FloorMappings { get; set; }

            public List<PersistedSessionRow> SessionRows { get; set; }
        }

        private class PersistedFloorMapping
        {
            public string FloorName { get; set; }

            public long SourceViewIdValue { get; set; }

            public long SourceSheetIdValue { get; set; }

            public long CeilingSourceViewIdValue { get; set; }

            public long CeilingSourceSheetIdValue { get; set; }
        }

        private class PersistedSessionRow
        {
            public int PlanKindValue { get; set; }

            public string FloorName { get; set; }

            public string ViewName { get; set; }

            public string ViewScaleText { get; set; }

            public long ViewTemplateIdValue { get; set; }

            public string SheetNumber { get; set; }

            public string SheetName { get; set; }

            public string SheetBrowserParameterValue { get; set; }

            public List<PersistedSheetBrowserParameterValue> SheetBrowserParameterValues { get; set; }
        }

        private class PersistedSheetBrowserParameterValue
        {
            public long? ParameterIdValue { get; set; }

            public string ParameterName { get; set; }

            public string Value { get; set; }
        }
    }
}
