using System;
using System.IO;
using Newtonsoft.Json;
using SAB.DoorWindowExplanations.Models;

namespace SAB.DoorWindowExplanations.Services
{
    public class DoorWindowSettingsStorageService
    {
        private const int CurrentSchemaVersion = 12;
        private readonly string _filePath;

        public DoorWindowSettingsStorageService()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _filePath = Path.Combine(appDataPath, "SAB", "DoorWindowExplanations", "settings.json");
        }

        public DoorWindowViewSettings Load()
        {
            try
            {
                if (!File.Exists(_filePath))
                {
                    return new DoorWindowViewSettings();
                }

                PersistedSettings persisted = JsonConvert.DeserializeObject<PersistedSettings>(File.ReadAllText(_filePath));
                if (persisted == null || persisted.SchemaVersion < 1 ||
                    persisted.SchemaVersion > CurrentSchemaVersion || persisted.Settings == null)
                {
                    return new DoorWindowViewSettings();
                }

                if (persisted.SchemaVersion < 3 &&
                    string.Equals(
                        persisted.Settings.FrontViewNameFormula,
                        "{Категория}_{Позиция}_Спереди",
                        StringComparison.CurrentCultureIgnoreCase))
                {
                    persisted.Settings.FrontViewNameFormula = "{Категория}_{Позиция}_{Сторона}";
                }

                DoorWindowViewSettings defaults = new DoorWindowViewSettings();
                if (persisted.SchemaVersion < 5)
                {
                    persisted.Settings.ViewTitleAnchor = defaults.ViewTitleAnchor;
                    persisted.Settings.ViewTitleOffsetXmm = defaults.ViewTitleOffsetXmm;
                    persisted.Settings.ViewTitleOffsetYmm = defaults.ViewTitleOffsetYmm;
                }

                if (persisted.SchemaVersion < 6)
                {
                    persisted.Settings.ViewportTypeIdValue = -1;
                }

                if (persisted.SchemaVersion < 7)
                {
                    persisted.Settings.CreateFrontDimensions = defaults.CreateFrontDimensions;
                    persisted.Settings.DimensionTypeIdValue = defaults.DimensionTypeIdValue;
                    persisted.Settings.DetailedDimensionOffsetPaperMm = defaults.DetailedDimensionOffsetPaperMm;
                    persisted.Settings.OverallDimensionOffsetPaperMm = defaults.OverallDimensionOffsetPaperMm;
                    persisted.Settings.CreateElementImages = defaults.CreateElementImages;
                    persisted.Settings.CurtainWallInstanceImageParameterName = defaults.CurtainWallInstanceImageParameterName;
                    persisted.Settings.DoorWindowTypeImageParameterName = defaults.DoorWindowTypeImageParameterName;
                    persisted.Settings.ImagePixelSize = defaults.ImagePixelSize;
                }

                if (persisted.SchemaVersion < 8)
                {
                    persisted.Settings.IsolateSelectedElement = defaults.IsolateSelectedElement;
                }

                if (persisted.SchemaVersion < 9)
                {
                    persisted.Settings.WorkflowMode = defaults.WorkflowMode;
                    persisted.Settings.SingleViewKind = defaults.SingleViewKind;
                    persisted.Settings.CurtainWallFrontSideMode = defaults.CurtainWallFrontSideMode;
                    persisted.Settings.HorizontalDimensionSide = defaults.HorizontalDimensionSide;
                    persisted.Settings.VerticalDimensionSide = defaults.VerticalDimensionSide;
                }

                if (persisted.SchemaVersion < 10)
                {
                    persisted.Settings.DimensionTextHeightMm = defaults.DimensionTextHeightMm;
                }

                if (persisted.SchemaVersion < 11)
                {
                    persisted.Settings.SideDetailedDimensionOffsetPaperMm =
                        persisted.Settings.DetailedDimensionOffsetPaperMm;
                    persisted.Settings.SideOverallDimensionOffsetPaperMm =
                        persisted.Settings.OverallDimensionOffsetPaperMm;
                }

                if (persisted.SchemaVersion < 12 || persisted.Settings.ImageHeightPaperMm <= 0.0)
                {
                    persisted.Settings.ImageHeightPaperMm = defaults.ImageHeightPaperMm;
                }

                if (persisted.Settings.TopProjectionDepthMm <= 0.0)
                {
                    persisted.Settings.TopProjectionDepthMm = defaults.TopProjectionDepthMm;
                }

                if (persisted.Settings.FrontProjectionDepthMm <= 0.0)
                {
                    persisted.Settings.FrontProjectionDepthMm = defaults.FrontProjectionDepthMm;
                }

                if (persisted.Settings.SectionProjectionDepthMm <= 0.0)
                {
                    persisted.Settings.SectionProjectionDepthMm = defaults.SectionProjectionDepthMm;
                }

                return persisted.Settings;
            }
            catch
            {
                return new DoorWindowViewSettings();
            }
        }

        public void Save(DoorWindowViewSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            string directory = Path.GetDirectoryName(_filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            PersistedSettings persisted = new PersistedSettings();
            persisted.SchemaVersion = CurrentSchemaVersion;
            persisted.Settings = settings;
            File.WriteAllText(_filePath, JsonConvert.SerializeObject(persisted, Formatting.Indented));
        }

        private class PersistedSettings
        {
            public int SchemaVersion { get; set; }

            public DoorWindowViewSettings Settings { get; set; }
        }
    }
}
