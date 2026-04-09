using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SAB; // подключаем класс AllCategoriesByPlacement

namespace RevitLibraryBuilder.Services
{
    /// <summary>
    /// Сервис экспорта типов элементов в CSV с разделением по категориям
    /// </summary>
    public class CsvExportService
    {
        /// <summary>
        /// Экспортирует элементы в CSV, создавая отдельный файл для каждой категории
        /// </summary>
        /// <param name="types">Список всех типов элементов в документе</param>
        /// <param name="document">Документ Revit</param>
        /// <param name="outputFolder">Папка для сохранения CSV</param>
        public void ExportToCsv(List<ElementType> types, Document document, string outputFolder)
        {
            if (types == null || types.Count == 0)
                throw new ArgumentException("Список элементов пуст.");

            if (document == null)
                throw new ArgumentNullException(nameof(document));

            if (string.IsNullOrEmpty(outputFolder))
                throw new ArgumentException("Папка для сохранения не указана.", nameof(outputFolder));

            if (!Directory.Exists(outputFolder))
                Directory.CreateDirectory(outputFolder);

            // Проходим по всем категориям из AllCategoriesByPlacement
            foreach (var placementGroup in AllCategoriesByPlacement.CategoriesByPlacement)
            {
                string placementType = placementGroup.Key;
                BuiltInCategory[] categories = placementGroup.Value;

                foreach (var builtInCategory in categories)
                {
                    // Получаем тип категории из документа
                    Category category = Category.GetCategory(document, builtInCategory);
                    if (category == null) continue; // если категория не найдена в документе, пропускаем

                    string categoryName = category.Name;

                    // Формируем имя CSV файла: [Имя документа]_[Категория].csv
                    string safeDocName = MakeSafeFileName(document.Title);
                    string safeCategoryName = MakeSafeFileName(categoryName);
                    string fileName = Path.Combine(outputFolder, $"{safeDocName}_{safeCategoryName}.csv");

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Category,Family,TypeName,Include");

                    // Фильтруем элементы по данной категории
                    var filteredTypes = types
                        .Where(t => t.Category != null && t.Category.Id == category.Id)
                        .OrderBy(t => GetFamilyName(t))
                        .ThenBy(t => t.Name);

                    foreach (var type in filteredTypes)
                    {
                        string cat = Escape(type.Category.Name);
                        string family = Escape(GetFamilyName(type));
                        string typeName = Escape(type.Name);
                        string include = "TRUE";

                        sb.AppendLine($"{cat},{family},{typeName},{include}");
                    }

                    // Если в категории есть элементы — сохраняем CSV
                    if (filteredTypes.Any())
                        File.WriteAllText(fileName, sb.ToString(), Encoding.UTF8);
                }
            }
        }

        private string GetFamilyName(ElementType type)
        {
            return string.IsNullOrEmpty(type.FamilyName) ? string.Empty : type.FamilyName;
        }

        private string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(",") || value.Contains("\""))
                value = $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }

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