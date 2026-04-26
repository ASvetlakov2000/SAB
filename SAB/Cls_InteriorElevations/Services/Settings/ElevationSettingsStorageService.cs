using System;
using System.IO;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Settings
{
    public class ElevationSettingsStorageService
    {
        private const int CurrentSchemaVersion = 3;
        private readonly string _settingsFilePath;

        public ElevationSettingsStorageService()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string settingsDirectory = Path.Combine(appDataPath, "SAB", "InteriorElevations");
            _settingsFilePath = Path.Combine(settingsDirectory, "elevation-settings.json");
        }

        public ElevationSettings LoadSettings()
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

            PersistedElevationSettings persistedSettings = JsonConvert.DeserializeObject<PersistedElevationSettings>(json);
            if (persistedSettings == null)
            {
                return null;
            }

            if (persistedSettings.SchemaVersion != CurrentSchemaVersion)
            {
                return null;
            }

            return ConvertToElevationSettings(persistedSettings);
        }

        public void SaveSettings(ElevationSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            PersistedElevationSettings persistedSettings = ConvertFromElevationSettings(settings);

            string settingsDirectory = Path.GetDirectoryName(_settingsFilePath);
            if (!Directory.Exists(settingsDirectory))
            {
                Directory.CreateDirectory(settingsDirectory);
            }

            string json = JsonConvert.SerializeObject(persistedSettings, Formatting.Indented);
            File.WriteAllText(_settingsFilePath, json);
        }

        private ElevationSettings ConvertToElevationSettings(PersistedElevationSettings persistedSettings)
        {
            ElevationSettings settings = new ElevationSettings();
            settings.ViewTemplateId = RevitElementIdUtils.CreateElementIdFromLong(persistedSettings.ViewTemplateIdValue);
            settings.ElevationViewFamilyTypeId = RevitElementIdUtils.CreateElementIdFromLong(persistedSettings.ElevationViewFamilyTypeIdValue);
            settings.ViewScale = persistedSettings.ViewScale;

            settings.TopOffsetMm = persistedSettings.TopOffsetMm;
            settings.BottomOffsetMm = persistedSettings.BottomOffsetMm;
            settings.LeftOffsetMm = persistedSettings.LeftOffsetMm;
            settings.RightOffsetMm = persistedSettings.RightOffsetMm;
            settings.ViewDepthMm = persistedSettings.ViewDepthMm;
            settings.MarkerOffsetMm = persistedSettings.MarkerOffsetMm;

            settings.CreateSheet = persistedSettings.CreateSheet;
            settings.TitleBlockTypeId = RevitElementIdUtils.CreateElementIdFromLong(persistedSettings.TitleBlockTypeIdValue);
            settings.PlanCornerMarkTypeId = RevitElementIdUtils.CreateElementIdFromLong(persistedSettings.PlanCornerMarkTypeIdValue);
            settings.SheetCornerMarkTypeId = RevitElementIdUtils.CreateElementIdFromLong(persistedSettings.SheetCornerMarkTypeIdValue);
            settings.SheetFormatAValue = persistedSettings.HasSheetFormatAValue
                ? (int?)persistedSettings.SheetFormatAValue
                : null;

            settings.SheetLayoutSettings = new SheetLayoutSettings();
            settings.SheetLayoutSettings.ColumnsCount = persistedSettings.ColumnsCount;
            settings.SheetLayoutSettings.StartXmm = persistedSettings.StartXmm;
            settings.SheetLayoutSettings.StartYmm = persistedSettings.StartYmm;
            settings.SheetLayoutSettings.StepXmm = persistedSettings.StepXmm;
            settings.SheetLayoutSettings.StepYmm = persistedSettings.StepYmm;

            return settings;
        }

        private PersistedElevationSettings ConvertFromElevationSettings(ElevationSettings settings)
        {
            PersistedElevationSettings persistedSettings = new PersistedElevationSettings();
            persistedSettings.SchemaVersion = CurrentSchemaVersion;

            persistedSettings.ViewTemplateIdValue = RevitElementIdUtils.GetElementIdValue(settings.ViewTemplateId);
            persistedSettings.ElevationViewFamilyTypeIdValue = RevitElementIdUtils.GetElementIdValue(settings.ElevationViewFamilyTypeId);
            persistedSettings.ViewScale = settings.ViewScale;

            persistedSettings.TopOffsetMm = settings.TopOffsetMm;
            persistedSettings.BottomOffsetMm = settings.BottomOffsetMm;
            persistedSettings.LeftOffsetMm = settings.LeftOffsetMm;
            persistedSettings.RightOffsetMm = settings.RightOffsetMm;
            persistedSettings.ViewDepthMm = settings.ViewDepthMm;
            persistedSettings.MarkerOffsetMm = settings.MarkerOffsetMm;

            persistedSettings.CreateSheet = settings.CreateSheet;
            persistedSettings.TitleBlockTypeIdValue = RevitElementIdUtils.GetElementIdValue(settings.TitleBlockTypeId);
            persistedSettings.PlanCornerMarkTypeIdValue = RevitElementIdUtils.GetElementIdValue(settings.PlanCornerMarkTypeId);
            persistedSettings.SheetCornerMarkTypeIdValue = RevitElementIdUtils.GetElementIdValue(settings.SheetCornerMarkTypeId);
            persistedSettings.HasSheetFormatAValue = settings.SheetFormatAValue.HasValue;
            persistedSettings.SheetFormatAValue = settings.SheetFormatAValue.HasValue ? settings.SheetFormatAValue.Value : 0;

            SheetLayoutSettings sheetLayoutSettings = settings.SheetLayoutSettings ?? new SheetLayoutSettings();
            persistedSettings.ColumnsCount = sheetLayoutSettings.ColumnsCount;
            persistedSettings.StartXmm = sheetLayoutSettings.StartXmm;
            persistedSettings.StartYmm = sheetLayoutSettings.StartYmm;
            persistedSettings.StepXmm = sheetLayoutSettings.StepXmm;
            persistedSettings.StepYmm = sheetLayoutSettings.StepYmm;

            return persistedSettings;
        }

        private class PersistedElevationSettings
        {
            public int SchemaVersion { get; set; }

            public long ViewTemplateIdValue { get; set; }

            public long ElevationViewFamilyTypeIdValue { get; set; }

            public int ViewScale { get; set; }

            public double TopOffsetMm { get; set; }

            public double BottomOffsetMm { get; set; }

            public double LeftOffsetMm { get; set; }

            public double RightOffsetMm { get; set; }

            public double ViewDepthMm { get; set; }

            public double MarkerOffsetMm { get; set; }

            public bool CreateSheet { get; set; }

            public long TitleBlockTypeIdValue { get; set; }

            public long PlanCornerMarkTypeIdValue { get; set; }

            public long SheetCornerMarkTypeIdValue { get; set; }

            public bool HasSheetFormatAValue { get; set; }

            public int SheetFormatAValue { get; set; }

            public int ColumnsCount { get; set; }

            public double StartXmm { get; set; }

            public double StartYmm { get; set; }

            public double StepXmm { get; set; }

            public double StepYmm { get; set; }
        }
    }
}
