using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitLibraryBuilder.Services.Regulations;

namespace RevitLibraryBuilder.Commands.Regulations
{
    [Transaction(TransactionMode.ReadOnly)]
    public class OpenNamingStandardsHtmlCommand : IExternalCommand
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
                // Блок отвечает за имя стартового файла.
                // При необходимости можно заменить "index" на другой маркер.
                StartFileNameContains = "index",

                // Блок отвечает за маску файлов.
                // При необходимости можно расширить до "*.htm*" или иной маски.
                SearchPattern = "*.html",

                // Блок отвечает за глубину поиска.
                // false = искать только в указанной папке.
                IncludeSubdirectories = false
            };

            // Блок путей-кандидатов.
            // 1) Рабочий сетевой путь для команды.
            // 2) Локальный fallback-путь для разработки/проверки.
            // Можно вручную менять или добавлять пути.
            options.CandidateDirectories.Add(
                @"G:\Общие диски\Ideologist Архитектурный\Софт\Плагины для Revit\SAB\Инструкции\Стандарты_HTML\Наименования");

            options.CandidateDirectories.Add(
                @"C:\Users\VB_User\Desktop\Стандарт наименований Revit");

            return options;
        }
    }
}
