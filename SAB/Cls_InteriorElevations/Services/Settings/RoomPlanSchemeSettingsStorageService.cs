using System;
using System.IO;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Settings
{
    /// <summary>
    /// Сервис сохранения настроек окна создания план-схем помещений.
    /// </summary>
    public class RoomPlanSchemeSettingsStorageService
    {
        private const int CurrentSchemaVersion = 1;
        private readonly string _settingsFilePath;

        public RoomPlanSchemeSettingsStorageService()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string settingsDirectory = Path.Combine(appDataPath, "SAB", "InteriorElevations");
            _settingsFilePath = Path.Combine(settingsDirectory, "room-plan-scheme-settings.json");
        }

        public RoomPlanSchemeSettings LoadSettings()
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

            PersistedRoomPlanSchemeSettings persisted = JsonConvert.DeserializeObject<PersistedRoomPlanSchemeSettings>(json);
            if (persisted == null || persisted.SchemaVersion != CurrentSchemaVersion)
            {
                return null;
            }

            RoomPlanSchemeSettings settings = new RoomPlanSchemeSettings();
            settings.NamePart1 = persisted.NamePart1 ?? string.Empty;
            settings.NamePart2 = persisted.NamePart2 ?? string.Empty;
            settings.NamePart3 = persisted.NamePart3 ?? string.Empty;
            settings.CropOffsetMm = persisted.CropOffsetMm;
            settings.ViewTemplateId = RevitElementIdUtils.CreateElementIdFromLong(persisted.ViewTemplateIdValue);
            return settings;
        }

        public void SaveSettings(RoomPlanSchemeSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            PersistedRoomPlanSchemeSettings persisted = new PersistedRoomPlanSchemeSettings();
            persisted.SchemaVersion = CurrentSchemaVersion;
            persisted.NamePart1 = settings.NamePart1 ?? string.Empty;
            persisted.NamePart2 = settings.NamePart2 ?? string.Empty;
            persisted.NamePart3 = settings.NamePart3 ?? string.Empty;
            persisted.CropOffsetMm = settings.CropOffsetMm;
            persisted.ViewTemplateIdValue = RevitElementIdUtils.GetElementIdValue(settings.ViewTemplateId);

            string directory = Path.GetDirectoryName(_settingsFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonConvert.SerializeObject(persisted, Formatting.Indented);
            File.WriteAllText(_settingsFilePath, json);
        }

        private class PersistedRoomPlanSchemeSettings
        {
            public int SchemaVersion { get; set; }

            public string NamePart1 { get; set; }

            public string NamePart2 { get; set; }

            public string NamePart3 { get; set; }

            public long ViewTemplateIdValue { get; set; }

            public double CropOffsetMm { get; set; }
        }
    }
}

