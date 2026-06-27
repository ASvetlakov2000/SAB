using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitLibraryBuilder.Services.Regulations;

namespace RevitLibraryBuilder.Commands.Regulations
{
    [Transaction(TransactionMode.ReadOnly)]
    public class OpenNamingStandardsHtmlCommand : IExternalCommand
    {
        private const string StartFileName = "IDEOLOGIST_HTML_Reglamenty.html";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            HtmlRegulationLaunchOptions options = BuildLaunchOptions();
            HtmlRegulationLauncherService launcherService = new HtmlRegulationLauncherService();

            string launchedFilePath;
            string errorMessage;

            bool launched = launcherService.TryLaunch(options, out launchedFilePath, out errorMessage);

            if (!launched)
            {
                TaskDialog.Show("Регламент", errorMessage);
                message = errorMessage;
                return Result.Failed;
            }

            return Result.Succeeded;
        }

        private static HtmlRegulationLaunchOptions BuildLaunchOptions()
        {
            HtmlRegulationLaunchOptions options = new HtmlRegulationLaunchOptions
            {
                // Блок отвечает за имя стартового HTML-файла регламентов.
                StartFileNameContains = StartFileName,
                SearchPattern = "*.html",
                IncludeSubdirectories = false
            };

            // Блок путей-кандидатов. Первым идет актуальная рабочая папка регламентов.
            options.CandidateDirectories.Add(@"Z:\01_IN\SAB\Стандарты");

            // Блок fallback-путей для установленных или старых рабочих окружений.
            options.CandidateDirectories.Add(
                @"G:\Общие диски\Ideologist Архитектурный\Софт\Плагины для Revit\SAB\Инструкции\Стандарты_HTML\Наименования");

            options.CandidateDirectories.Add(
                @"C:\Users\VB_User\Desktop\Стандарт наименований Revit");

            return options;
        }
    }
}
