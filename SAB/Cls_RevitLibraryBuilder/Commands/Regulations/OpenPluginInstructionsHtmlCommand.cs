using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitLibraryBuilder.Services.Regulations;
using System;
using System.IO;
using System.Reflection;

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
                TaskDialog.Show("Инструкции", errorMessage);
                message = errorMessage;
                return Result.Failed;
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

                currentDirectory = currentDirectory.Parent;
                depth++;
            }
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
