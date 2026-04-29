using Autodesk.Revit.DB;
using RevitLibraryBuilder.Services.Revit;
using SAB;
using System;
using System.Collections.Generic;
using System.IO;

namespace RevitLibraryBuilder.Services.Csv
{
    /// <summary>
    /// Выгрузка CSV загружаемых семейств по отдельным категориям.
    /// </summary>
    public class LoadableFamiliesCsvExportService
    {
        private const string FilePrefix = "Загружаемые семейства";

        private readonly CsvTableService _csvTableService = new CsvTableService();

        public List<string> Export(Document document, string selectedFolderPath)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            string outputFolder = ExportFolderRoutingService.ResolveLoadableFamiliesExportFolder(selectedFolderPath);

            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                throw new InvalidOperationException("Не удалось определить папку выгрузки загружаемых семейств.");
            }

            HashSet<int> allowedCategoryIds = BuildAllowedCategoryIdSet(document);
            TypeCollectorService collectorService = new TypeCollectorService();
            List<ElementType> allTypes = collectorService.CollectAllTypes(document);

            Dictionary<int, CategoryData> groupedByCategory = new Dictionary<int, CategoryData>();

            for (int i = 0; i < allTypes.Count; i++)
            {
                ElementType type = allTypes[i];

                if (!(type is FamilySymbol))
                {
                    continue;
                }

                if (type.Category == null)
                {
                    continue;
                }

                int categoryId = type.Category.Id.IntegerValue;

                if (!allowedCategoryIds.Contains(categoryId))
                {
                    continue;
                }

                if (!groupedByCategory.ContainsKey(categoryId))
                {
                    groupedByCategory[categoryId] = new CategoryData
                    {
                        CategoryName = type.Category.Name ?? string.Empty,
                        Types = new List<ElementType>()
                    };
                }

                groupedByCategory[categoryId].Types.Add(type);
            }

            List<string> header = new List<string>
            {
                "Категория",
                "Миниатюра",
                "Семейство",
                "Типоразмер",
                "Включить"
            };

            List<string> exportedFiles = new List<string>();
            List<CategoryData> categories = new List<CategoryData>(groupedByCategory.Values);
            categories.Sort(delegate (CategoryData left, CategoryData right)
            {
                return string.Compare(left.CategoryName, right.CategoryName, StringComparison.OrdinalIgnoreCase);
            });

            for (int categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
            {
                CategoryData categoryData = categories[categoryIndex];

                if (categoryData == null || categoryData.Types == null || categoryData.Types.Count == 0)
                {
                    continue;
                }

                SortTypes(categoryData.Types);

                List<List<string>> rows = new List<List<string>>();

                for (int typeIndex = 0; typeIndex < categoryData.Types.Count; typeIndex++)
                {
                    ElementType type = categoryData.Types[typeIndex];

                    rows.Add(new List<string>
                    {
                        categoryData.CategoryName,
                        ThumbnailPathResolverService.ResolveForElementType(type),
                        GetFamilyName(type),
                        type.Name ?? string.Empty,
                        "TRUE"
                    });
                }

                string fileName = BuildFileName(FilePrefix + "_" + categoryData.CategoryName) + ".csv";
                string filePath = Path.Combine(outputFolder, fileName);
                _csvTableService.Write(filePath, header, rows);
                exportedFiles.Add(filePath);
            }

            ExportFolderRoutingService.ConfigureThumbnailFoldersForLoadableFamiliesExport(outputFolder);
            return exportedFiles;
        }

        private static HashSet<int> BuildAllowedCategoryIdSet(Document document)
        {
            HashSet<int> ids = new HashSet<int>();

            foreach (BuiltInCategory builtInCategory in AllUsedCategoryList.categoryList)
            {
                try
                {
                    Category category = Category.GetCategory(document, builtInCategory);

                    if (category != null)
                    {
                        ids.Add(category.Id.IntegerValue);
                    }
                }
                catch
                {
                    // Категория может отсутствовать в конкретном шаблоне проекта.
                }
            }

            return ids;
        }

        private static string GetFamilyName(ElementType type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(type.FamilyName))
            {
                return type.FamilyName;
            }

            Parameter parameter = type.get_Parameter(BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM);

            if (parameter == null)
            {
                return string.Empty;
            }

            return parameter.AsString() ?? string.Empty;
        }

        private static void SortTypes(List<ElementType> types)
        {
            types.Sort(delegate (ElementType left, ElementType right)
            {
                int familyCompare = string.Compare(GetFamilyName(left), GetFamilyName(right), StringComparison.OrdinalIgnoreCase);

                if (familyCompare != 0)
                {
                    return familyCompare;
                }

                return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static string BuildFileName(string rawName)
        {
            string safeName = string.IsNullOrWhiteSpace(rawName)
                ? FilePrefix
                : rawName.Trim();

            char[] invalidChars = Path.GetInvalidFileNameChars();

            for (int i = 0; i < invalidChars.Length; i++)
            {
                safeName = safeName.Replace(invalidChars[i], '_');
            }

            return safeName;
        }

        private class CategoryData
        {
            public string CategoryName { get; set; }

            public List<ElementType> Types { get; set; }
        }
    }
}
