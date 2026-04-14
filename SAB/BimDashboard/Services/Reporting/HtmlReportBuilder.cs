using System;
using System.IO;
using System.Text;
using SAB.BimDashboard.Models;
using SAB.BimDashboard.Utils;

namespace SAB.BimDashboard.Services.Reporting
{
    /// <summary>
    /// Генератор HTML dashboard на основе шаблона и JSON-данных.
    /// </summary>
    public class HtmlReportBuilder
    {
        private const string DataPlaceholder = "{{DATA_JSON}}";

        private readonly JsonSerializerService _jsonSerializerService;
        private readonly HtmlTransferPipelineValidator _pipelineValidator;
        private readonly HtmlDashboardViewModelBuilder _htmlModelBuilder;

        public HtmlReportBuilder()
        {
            _jsonSerializerService = new JsonSerializerService();
            _pipelineValidator = new HtmlTransferPipelineValidator();
            _htmlModelBuilder = new HtmlDashboardViewModelBuilder();
        }

        public string Generate(DashboardData dashboardData)
        {
            if (dashboardData == null)
            {
                throw new ArgumentNullException(nameof(dashboardData));
            }

            _pipelineValidator.ValidateDashboardData(dashboardData);

            // Блок отвечает за формирование HTML модели.
            HtmlDashboardViewModel htmlModel = _htmlModelBuilder.Build(dashboardData);
            _pipelineValidator.ValidateHtmlModel(htmlModel);

            // Блок отвечает за загрузку исходных данных для HTML.
            string templatePath = FileUtils.GetTemplateFilePath();
            string templateHtml = File.ReadAllText(templatePath, Encoding.UTF8);

            if (!templateHtml.Contains(DataPlaceholder))
            {
                throw new InvalidOperationException("В HTML шаблоне отсутствует placeholder {{DATA_JSON}}.");
            }

            string dataJson = _jsonSerializerService.Serialize(htmlModel);

            if (string.IsNullOrWhiteSpace(dataJson))
            {
                throw new InvalidOperationException("Сериализация HTML модели вернула пустой JSON.");
            }

            // Блок отвечает за подстановку значений в HTML шаблон.
            string safeDataJson = MakeJsonSafeForInlineScript(dataJson);
            string finalHtml = templateHtml.Replace(DataPlaceholder, safeDataJson);
            _pipelineValidator.ValidateRenderedHtml(finalHtml, DataPlaceholder);

            // Блок отвечает за сохранение итогового HTML файла.
            string outputDirectory = FileUtils.GetTempDashboardDirectory();
            string outputHtmlPath = Path.Combine(outputDirectory, "index.html");

            string sourceWwwRoot = FileUtils.GetWwwRootDirectory();
            FileUtils.CopyWithOverwrite(Path.Combine(sourceWwwRoot, "dashboard.js"), Path.Combine(outputDirectory, "dashboard.js"));
            FileUtils.CopyWithOverwrite(Path.Combine(sourceWwwRoot, "dashboard.css"), Path.Combine(outputDirectory, "dashboard.css"));

            File.WriteAllText(outputHtmlPath, finalHtml, Encoding.UTF8);

            // Здесь можно проверить, какие значения передаются в HTML после записи файла.
            _pipelineValidator.ValidateSavedHtml(outputHtmlPath, safeDataJson);

            return outputHtmlPath;
        }

        private static string MakeJsonSafeForInlineScript(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return string.Empty;
            }

            // Защита от случайного закрытия script-тега внутри значений данных.
            string safe = json.Replace("</script", "<\\/script");
            safe = safe.Replace("</SCRIPT", "<\\/SCRIPT");
            return safe;
        }
    }
}
