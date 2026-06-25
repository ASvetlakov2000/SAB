using System;
using System.Collections.Generic;
using System.IO;
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
            settings.SourceViewId = RestoreElementId(document, persisted.SourceViewIdValue, "эталонный вид", warnings);
            settings.SourceSheetId = RestoreElementId(document, persisted.SourceSheetIdValue, "эталонный лист", warnings);
            settings.ViewportTypeId = RestoreElementId(document, persisted.ViewportTypeIdValue, "тип Viewport", warnings);
            settings.TitleBlockTypeId = RestoreElementId(document, persisted.TitleBlockTypeIdValue, "тип основной надписи", warnings);

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

            return settings;
        }

        private PersistedSettings ConvertFromSettings(CreateViewsAndSheetsSettings settings)
        {
            PlacementSettings placement = settings.Placement ?? new PlacementSettings();

            PersistedSettings persisted = new PersistedSettings();
            persisted.SchemaVersion = CurrentSchemaVersion;
            persisted.SourceViewIdValue = RevitElementIdUtils.GetElementIdValue(settings.SourceViewId);
            persisted.SourceSheetIdValue = RevitElementIdUtils.GetElementIdValue(settings.SourceSheetId);
            persisted.ViewportTypeIdValue = RevitElementIdUtils.GetElementIdValue(settings.ViewportTypeId);
            persisted.TitleBlockTypeIdValue = RevitElementIdUtils.GetElementIdValue(settings.TitleBlockTypeId);
            persisted.CoordinateUnits = placement.CoordinateUnits;
            persisted.ViewCenterXmm = placement.ViewCenterXmm;
            persisted.ViewCenterYmm = placement.ViewCenterYmm;
            persisted.ViewTitleXmm = placement.ViewTitleXmm;
            persisted.ViewTitleYmm = placement.ViewTitleYmm;
            persisted.TitleLineLengthMm = placement.TitleLineLengthMm;
            persisted.UsePointSelectionForViewCenter = placement.UsePointSelectionForViewCenter;
            persisted.UsePointSelectionForViewTitle = placement.UsePointSelectionForViewTitle;
            persisted.SaveSettings = placement.SaveSettings;
            return persisted;
        }

        private ElementId RestoreElementId(Document document, long value, string elementDescription, IList<string> warnings)
        {
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

            public long SourceViewIdValue { get; set; }

            public long SourceSheetIdValue { get; set; }

            public long ViewportTypeIdValue { get; set; }

            public long TitleBlockTypeIdValue { get; set; }

            public string CoordinateUnits { get; set; }

            public double ViewCenterXmm { get; set; }

            public double ViewCenterYmm { get; set; }

            public double ViewTitleXmm { get; set; }

            public double ViewTitleYmm { get; set; }

            public double TitleLineLengthMm { get; set; }

            public bool UsePointSelectionForViewCenter { get; set; }

            public bool UsePointSelectionForViewTitle { get; set; }

            public bool SaveSettings { get; set; }
        }
    }
}
