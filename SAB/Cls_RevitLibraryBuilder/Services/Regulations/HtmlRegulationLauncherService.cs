using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RevitLibraryBuilder.Services.Regulations
{
    /// <summary>
    /// Universal service for launching regulation HTML files.
    /// </summary>
    public class HtmlRegulationLauncherService
    {
        public bool TryLaunch(HtmlRegulationLaunchOptions options, out string launchedFilePath, out string errorMessage)
        {
            launchedFilePath = string.Empty;
            errorMessage = string.Empty;

            if (options == null)
            {
                errorMessage = "Параметры запуска HTML не заданы.";
                return false;
            }

            string resolvedDirectory;
            if (!TryResolveDirectory(options.CandidateDirectories, out resolvedDirectory))
            {
                errorMessage = "Не найдена доступная папка с HTML.\nПроверьте пути в настройках команды.";
                return false;
            }

            string startFilePath;
            if (!TryResolveStartFile(resolvedDirectory, options, out startFilePath, out errorMessage))
            {
                return false;
            }

            try
            {
                // Блок отвечает за запуск HTML в браузере по умолчанию Windows.
                ProcessStartInfo processStartInfo = new ProcessStartInfo
                {
                    FileName = startFilePath,
                    UseShellExecute = true
                };

                Process.Start(processStartInfo);
                launchedFilePath = startFilePath;
                return true;
            }
            catch (Exception exception)
            {
                errorMessage = "Не удалось открыть HTML файл.\n" + exception.Message;
                return false;
            }
        }

        private static bool TryResolveDirectory(IList<string> candidateDirectories, out string resolvedDirectory)
        {
            resolvedDirectory = string.Empty;

            if (candidateDirectories == null || candidateDirectories.Count == 0)
            {
                return false;
            }

            for (int index = 0; index < candidateDirectories.Count; index++)
            {
                string candidatePath = candidateDirectories[index];

                if (string.IsNullOrWhiteSpace(candidatePath))
                {
                    continue;
                }

                if (Directory.Exists(candidatePath))
                {
                    resolvedDirectory = candidatePath;
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveStartFile(
            string directoryPath,
            HtmlRegulationLaunchOptions options,
            out string startFilePath,
            out string errorMessage)
        {
            startFilePath = string.Empty;
            errorMessage = string.Empty;

            string safePattern = string.IsNullOrWhiteSpace(options.SearchPattern) ? "*.html" : options.SearchPattern.Trim();
            string safeContains = string.IsNullOrWhiteSpace(options.StartFileNameContains) ? "index" : options.StartFileNameContains.Trim();

            List<string> filePaths = Directory.GetFiles(directoryPath, safePattern, options.GetSearchOption()).ToList();

            if (filePaths.Count == 0)
            {
                errorMessage = "В папке не найдено HTML файлов:\n" + directoryPath;
                return false;
            }

            // Блок отвечает за выбор стартовой страницы.
            // Приоритет: "index.html" -> любой файл с "index" в имени.
            for (int index = 0; index < filePaths.Count; index++)
            {
                string fileName = Path.GetFileName(filePaths[index]);
                if (string.Equals(fileName, "index.html", StringComparison.OrdinalIgnoreCase))
                {
                    startFilePath = filePaths[index];
                    return true;
                }
            }

            List<string> candidates = new List<string>();

            for (int index = 0; index < filePaths.Count; index++)
            {
                string fileName = Path.GetFileName(filePaths[index]);
                if (!string.IsNullOrWhiteSpace(fileName) &&
                    fileName.IndexOf(safeContains, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    candidates.Add(filePaths[index]);
                }
            }

            if (candidates.Count == 0)
            {
                errorMessage = "Не найден стартовый HTML файл, содержащий \"" + safeContains + "\" в названии.\nПапка: " + directoryPath;
                return false;
            }

            candidates.Sort(StringComparer.OrdinalIgnoreCase);
            startFilePath = candidates[0];
            return true;
        }
    }
}
