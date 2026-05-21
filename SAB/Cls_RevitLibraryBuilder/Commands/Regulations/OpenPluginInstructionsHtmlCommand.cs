using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitLibraryBuilder.Services.Regulations;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using Forms = System.Windows.Forms;

namespace RevitLibraryBuilder.Commands.Regulations
{
    [Transaction(TransactionMode.ReadOnly)]
    public class OpenPluginInstructionsHtmlCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            HtmlRegulationLaunchOptions options = BuildLaunchOptions();
            HtmlRegulationLauncherService launcherService = new HtmlRegulationLauncherService();

            string launchedFilePath;
            string errorMessage;

            bool launched = launcherService.TryLaunch(options, out launchedFilePath, out errorMessage);

            if (!launched)
            {
                // DEBUG-блок: показываем фактические пути поиска и текст ошибки первого запуска.
                TaskDialog.Show("Инструкции - DEBUG", BuildDebugMessage(options, errorMessage));

                // Если автопоиск не сработал, даем пользователю выбрать папку с HTML вручную.
                string selectedDirectory;
                if (TrySelectInstructionsDirectory(out selectedDirectory))
                {
                    HtmlRegulationLaunchOptions retryOptions = BuildLaunchOptions();
                    retryOptions.CandidateDirectories.Insert(0, selectedDirectory);

                    bool launchedFromSelectedFolder = launcherService.TryLaunch(
                        retryOptions,
                        out launchedFilePath,
                        out errorMessage);

                    if (launchedFromSelectedFolder)
                    {
                        return Result.Succeeded;
                    }

                    TaskDialog.Show("Инструкции", "Не удалось открыть инструкции из выбранной папки.\n" + errorMessage);
                    message = errorMessage;
                    return Result.Failed;
                }

                TaskDialog.Show("Инструкции", "Открытие инструкций отменено. Папка не выбрана.");
                message = errorMessage;
                return Result.Cancelled;
            }

            return Result.Succeeded;
        }

        private static HtmlRegulationLaunchOptions BuildLaunchOptions()
        {
            HtmlRegulationLaunchOptions options = new HtmlRegulationLaunchOptions
            {
                StartFileNameContains = "index-light",
                SearchPattern = "*.html",
                IncludeSubdirectories = false
            };

            // Блок динамического поиска папки Docs\PluginInstructions рядом с местом сборки.
            AddDynamicCandidateDirectories(options);

            // Блок явного fallback-пути для локальной рабочей среды.
            options.CandidateDirectories.Add(@"C:\Users\VB_User\Desktop\C#\ASvetlakov2000\SAB\Docs\PluginInstructions");
            options.CandidateDirectories.Add(@"C:\Users\VB_User\Desktop\C#\ASvetlakov2000\SAB\SAB\Docs\PluginInstructions");

            // Блок типовых пользовательских путей (для установленных сборок).
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string myDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            AddCandidate(options, Path.Combine(appData, "SAB", "Docs", "PluginInstructions"));
            AddCandidate(options, Path.Combine(localAppData, "SAB", "Docs", "PluginInstructions"));
            AddCandidate(options, Path.Combine(myDocuments, "SAB", "Docs", "PluginInstructions"));

            return options;
        }

        private static void AddDynamicCandidateDirectories(HtmlRegulationLaunchOptions options)
        {
            if (options == null)
            {
                return;
            }

            string assemblyLocation = string.Empty;

            try
            {
                Assembly currentAssembly = typeof(OpenPluginInstructionsHtmlCommand).Assembly;
                assemblyLocation = currentAssembly != null ? currentAssembly.Location : string.Empty;
            }
            catch
            {
                assemblyLocation = string.Empty;
            }

            if (string.IsNullOrWhiteSpace(assemblyLocation))
            {
                return;
            }

            string assemblyDirectoryPath = Path.GetDirectoryName(assemblyLocation);

            if (string.IsNullOrWhiteSpace(assemblyDirectoryPath))
            {
                return;
            }

            DirectoryInfo currentDirectory = new DirectoryInfo(assemblyDirectoryPath);
            int depth = 0;

            while (currentDirectory != null && depth < 10)
            {
                string candidateDirectory = Path.Combine(currentDirectory.FullName, "Docs", "PluginInstructions");
                AddCandidate(options, candidateDirectory);

                // Дополнительный вариант: папка PluginInstructions может лежать рядом без промежуточной Docs.
                string flatCandidateDirectory = Path.Combine(currentDirectory.FullName, "PluginInstructions");
                AddCandidate(options, flatCandidateDirectory);

                currentDirectory = currentDirectory.Parent;
                depth++;
            }
        }

        private static bool TrySelectInstructionsDirectory(out string selectedDirectory)
        {
            selectedDirectory = string.Empty;

            using (Forms.FolderBrowserDialog dialog = new Forms.FolderBrowserDialog())
            {
                dialog.Description = "Выберите папку с HTML-инструкциями (должен быть файл index-light.html).";
                dialog.ShowNewFolderButton = false;

                Forms.DialogResult dialogResult = dialog.ShowDialog();
                if (dialogResult != Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    return false;
                }

                selectedDirectory = dialog.SelectedPath;
                return true;
            }
        }

        private static string BuildDebugMessage(HtmlRegulationLaunchOptions options, string errorMessage)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Этап: запуск инструкции по кнопке \"Инструкции\".");
            builder.AppendLine();
            builder.AppendLine("Текст ошибки:");
            builder.AppendLine(string.IsNullOrWhiteSpace(errorMessage) ? "(пусто)" : errorMessage);
            builder.AppendLine();
            builder.AppendLine("Папки-кандидаты:");

            if (options == null || options.CandidateDirectories == null || options.CandidateDirectories.Count == 0)
            {
                builder.AppendLine("(список пуст)");
            }
            else
            {
                for (int index = 0; index < options.CandidateDirectories.Count; index++)
                {
                    string directory = options.CandidateDirectories[index];
                    bool exists = !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory);
                    builder.AppendLine((index + 1).ToString() + ". " + directory + " | Exists=" + exists);
                }
            }

            return builder.ToString();
        }

        private static void AddCandidate(HtmlRegulationLaunchOptions options, string directoryPath)
        {
            if (options == null || string.IsNullOrWhiteSpace(directoryPath))
            {
                return;
            }

            for (int i = 0; i < options.CandidateDirectories.Count; i++)
            {
                string existing = options.CandidateDirectories[i];

                if (string.Equals(existing, directoryPath, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            options.CandidateDirectories.Add(directoryPath);
        }
    }
}
