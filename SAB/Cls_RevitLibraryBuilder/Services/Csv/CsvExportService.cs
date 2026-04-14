using Autodesk.Revit.DB;
using SAB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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
        public enum TypeCsvExportMode
        {
            Full = 0,
            Naming = 1
        }

        /// <summary>
        /// Экспортирует элементы в CSV, создавая отдельный файл для каждой категории
        /// </summary>
        public void ExportToCsv(List<ElementType> types, Document document, string outputFolder)
        {
            ExportToCsv(types, document, outputFolder, TypeCsvExportMode.Full);
        }

        /// <summary>
        /// Экспортирует элементы в CSV, создавая отдельный файл для каждой категории
        /// </summary>
        public void ExportToCsv(
            List<ElementType> types,
            Document document,
            string outputFolder,
            TypeCsvExportMode mode)
        {
            // Проверка входных данных
            ValidateInput(types, document, outputFolder);

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            // Проходим по группам категорий (по типу размещения)
            foreach (KeyValuePair<string, BuiltInCategory[]> placementGroup in AllCategoriesByPlacement.CategoriesByPlacement)
            {
                BuiltInCategory[] categories = placementGroup.Value;

                if (categories == null || categories.Length == 0)
                {
                    continue;
                }

                foreach (BuiltInCategory builtInCategory in categories)
                {
                    Category category = TryGetCategory(document, builtInCategory);

                    if (category == null)
                    {
                        continue;
                    }

                    string safeDocName = MakeSafeFileName(document.Title);
                    string safeCategoryName = MakeSafeFileName(category.Name);
                    string fileName = Path.Combine(outputFolder, safeDocName + "_" + safeCategoryName + ".csv");

                    List<ElementType> filteredTypes = FilterTypesByCategory(types, category.Id.IntegerValue);

                    if (filteredTypes.Count == 0)
                    {
                        continue;
                    }

                    SortTypes(filteredTypes);

                    StringBuilder stringBuilder = new StringBuilder();
                    AppendHeader(stringBuilder, mode);

                    for (int i = 0; i < filteredTypes.Count; i++)
                    {
                        AppendTypeRow(stringBuilder, filteredTypes[i], mode);
                    }

                    File.WriteAllText(fileName, stringBuilder.ToString(), Encoding.UTF8);
                }
            }
        }

        /// <summary>
        /// Экспортирует ВСЕ категории в ОДИН CSV файл
        /// </summary>
        public void ExportToSingleCsv(List<ElementType> types, Document document, string outputFolder)
        {
            ExportToSingleCsv(types, document, outputFolder, TypeCsvExportMode.Full);
        }

        /// <summary>
        /// Экспортирует ВСЕ категории в ОДИН CSV файл
        /// </summary>
        public void ExportToSingleCsv(
            List<ElementType> types,
            Document document,
            string outputFolder,
            TypeCsvExportMode mode)
        {
            // Проверка входных данных
            ValidateInput(types, document, outputFolder);

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            string safeDocName = MakeSafeFileName(document.Title);
            string suffix = mode == TypeCsvExportMode.Naming ? "_TYPE_NAMING.csv" : "_ALL_CATEGORIES.csv";
            string fileName = Path.Combine(outputFolder, safeDocName + suffix);

            Dictionary<string, List<ElementType>> groupedByCategory = GroupTypesByCategory(types);
            List<string> categoryNames = new List<string>(groupedByCategory.Keys);
            categoryNames.Sort(StringComparer.OrdinalIgnoreCase);

            StringBuilder stringBuilder = new StringBuilder();
            AppendHeader(stringBuilder, mode);

            for (int i = 0; i < categoryNames.Count; i++)
            {
                string categoryName = categoryNames[i];
                List<ElementType> groupedTypes = groupedByCategory[categoryName];
                SortTypes(groupedTypes);

                for (int j = 0; j < groupedTypes.Count; j++)
                {
                    AppendTypeRow(stringBuilder, groupedTypes[j], mode);
                }
            }

            File.WriteAllText(fileName, stringBuilder.ToString(), Encoding.UTF8);
        }

        // Блок выбора состава колонок CSV для разных сценариев выгрузки
        private static void AppendHeader(StringBuilder stringBuilder, TypeCsvExportMode mode)
        {
            if (mode == TypeCsvExportMode.Naming)
            {
                stringBuilder.AppendLine("Category,Family_Old,Family_New,TypeName_Old,TypeName_New");
                return;
            }

            stringBuilder.AppendLine("Category,Family,TypeName,Include");
        }

        // Блок формирования строки CSV с учетом профиля экспорта
        private void AppendTypeRow(StringBuilder stringBuilder, ElementType type, TypeCsvExportMode mode)
        {
            string categoryName = type.Category != null ? type.Category.Name : string.Empty;
            string familyName = GetFamilyName(type);
            string typeName = type.Name ?? string.Empty;

            if (mode == TypeCsvExportMode.Naming)
            {
                stringBuilder.AppendLine(
                    Escape(categoryName) + "," +
                    Escape(familyName) + "," +
                    Escape(familyName) + "," +
                    Escape(typeName) + "," +
                    Escape(typeName));
                return;
            }

            stringBuilder.AppendLine(
                Escape(categoryName) + "," +
                Escape(familyName) + "," +
                Escape(typeName) + "," +
                "TRUE");
        }

        private static Dictionary<string, List<ElementType>> GroupTypesByCategory(List<ElementType> types)
        {
            Dictionary<string, List<ElementType>> grouped = new Dictionary<string, List<ElementType>>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < types.Count; i++)
            {
                ElementType type = types[i];

                if (type == null || type.Category == null)
                {
                    continue;
                }

                string categoryName = type.Category.Name;

                if (!grouped.ContainsKey(categoryName))
                {
                    grouped[categoryName] = new List<ElementType>();
                }

                grouped[categoryName].Add(type);
            }

            return grouped;
        }

        private static List<ElementType> FilterTypesByCategory(List<ElementType> source, int categoryId)
        {
            List<ElementType> filteredTypes = new List<ElementType>();

            for (int i = 0; i < source.Count; i++)
            {
                ElementType type = source[i];

                if (type == null || type.Category == null)
                {
                    continue;
                }

                if (type.Category.Id.IntegerValue == categoryId)
                {
                    filteredTypes.Add(type);
                }
            }

            return filteredTypes;
        }

        private void SortTypes(List<ElementType> list)
        {
            list.Sort(delegate (ElementType left, ElementType right)
            {
                string leftFamily = GetFamilyName(left);
                string rightFamily = GetFamilyName(right);
                int familyCompare = string.Compare(leftFamily, rightFamily, StringComparison.OrdinalIgnoreCase);

                if (familyCompare != 0)
                {
                    return familyCompare;
                }

                return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static Category TryGetCategory(Document document, BuiltInCategory builtInCategory)
        {
            try
            {
                return Category.GetCategory(document, builtInCategory);
            }
            catch
            {
                return null;
            }
        }

        private static void ValidateInput(List<ElementType> types, Document document, string outputFolder)
        {
            if (types == null || types.Count == 0)
            {
                throw new ArgumentException("Список элементов пуст.");
            }

            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                throw new ArgumentException("Папка для сохранения не указана.", nameof(outputFolder));
            }
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
        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (value.Contains(",") || value.Contains("\""))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }

        /// <summary>
        /// Убираем запрещённые символы из имени файла
        /// </summary>
        private static string MakeSafeFileName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "Unnamed";
            }

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalidChar, '_');
            }

            return name;
        }
    }
}
