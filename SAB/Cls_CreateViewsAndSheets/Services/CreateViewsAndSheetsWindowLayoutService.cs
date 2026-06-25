using System;
using System.IO;
using Newtonsoft.Json;
using SAB.CreateViewsAndSheets.Models;

namespace SAB.CreateViewsAndSheets.Services
{
    public class CreateViewsAndSheetsWindowLayoutService
    {
        private readonly string _layoutFilePath;

        public CreateViewsAndSheetsWindowLayoutService()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string settingsDirectory = Path.Combine(appDataPath, "SAB", "CreateViewsAndSheets");
            _layoutFilePath = Path.Combine(settingsDirectory, "window-layout.json");
        }

        public CreateViewsAndSheetsWindowLayoutSettings Load()
        {
            try
            {
                if (!File.Exists(_layoutFilePath))
                {
                    return null;
                }

                string json = File.ReadAllText(_layoutFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return null;
                }

                return JsonConvert.DeserializeObject<CreateViewsAndSheetsWindowLayoutSettings>(json);
            }
            catch
            {
                return null;
            }
        }

        public void Save(CreateViewsAndSheetsWindowLayoutSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            try
            {
                string settingsDirectory = Path.GetDirectoryName(_layoutFilePath);
                if (!Directory.Exists(settingsDirectory))
                {
                    Directory.CreateDirectory(settingsDirectory);
                }

                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(_layoutFilePath, json);
            }
            catch
            {
                // Ошибка сохранения пользовательской разметки окна не должна мешать работе команды.
            }
        }
    }
}
