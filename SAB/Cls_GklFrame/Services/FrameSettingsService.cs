using System;
using System.IO;
using Newtonsoft.Json;
using SAB.GklFrame.Models;

namespace SAB.GklFrame.Services
{
    public sealed class FrameSettingsService
    {
        private readonly string _settingsPath;

        public FrameSettingsService()
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SAB",
                "GklFrame");
            _settingsPath = Path.Combine(directory, "settings.json");
        }

        public FrameModuleSettings Load()
        {
            try
            {
                if (File.Exists(_settingsPath))
                {
                    FrameModuleSettings settings = JsonConvert.DeserializeObject<FrameModuleSettings>(
                        File.ReadAllText(_settingsPath));
                    if (settings != null)
                    {
                        EnsureDefaults(settings);
                        return settings;
                    }
                }
            }
            catch
            {
                // Повреждённые пользовательские настройки не должны блокировать команду.
            }

            FrameModuleSettings fallback = new FrameModuleSettings();
            EnsureDefaults(fallback);
            return fallback;
        }

        public void Save(FrameModuleSettings settings)
        {
            string directory = Path.GetDirectoryName(_settingsPath);
            Directory.CreateDirectory(directory);
            File.WriteAllText(_settingsPath, JsonConvert.SerializeObject(settings, Formatting.Indented));
        }

        private static void EnsureDefaults(FrameModuleSettings settings)
        {
            if (settings.StudSpacingMm <= 0.0)
            {
                settings.StudSpacingMm = FrameModuleConstants.DefaultStudSpacingMm;
            }

            if (settings.CwPurchaseLengthMm <= 0.0)
            {
                settings.CwPurchaseLengthMm = FrameModuleConstants.DefaultPurchaseLengthMm;
            }

            if (settings.UwPurchaseLengthMm <= 0.0)
            {
                settings.UwPurchaseLengthMm = FrameModuleConstants.DefaultPurchaseLengthMm;
            }

            if (string.IsNullOrWhiteSpace(settings.CurtainWallTypeName))
            {
                settings.CurtainWallTypeName = FrameModuleConstants.DefaultCurtainWallTypeName;
            }

            if (string.IsNullOrWhiteSpace(settings.ExportDirectory))
            {
                settings.ExportDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "SAB",
                    "GklFrame");
            }
        }
    }
}
