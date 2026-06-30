using System;
using System.IO;
using System.Windows;
using Newtonsoft.Json;

namespace SAB.UI
{
    public static class WindowSizeSettingsService
    {
        private const double MinimumSavedWindowSize = 100.0;

        public static void Apply(Window window, string windowKey)
        {
            if (window == null || string.IsNullOrWhiteSpace(windowKey))
            {
                return;
            }

            Restore(window, windowKey);
            SabWindowBehaviorService.Apply(window);
            window.Closing += delegate
            {
                Save(window, windowKey);
            };
        }

        private static void Restore(Window window, string windowKey)
        {
            try
            {
                string settingsFilePath = GetSettingsFilePath(windowKey);
                if (!File.Exists(settingsFilePath))
                {
                    return;
                }

                string json = File.ReadAllText(settingsFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return;
                }

                WindowSizeSettings settings = JsonConvert.DeserializeObject<WindowSizeSettings>(json);
                if (settings == null)
                {
                    return;
                }

                if (IsValidSize(settings.Width) && IsValidSize(settings.Height))
                {
                    window.Width = Clamp(settings.Width, GetMinimumWidth(window), SystemParameters.WorkArea.Width);
                    window.Height = Clamp(settings.Height, GetMinimumHeight(window), SystemParameters.WorkArea.Height);
                }
            }
            catch
            {
                // Saved UI preferences must not block plugin startup.
            }
        }

        private static void Save(Window window, string windowKey)
        {
            try
            {
                double width = window.RestoreBounds.Width > 0 ? window.RestoreBounds.Width : window.ActualWidth;
                double height = window.RestoreBounds.Height > 0 ? window.RestoreBounds.Height : window.ActualHeight;

                if (!IsValidSize(width) || !IsValidSize(height))
                {
                    return;
                }

                WindowSizeSettings settings = new WindowSizeSettings
                {
                    Width = Math.Max(width, GetMinimumWidth(window)),
                    Height = Math.Max(height, GetMinimumHeight(window))
                };

                string settingsFilePath = GetSettingsFilePath(windowKey);
                string settingsDirectory = Path.GetDirectoryName(settingsFilePath);
                if (!Directory.Exists(settingsDirectory))
                {
                    Directory.CreateDirectory(settingsDirectory);
                }

                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(settingsFilePath, json);
            }
            catch
            {
                // Saving window size is a convenience feature and should never interrupt work.
            }
        }

        private static string GetSettingsFilePath(string windowKey)
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string settingsDirectory = Path.Combine(appDataPath, "SAB", "WindowSizes");
            string safeFileName = MakeSafeFileName(windowKey) + ".json";
            return Path.Combine(settingsDirectory, safeFileName);
        }

        private static string MakeSafeFileName(string value)
        {
            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            string result = value.Trim();

            for (int i = 0; i < invalidCharacters.Length; i++)
            {
                result = result.Replace(invalidCharacters[i], '_');
            }

            return result.Replace(' ', '_');
        }

        private static double GetMinimumWidth(Window window)
        {
            if (IsValidSize(window.MinWidth))
            {
                return Math.Max(window.MinWidth, MinimumSavedWindowSize);
            }

            return MinimumSavedWindowSize;
        }

        private static double GetMinimumHeight(Window window)
        {
            if (IsValidSize(window.MinHeight))
            {
                return Math.Max(window.MinHeight, MinimumSavedWindowSize);
            }

            return MinimumSavedWindowSize;
        }

        private static bool IsValidSize(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value >= MinimumSavedWindowSize;
        }

        private static double Clamp(double value, double minimum, double maximum)
        {
            if (!IsValidSize(maximum) || maximum < minimum)
            {
                return Math.Max(value, minimum);
            }

            if (value < minimum)
            {
                return minimum;
            }

            if (value > maximum)
            {
                return maximum;
            }

            return value;
        }

        private class WindowSizeSettings
        {
            public double Width { get; set; }

            public double Height { get; set; }
        }
    }
}
