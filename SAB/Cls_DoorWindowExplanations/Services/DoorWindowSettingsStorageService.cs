using System;
using System.IO;
using Newtonsoft.Json;
using SAB.DoorWindowExplanations.Models;

namespace SAB.DoorWindowExplanations.Services
{
    public class DoorWindowSettingsStorageService
    {
        private const int CurrentSchemaVersion = 5;
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
