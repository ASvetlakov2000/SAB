using System;
using System.IO;
using Newtonsoft.Json;
using SAB.InteriorElevations.Models;

namespace SAB.InteriorElevations.Services.Settings
{
    public class PlanCornerMarkAlignmentSettingsStorageService
    {
        private const int CurrentSchemaVersion = 1;
        private readonly string _settingsFilePath;

        public PlanCornerMarkAlignmentSettingsStorageService()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string settingsDirectory = Path.Combine(appDataPath, "SAB", "InteriorElevations");
            _settingsFilePath = Path.Combine(settingsDirectory, "plan-corner-mark-alignment-settings.json");
        }

        public PlanCornerMarkAlignmentSettings LoadSettings()
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

            PersistedSettings persistedSettings = JsonConvert.DeserializeObject<PersistedSettings>(json);
            if (persistedSettings == null)
            {
                return null;
            }

            if (persistedSettings.SchemaVersion != CurrentSchemaVersion)
            {
                return null;
            }

            PlanCornerMarkAlignmentSettings settings = new PlanCornerMarkAlignmentSettings();
            settings.CornerOffsetMm = persistedSettings.CornerOffsetMm;
            settings.LeaderBreakAngle = persistedSettings.LeaderBreakAngle == (int)PlanLeaderBreakAngleType.Degrees135
                ? PlanLeaderBreakAngleType.Degrees135
                : PlanLeaderBreakAngleType.Degrees90;
            return settings;
        }

        public void SaveSettings(PlanCornerMarkAlignmentSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            PersistedSettings persistedSettings = new PersistedSettings();
            persistedSettings.SchemaVersion = CurrentSchemaVersion;
            persistedSettings.CornerOffsetMm = settings.CornerOffsetMm;
            persistedSettings.LeaderBreakAngle = (int)settings.LeaderBreakAngle;

            string settingsDirectory = Path.GetDirectoryName(_settingsFilePath);
            if (!Directory.Exists(settingsDirectory))
            {
                Directory.CreateDirectory(settingsDirectory);
            }

            string json = JsonConvert.SerializeObject(persistedSettings, Formatting.Indented);
            File.WriteAllText(_settingsFilePath, json);
        }

        private class PersistedSettings
        {
            public int SchemaVersion { get; set; }

            public double CornerOffsetMm { get; set; }

            public int LeaderBreakAngle { get; set; }
        }
    }
}
