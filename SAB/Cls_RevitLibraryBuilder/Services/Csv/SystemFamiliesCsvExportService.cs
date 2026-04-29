using Autodesk.Revit.DB;
using RevitLibraryBuilder.Services.Revit;
using System;
using System.Collections.Generic;
using System.IO;

namespace RevitLibraryBuilder.Services.Csv
{
    /// <summary>
    /// Выгрузка CSV системных семейств по отдельным категориям.
    /// </summary>
    public class SystemFamiliesCsvExportService
    {
        private const string FilePrefix = "Системные семейства";

        private readonly ElementThicknessService _thicknessService = new ElementThicknessService();
        private readonly ElementLayerStructureService _layerStructureService = new ElementLayerStructureService();
        private readonly CsvTableService _csvTableService = new CsvTableService();

        public List<string> Export(Document document, string selectedFolderPath)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            string outputFolder = ExportFolderRoutingService.ResolveSystemFamiliesExportFolder(selectedFolderPath);

            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                throw new InvalidOperationException("Не удалось определить папку выгрузки системных семейств.");
            }

            Dictionary<int, string> targetCategoryNames = BuildTargetCategoryNames(document);
            TypeCollectorService collectorService = new TypeCollectorService();
            List<ElementType> allTypes = collectorService.CollectAllTypes(document);

            Dictionary<int, List<ElementType>> groupedByCategory = new Dictionary<int, List<ElementType>>();

            for (int i = 0; i < allTypes.Count; i++)
            {
                ElementType type = allTypes[i];

                if (type == null || type.Category == null)
                {
                    continue;
                }

                if (type is FamilySymbol)
                {
                    continue;
                }

                int categoryId = type.Category.Id.IntegerValue;

                if (!targetCategoryNames.ContainsKey(categoryId))
                {
                    continue;
                }

                if (!groupedByCategory.ContainsKey(categoryId))
                {
                    groupedByCategory[categoryId] = new List<ElementType>();
                }

                groupedByCategory[categoryId].Add(type);
            }

            List<string> header = new List<string>
            {
                "Категория",
                "Семейство",
                "Миниатюра",
                "Типоразмер",
                "Структура",
                "Включить",
                "Толщина типа, мм"
            };

            List<string> exportedFiles = new List<string>();
            List<List<string>> allRows = new List<List<string>>();
            string modelPrefix = BuildFileName(document.Title);
            List<int> categoryIds = new List<int>(groupedByCategory.Keys);
            categoryIds.Sort(delegate (int left, int right)
            {
                string leftName = targetCategoryNames.ContainsKey(left) ? targetCategoryNames[left] : string.Empty;
                string rightName = targetCategoryNames.ContainsKey(right) ? targetCategoryNames[right] : string.Empty;
                return string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
            });

            for (int categoryIndex = 0; categoryIndex < categoryIds.Count; categoryIndex++)
            {
                int categoryId = categoryIds[categoryIndex];
                List<ElementType> categoryTypes = groupedByCategory[categoryId];

                if (categoryTypes == null || categoryTypes.Count == 0)
                {
                    continue;
                }

                SortTypes(categoryTypes);
                string categoryName = targetCategoryNames.ContainsKey(categoryId)
                    ? targetCategoryNames[categoryId]
                    : "Категория";

                List<List<string>> rows = new List<List<string>>();

                for (int typeIndex = 0; typeIndex < categoryTypes.Count; typeIndex++)
                {
                    ElementType type = categoryTypes[typeIndex];
                    string familyName = GetFamilyName(type);
                    string typeName = type.Name ?? string.Empty;

                    List<string> row = new List<string>
                    {
                        categoryName,
                        familyName,
                        ThumbnailPathResolverService.ResolveForElementType(type),
                        typeName,
                        _layerStructureService.GetLayerStructureText(type, document),
                        "TRUE",
                        _thicknessService.GetTotalThicknessMm(type)
                    };

                    rows.Add(row);
                    allRows.Add(new List<string>(row));
                }

                string fileName = BuildFileName(modelPrefix + "_" + FilePrefix + "_" + categoryName) + ".csv";
                string filePath = Path.Combine(outputFolder, fileName);
                _csvTableService.Write(filePath, header, rows);
                exportedFiles.Add(filePath);
            }

            if (allRows.Count > 0)
            {
                string allFileName = BuildFileName(modelPrefix + "_" + FilePrefix + "_Все категории") + ".csv";
                string allFilePath = Path.Combine(outputFolder, allFileName);
                _csvTableService.Write(allFilePath, header, allRows);
                exportedFiles.Add(allFilePath);
            }

            ExportFolderRoutingService.ConfigureThumbnailFoldersForSystemFamiliesExport(outputFolder);
            return exportedFiles;
        }

        private static Dictionary<int, string> BuildTargetCategoryNames(Document document)
        {
            Dictionary<int, string> categoryNames = new Dictionary<int, string>();
            List<BuiltInCategory> targetCategories = GetTargetSystemCategories();

            for (int i = 0; i < targetCategories.Count; i++)
            {
                BuiltInCategory builtInCategory = targetCategories[i];

                try
                {
                    Category category = Category.GetCategory(document, builtInCategory);

                    if (category != null)
                    {
                        categoryNames[category.Id.IntegerValue] = category.Name ?? string.Empty;
                    }
                }
                catch
                {
                    // Категория может отсутствовать в конкретном шаблоне проекта.
                }
            }

            return categoryNames;
        }

        private static List<BuiltInCategory> GetTargetSystemCategories()
        {
            List<BuiltInCategory> categories = new List<BuiltInCategory>();

            AddCategoryIfDefined(categories, "OST_Walls");
            AddCategoryIfDefined(categories, "OST_Floors");
            AddCategoryIfDefined(categories, "OST_Ceilings");
            AddCategoryIfDefined(categories, "OST_Roofs");

            return categories;
        }

        private static void AddCategoryIfDefined(List<BuiltInCategory> categories, string categoryName)
        {
            if (categories == null || string.IsNullOrWhiteSpace(categoryName))
            {
                return;
            }

            BuiltInCategory category;

            if (!Enum.TryParse(categoryName, out category))
            {
                return;
            }

            if (!Enum.IsDefined(typeof(BuiltInCategory), category))
            {
                return;
            }

            if (!categories.Contains(category))
            {
                categories.Add(category);
            }
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
    }
}
