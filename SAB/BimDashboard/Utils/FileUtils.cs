using System;
using System.IO;
using System.Reflection;

namespace SAB.BimDashboard.Utils
{
    /// <summary>
    /// Утилиты для безопасной работы с путями и файлами dashboard.
    /// </summary>
    public static class FileUtils
    {
        public static string GetAssemblyDirectory()
        {
            string assemblyPath = Assembly.GetExecutingAssembly().Location;
            string directory = Path.GetDirectoryName(assemblyPath);

            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("Не удалось определить каталог сборки плагина.");
            }

            return directory;
        }

        public static string GetTemplateFilePath()
        {
            string templatePath = Path.Combine(GetAssemblyDirectory(), "BimDashboard", "Templates", "dashboard_template.html");

            if (!File.Exists(templatePath))
            {
                throw new FileNotFoundException("HTML шаблон dashboard не найден.", templatePath);
            }

            return templatePath;
        }

        public static string GetWwwRootDirectory()
        {
            string wwwrootPath = Path.Combine(GetAssemblyDirectory(), "BimDashboard", "wwwroot");

            if (!Directory.Exists(wwwrootPath))
            {
                throw new DirectoryNotFoundException("Папка ассетов dashboard не найдена: " + wwwrootPath);
            }

            return wwwrootPath;
        }

        public static string GetTempDashboardDirectory()
        {
            string tempRoot = Path.GetTempPath();
            string dashboardDirectory = Path.Combine(tempRoot, "BimDashboard");

            if (!Directory.Exists(dashboardDirectory))
            {
                Directory.CreateDirectory(dashboardDirectory);
            }

            return dashboardDirectory;
        }

        public static void EnsureFileExists(string filePath, string description)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException(description + " не найден.", filePath);
            }
        }

        public static void CopyWithOverwrite(string sourcePath, string targetPath)
        {
            EnsureFileExists(sourcePath, "Файл ассета dashboard");
            File.Copy(sourcePath, targetPath, true);
        }
    }
}
