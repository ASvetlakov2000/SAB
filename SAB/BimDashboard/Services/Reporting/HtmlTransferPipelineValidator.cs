using System;
using System.Collections.Generic;
using System.IO;
using SAB.BimDashboard.Models;

namespace SAB.BimDashboard.Services.Reporting
{
    /// <summary>
    /// Валидатор цепочки передачи данных в HTML.
    /// </summary>
    public class HtmlTransferPipelineValidator
    {
        public void ValidateDashboardData(DashboardData data)
        {
            if (data == null)
            {
                throw new InvalidOperationException("Модель данных для HTML не создана.");
            }

            if (string.IsNullOrWhiteSpace(data.SourceName))
            {
                throw new InvalidOperationException("Источник данных для HTML не определен.");
            }

            ValidateTable(data.Columns, data.Rows);
        }

        public void ValidateHtmlModel(HtmlDashboardViewModel data)
        {
            if (data == null)
            {
                throw new InvalidOperationException("HTML view-model не создана.");
            }

            if (string.IsNullOrWhiteSpace(data.CatalogName))
            {
                throw new InvalidOperationException("Каталог проекта не заполнен для HTML.");
            }

            if (string.IsNullOrWhiteSpace(data.SourceName))
            {
                throw new InvalidOperationException("Поле 'Источник' не заполнено для HTML.");
            }

            if (string.IsNullOrWhiteSpace(data.SourceFormat))
            {
                throw new InvalidOperationException("Поле 'Формат источника' не заполнено для HTML.");
            }

            ValidateTable(data.Columns, data.Rows);
        }

        private static void ValidateTable(List<string> columns, List<List<string>> rows)
        {
            // Блок отвечает за проверку обязательных данных перед рендером HTML.
            if (columns == null || columns.Count == 0)
            {
                throw new InvalidOperationException("Модель HTML не содержит колонок. Рендер невозможен.");
            }

            if (rows == null)
            {
                throw new InvalidOperationException("Модель HTML не содержит строк. Рендер невозможен.");
            }

            for (int i = 0; i < rows.Count; i++)
            {
                List<string> row = rows[i];

                if (row == null)
                {
                    throw new InvalidOperationException("Найдена пустая строка модели HTML. Индекс строки: " + i);
                }

                if (row.Count != columns.Count)
                {
                    throw new InvalidOperationException(
                        "Количество значений строки не совпадает с количеством колонок. " +
                        "Строка: " + i + ", значений: " + row.Count + ", колонок: " + columns.Count);
                }
            }
        }

        public void ValidateRenderedHtml(string renderedHtml, string placeholder)
        {
            if (string.IsNullOrWhiteSpace(renderedHtml))
            {
                throw new InvalidOperationException("Сформированная HTML-строка пустая.");
            }

            // Блок отвечает за проверку, что placeholder действительно заменён.
            if (!string.IsNullOrWhiteSpace(placeholder) && renderedHtml.Contains(placeholder))
            {
                throw new InvalidOperationException("Placeholder в шаблоне не заменен: " + placeholder);
            }

            if (!renderedHtml.Contains("dashboard-data"))
            {
                throw new InvalidOperationException("В итоговом HTML отсутствует блок dashboard-data.");
            }

            if (!renderedHtml.Contains("catalogName") || !renderedHtml.Contains("sourceName") || !renderedHtml.Contains("sourceFormat"))
            {
                throw new InvalidOperationException("В итоговом HTML отсутствуют обязательные поля шапки dashboard.");
            }
        }

        public void ValidateSavedHtml(string htmlPath, string expectedJson)
        {
            if (string.IsNullOrWhiteSpace(htmlPath) || !File.Exists(htmlPath))
            {
                throw new InvalidOperationException("Итоговый HTML файл не найден после сохранения.");
            }

            string savedHtml = File.ReadAllText(htmlPath);

            // Здесь можно проверить, какие значения передаются в HTML после записи файла.
            if (!string.IsNullOrWhiteSpace(expectedJson) && !savedHtml.Contains(expectedJson))
            {
                throw new InvalidOperationException("Сохраненный HTML не содержит ожидаемый JSON-блок данных.");
            }
        }
    }
}
