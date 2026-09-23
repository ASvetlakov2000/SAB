using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace RevitLibraryBuilder.Services.Regulations
{
    /// <summary>
    /// Универсальный сервис запуска HTML-инструкций и HTML-регламентов.
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

            string startFilePath;
            if (!TryResolveStartFileFromCandidates(options, out startFilePath, out errorMessage))
            {
                return false;
            }

            try
            {
                // Блок запуска HTML-файла через приложение по умолчанию в Windows.
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
                errorMessage = "Не удалось открыть HTML-файл.\n" + exception.Message;
                return false;
            }
        }

        private static bool TryResolveStartFileFromCandidates(
            HtmlRegulationLaunchOptions options,
            out string startFilePath,
            out string errorMessage)
        {
            startFilePath = string.Empty;
            errorMessage = string.Empty;

            if (options.CandidateDirectories == null || options.CandidateDirectories.Count == 0)
            {
                errorMessage = "Не задан список папок для поиска HTML.";
                return false;
            }

            bool hasExistingDirectory = false;
            List<string> checkedDirectories = new List<string>();

            for (int index = 0; index < options.CandidateDirectories.Count; index++)
            {
                string directoryPath = options.CandidateDirectories[index];

                if (string.IsNullOrWhiteSpace(directoryPath))
                {
                    continue;
                }

                if (!Directory.Exists(directoryPath))
                {
                    continue;
                }

                hasExistingDirectory = true;

                string directoryErrorMessage;
                if (TryResolveStartFile(directoryPath, options, out startFilePath, out directoryErrorMessage))
                {
                    return true;
                }

                checkedDirectories.Add(directoryPath + " -> " + directoryErrorMessage);
            }

            if (!hasExistingDirectory)
            {
                errorMessage = "Не найдена доступная папка с HTML.\nПроверьте пути в настройках команды.";
                return false;
            }

            errorMessage = BuildMissingStartFileMessage(options, checkedDirectories);
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
            string safeContains = string.IsNullOrWhiteSpace(options.StartFileNameContains) ? "IDEOLOGIST_HTML" : options.StartFileNameContains.Trim();

            string[] filePaths = Directory.GetFiles(directoryPath, safePattern, options.GetSearchOption());

            if (filePaths == null || filePaths.Length == 0)
            {
                errorMessage = "HTML-файлы не найдены.";
                return false;
            }

            // Блок точного поиска: если передано полное имя файла, оно имеет максимальный приоритет.
            for (int index = 0; index < filePaths.Length; index++)
            {
                string fileName = Path.GetFileName(filePaths[index]);
                if (string.Equals(fileName, safeContains, StringComparison.OrdinalIgnoreCase))
                {
                    startFilePath = filePaths[index];
                    return true;
                }
            }

            List<string> candidates = new List<string>();

            for (int index = 0; index < filePaths.Length; index++)
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
                errorMessage = "не найден стартовый файл с маркером \"" + safeContains + "\".";
                return false;
            }

            candidates.Sort(StringComparer.OrdinalIgnoreCase);
            startFilePath = candidates[0];
            return true;
        }

        private static string BuildMissingStartFileMessage(HtmlRegulationLaunchOptions options, IList<string> checkedDirectories)
        {
            string safeContains = options != null && !string.IsNullOrWhiteSpace(options.StartFileNameContains)
                ? options.StartFileNameContains.Trim()
                : "IDEOLOGIST_HTML";

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.AppendLine("Не найден стартовый HTML-файл.");
            builder.AppendLine("Ожидаемый файл или маркер: " + safeContains);
            builder.AppendLine();
            builder.AppendLine("Проверенные папки:");

            if (checkedDirectories == null || checkedDirectories.Count == 0)
            {
                builder.AppendLine("(нет проверенных папок)");
            }
            else
            {
                for (int index = 0; index < checkedDirectories.Count; index++)
                {
                    builder.AppendLine((index + 1).ToString() + ". " + checkedDirectories[index]);
                }
            }

            return builder.ToString().Trim();
        }
    }
}
