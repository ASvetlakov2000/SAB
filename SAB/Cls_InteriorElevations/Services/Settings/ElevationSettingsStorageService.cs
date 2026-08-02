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
        private const int CurrentSchemaVersion = 5;
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

            if (persistedSettings.SchemaVersion < 4 || persistedSettings.SchemaVersion > CurrentSchemaVersion)
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
            if (persistedSettings.SchemaVersion >= 5)
            {
                settings.ElevationNamePart1 = persistedSettings.ElevationNamePart1 ?? string.Empty;
                settings.ElevationNamePart2 = persistedSettings.ElevationNamePart2 ?? string.Empty;
                settings.ElevationNamePart3 = persistedSettings.ElevationNamePart3 ?? string.Empty;
            }
            else
            {
                settings.ElevationNamePart1 = "ELV_r";
                settings.ElevationNamePart2 = "{Номер помещения}_{Имя помещения}";
                settings.ElevationNamePart3 = "_Elev_{Начальный угол}-{Конечный угол}";
            }

            settings.CreateSheet = persistedSettings.CreateSheet;
            settings.MultipleRoomsOnSheet = persistedSettings.MultipleRoomsOnSheet;
            settings.TitleBlockTypeId = RevitElementIdUtils.CreateElementIdFromLong(persistedSettings.TitleBlockTypeIdValue);
            settings.ViewportTypeId = RevitElementIdUtils.CreateElementIdFromLong(persistedSettings.ViewportTypeIdValue);
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
            settings.SheetLayoutSettings.ViewTitleAnchor = persistedSettings.SchemaVersion >= 5
                ? persistedSettings.ViewTitleAnchor
                : ViewTitleAnchor.BottomLeft;
            settings.SheetLayoutSettings.ViewTitleOffsetXmm = persistedSettings.SchemaVersion >= 5
                ? persistedSettings.ViewTitleOffsetXmm
                : 0.0;
            settings.SheetLayoutSettings.ViewTitleOffsetYmm = persistedSettings.SchemaVersion >= 5
                ? persistedSettings.ViewTitleOffsetYmm
                : -5.0;

            if (persistedSettings.SchemaVersion >= 5)
            {
                settings.SheetNamePart1 = persistedSettings.SheetNamePart1 ?? string.Empty;
                settings.SheetNamePart2 = persistedSettings.SheetNamePart2 ?? string.Empty;
                settings.SheetNamePart3 = persistedSettings.SheetNamePart3 ?? string.Empty;
            }
            else
            {
                settings.SheetNamePart1 = "Развертки стен пом. ";
                settings.SheetNamePart2 = "{Помещения}";
                settings.SheetNamePart3 = string.Empty;
            }

            settings.RoomPlanNamePart1 = persistedSettings.RoomPlanNamePart1 ?? string.Empty;
            settings.RoomPlanNamePart2 = persistedSettings.RoomPlanNamePart2 ?? string.Empty;
            settings.RoomPlanNamePart3 = persistedSettings.RoomPlanNamePart3 ?? string.Empty;
            settings.RoomPlanViewTemplateId = RevitElementIdUtils.CreateElementIdFromLong(persistedSettings.RoomPlanViewTemplateIdValue);
            settings.RoomPlanRoomTagTypeId = persistedSettings.RoomPlanRoomTagTypeIdValue > 0
                ? RevitElementIdUtils.CreateElementIdFromLong(persistedSettings.RoomPlanRoomTagTypeIdValue)
                : ElementId.InvalidElementId;
            settings.RoomPlanViewScale = persistedSettings.RoomPlanViewScale;
            settings.RoomPlanCropOffsetMm = persistedSettings.RoomPlanCropOffsetMm;

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
            persistedSettings.ElevationNamePart1 = settings.ElevationNamePart1 ?? string.Empty;
            persistedSettings.ElevationNamePart2 = settings.ElevationNamePart2 ?? string.Empty;
            persistedSettings.ElevationNamePart3 = settings.ElevationNamePart3 ?? string.Empty;

            persistedSettings.CreateSheet = settings.CreateSheet;
            persistedSettings.MultipleRoomsOnSheet = settings.MultipleRoomsOnSheet;
            persistedSettings.TitleBlockTypeIdValue = RevitElementIdUtils.GetElementIdValue(settings.TitleBlockTypeId);
            persistedSettings.ViewportTypeIdValue = RevitElementIdUtils.GetElementIdValue(settings.ViewportTypeId);
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
            persistedSettings.ViewTitleAnchor = sheetLayoutSettings.ViewTitleAnchor;
            persistedSettings.ViewTitleOffsetXmm = sheetLayoutSettings.ViewTitleOffsetXmm;
            persistedSettings.ViewTitleOffsetYmm = sheetLayoutSettings.ViewTitleOffsetYmm;

            persistedSettings.SheetNamePart1 = settings.SheetNamePart1 ?? string.Empty;
            persistedSettings.SheetNamePart2 = settings.SheetNamePart2 ?? string.Empty;
            persistedSettings.SheetNamePart3 = settings.SheetNamePart3 ?? string.Empty;

            persistedSettings.RoomPlanNamePart1 = settings.RoomPlanNamePart1 ?? string.Empty;
            persistedSettings.RoomPlanNamePart2 = settings.RoomPlanNamePart2 ?? string.Empty;
            persistedSettings.RoomPlanNamePart3 = settings.RoomPlanNamePart3 ?? string.Empty;
            persistedSettings.RoomPlanViewTemplateIdValue = RevitElementIdUtils.GetElementIdValue(settings.RoomPlanViewTemplateId);
            persistedSettings.RoomPlanRoomTagTypeIdValue = RevitElementIdUtils.GetElementIdValue(settings.RoomPlanRoomTagTypeId);
            persistedSettings.RoomPlanViewScale = settings.RoomPlanViewScale;
            persistedSettings.RoomPlanCropOffsetMm = settings.RoomPlanCropOffsetMm;

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

            public string ElevationNamePart1 { get; set; }

            public string ElevationNamePart2 { get; set; }

            public string ElevationNamePart3 { get; set; }

            public bool CreateSheet { get; set; }

            public bool MultipleRoomsOnSheet { get; set; }

            public long TitleBlockTypeIdValue { get; set; }

            public long ViewportTypeIdValue { get; set; }

            public long PlanCornerMarkTypeIdValue { get; set; }

            public long SheetCornerMarkTypeIdValue { get; set; }

            public bool HasSheetFormatAValue { get; set; }

            public int SheetFormatAValue { get; set; }

            public int ColumnsCount { get; set; }

            public double StartXmm { get; set; }

            public double StartYmm { get; set; }

            public double StepXmm { get; set; }

            public double StepYmm { get; set; }

            public ViewTitleAnchor ViewTitleAnchor { get; set; }

            public double ViewTitleOffsetXmm { get; set; }

            public double ViewTitleOffsetYmm { get; set; }

            public string SheetNamePart1 { get; set; }

            public string SheetNamePart2 { get; set; }

            public string SheetNamePart3 { get; set; }

            public string RoomPlanNamePart1 { get; set; }

            public string RoomPlanNamePart2 { get; set; }

            public string RoomPlanNamePart3 { get; set; }

            public long RoomPlanViewTemplateIdValue { get; set; }

            public long RoomPlanRoomTagTypeIdValue { get; set; }

            public int RoomPlanViewScale { get; set; }

            public double RoomPlanCropOffsetMm { get; set; }
        }
    }
}
