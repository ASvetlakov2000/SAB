using System;
using System.IO;
using System.Text;

namespace RevitLibraryBuilder.Services.Regulations
{
    public class RegulationsLandingSettings
    {
        public static readonly string DefaultLandingPath = Path.Combine(
            Path.GetDirectoryName(typeof(RegulationsLandingSettings).Assembly.Location),
            "Docs", "PluginInstructions", "SAB_HTML_Instruktsii.html");
        private readonly string settingsPath;

        public RegulationsLandingSettings() : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SAB", "Regulations", "landing-path.txt")) { }

        public RegulationsLandingSettings(string settingsPath)
        {
            this.settingsPath = settingsPath;
        }

        public string Load()
        {
            return File.Exists(settingsPath) ? Normalize(File.ReadAllText(settingsPath)) : DefaultLandingPath;
        }

        public static string Normalize(string path)
        {
            return (path ?? string.Empty).Trim().Trim('"').Trim();
        }

        public static bool IsValid(string path)
        {
            try
            {
                path = Normalize(path);
                string extension = Path.GetExtension(path);
                return Path.IsPathRooted(path) && File.Exists(path) &&
                    (string.Equals(extension, ".html", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(extension, ".htm", StringComparison.OrdinalIgnoreCase));
            }
            catch (ArgumentException) { return false; }
            catch (NotSupportedException) { return false; }
        }

        public void Save(string path)
        {
            path = Normalize(path);
            if (!IsValid(path))
                throw new IOException("Выберите существующий HTML-файл лендинга.");

            string fullSettingsPath = Path.GetFullPath(settingsPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullSettingsPath));
            string temporaryPath = fullSettingsPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, path, new UTF8Encoding(false));
                if (File.Exists(fullSettingsPath))
                    File.Replace(temporaryPath, fullSettingsPath, null);
                else
                    File.Move(temporaryPath, fullSettingsPath);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
    }
}
