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

        public HtmlReportBuilder()
        {
            _jsonSerializerService = new JsonSerializerService();
            _pipelineValidator = new HtmlTransferPipelineValidator();
        }

        public string Generate(DashboardData dashboardData)
        {
            if (dashboardData == null)
            {
                throw new ArgumentNullException(nameof(dashboardData));
            }

            // Блок отвечает за формирование модели данных для HTML.
            _pipelineValidator.ValidateModel(dashboardData);

            // Блок отвечает за загрузку HTML шаблона.
            string templatePath = FileUtils.GetTemplateFilePath();
            string templateHtml = File.ReadAllText(templatePath, Encoding.UTF8);

            if (!templateHtml.Contains(DataPlaceholder))
            {
                throw new InvalidOperationException("В HTML шаблоне отсутствует placeholder {{DATA_JSON}}.");
            }

            // Блок отвечает за сериализацию модели в JSON.
            string dataJson = _jsonSerializerService.Serialize(dashboardData);

            if (string.IsNullOrWhiteSpace(dataJson))
            {
                throw new InvalidOperationException("Сериализация модели HTML вернула пустой JSON.");
            }

            // Блок отвечает за подстановку значений в HTML шаблон.
            string finalHtml = templateHtml.Replace(DataPlaceholder, dataJson);
            _pipelineValidator.ValidateRenderedHtml(finalHtml, DataPlaceholder);

            // Блок отвечает за сохранение итогового HTML файла.
            string outputDirectory = FileUtils.GetTempDashboardDirectory();
            string outputHtmlPath = Path.Combine(outputDirectory, "index.html");

            // Блок отвечает за копирование статических ассетов dashboard.
            string sourceWwwRoot = FileUtils.GetWwwRootDirectory();
            FileUtils.CopyWithOverwrite(Path.Combine(sourceWwwRoot, "dashboard.js"), Path.Combine(outputDirectory, "dashboard.js"));
            FileUtils.CopyWithOverwrite(Path.Combine(sourceWwwRoot, "dashboard.css"), Path.Combine(outputDirectory, "dashboard.css"));

            File.WriteAllText(outputHtmlPath, finalHtml, Encoding.UTF8);

            // Здесь можно проверить, какие значения передаются в HTML после записи файла.
            _pipelineValidator.ValidateSavedHtml(outputHtmlPath, dataJson);

            return outputHtmlPath;
        }
    }
}
