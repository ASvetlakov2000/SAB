using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using asBIM;
using SAB;

namespace RevitLibraryBuilder.Services
{
    /// <summary>
    /// Сервис экспорта типов элементов в CSV
    /// Поддерживает:
    /// 1. Отдельные файлы по категориям
    /// 2. Один файл со всеми категориями
    /// </summary>
    public class CsvExportService
    {
        /// <summary>
        /// Экспортирует элементы в CSV, создавая отдельный файл для каждой категории
        /// </summary>
        public void ExportToCsv(List<ElementType> types, Document document, string outputFolder)
        {
            // 🔹 Проверка входных данных
            if (types == null || types.Count == 0)
                throw new ArgumentException("Список элементов пуст.");

            if (document == null)
                throw new ArgumentNullException(nameof(document));

            if (string.IsNullOrEmpty(outputFolder))
                throw new ArgumentException("Папка для сохранения не указана.", nameof(outputFolder));

            // 🔹 Создаём папку, если её нет
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            // 🔹 Проходим по группам категорий (по типу размещения)
            foreach (var placementGroup in AllCategoriesByPlacement.CategoriesByPlacement)
            {
                BuiltInCategory[] categories = placementGroup.Value;

                foreach (var builtInCategory in categories)
                {
                    // 🔹 Получаем категорию из документа
                    Category category = Category.GetCategory(document, builtInCategory);
                    if (category == null) continue;

                    string categoryName = category.Name;

                    // 🔹 Формируем безопасное имя файла
                    string safeDocName = MakeSafeFileName(document.Title);
                    string safeCategoryName = MakeSafeFileName(categoryName);

                    string fileName = Path.Combine(outputFolder, $"{safeDocName}_{safeCategoryName}.csv");

                    StringBuilder sb = new StringBuilder();

                    // 🔹 Заголовок CSV
                    sb.AppendLine("Category,Family,TypeName,Include");

                    // 🔹 Фильтрация типов по категории
                    var filteredTypes = types
                        .Where(t => t.Category != null && t.Category.Id == category.Id)
                        .OrderBy(t => GetFamilyName(t))
                        .ThenBy(t => t.Name);

                    foreach (var type in filteredTypes)
                    {
                        // 🔹 Подготовка значений
                        string cat = Escape(type.Category.Name);
                        string family = Escape(GetFamilyName(type));
                        string typeName = Escape(type.Name);
                        string include = "TRUE";

                        sb.AppendLine($"{cat},{family},{typeName},{include}");
                    }

                    // 🔹 Записываем файл только если есть данные
                    if (filteredTypes.Any())
                        File.WriteAllText(fileName, sb.ToString(), Encoding.UTF8);
                }
            }
        }

        /// <summary>
        /// Экспортирует ВСЕ категории в ОДИН CSV файл
        /// </summary>
        public void ExportToSingleCsv(List<ElementType> types, Document document, string outputFolder)
        {
            // 🔹 Проверка входных данных
            if (types == null || types.Count == 0)
                throw new ArgumentException("Список элементов пуст.");

            if (document == null)
                throw new ArgumentNullException(nameof(document));

            if (string.IsNullOrEmpty(outputFolder))
                throw new ArgumentException("Папка не указана.");

            // 🔹 Создаём папку
            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            // 🔹 Имя файла
            string safeDocName = MakeSafeFileName(document.Title);
            string fileName = Path.Combine(outputFolder, $"{safeDocName}_ALL_CATEGORIES.csv");

            StringBuilder sb = new StringBuilder();

            // 🔹 Заголовок CSV
            sb.AppendLine("Category,Family,TypeName,Include");

            // 🔹 Группировка по категориям
            var grouped = types
                .Where(t => t.Category != null)
                .GroupBy(t => t.Category.Name)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                foreach (var type in group
                    .OrderBy(t => GetFamilyName(t))
                    .ThenBy(t => t.Name))
                {
                    string cat = Escape(group.Key);
                    string family = Escape(GetFamilyName(type));
                    string typeName = Escape(type.Name);
                    string include = "TRUE";

                    sb.AppendLine($"{cat},{family},{typeName},{include}");
                }
            }

            // 🔹 Запись файла
            File.WriteAllText(fileName, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Получение имени семейства
        /// </summary>
        private string GetFamilyName(ElementType type)
        {
            return string.IsNullOrEmpty(type.FamilyName) ? string.Empty : type.FamilyName;
        }

        /// <summary>
        /// Экранирование CSV (кавычки, запятые)
        /// </summary>
        private string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";

            if (value.Contains(",") || value.Contains("\""))
                value = $"\"{value.Replace("\"", "\"\"")}\"";

            return value;
        }

        /// <summary>
        /// Убираем запрещённые символы из имени файла
        /// </summary>
        private string MakeSafeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(c, '_');
            }
            return name;
        }
    }
}