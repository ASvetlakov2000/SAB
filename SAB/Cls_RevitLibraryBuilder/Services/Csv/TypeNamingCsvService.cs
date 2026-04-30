using Autodesk.Revit.DB;
using RevitLibraryBuilder.Models;
using RevitLibraryBuilder.Services;
using SAB;
using System;
using System.Collections.Generic;
using System.IO;

namespace RevitLibraryBuilder.Services.Csv
{
    /// <summary>
    /// Сервис XLSX/CSV импорта и XLSX экспорта для переименования типоразмеров.
    /// </summary>
    public class TypeNamingCsvService
    {
        private const string SystemGroupName = "Системные семейства";
        private const string LoadableGroupName = "Загружаемые семейства";
        private const string OtherGroupName = "Прочее";

        private readonly CsvTableService _csvTableService;
        private readonly NamingSpreadsheetExportService _namingSpreadsheetExportService;
        private readonly NamingSpreadsheetImportService _namingSpreadsheetImportService;
        private readonly ElementLayerStructureService _elementLayerStructureService;

        public TypeNamingCsvService()
        {
            _csvTableService = new CsvTableService();
            _namingSpreadsheetExportService = new NamingSpreadsheetExportService();
            _namingSpreadsheetImportService = new NamingSpreadsheetImportService();
            _elementLayerStructureService = new ElementLayerStructureService();
        }

        public List<TypeNamingCsvModel> ImportRows(string filePath)
        {
            CsvTable table = ReadNamingTable(filePath);

            // Блок отвечает за чтение столбцов из XLSX файла
            // Колонка Structure (если есть) используется только для просмотра и не участвует в переименовании
            int categoryIndex = FindRequiredHeaderIndex(table, "Категория", "Category");
            int familyOldIndex = FindRequiredHeaderIndex(table, "Старое семейство", "Family_Old");
            int familyNewIndex = FindRequiredHeaderIndex(table, "Новое семейство", "Family_New");
            int typeOldIndex = FindRequiredHeaderIndex(table, "Старый типоразмер", "TypeName_Old");
            int typeNewIndex = FindRequiredHeaderIndex(table, "Новый типоразмер", "TypeName_New");
            int deleteIndex = FindAnyHeaderIndex(table, "Удалить", "Delete", "Удалитьф");

            List<TypeNamingCsvModel> result = new List<TypeNamingCsvModel>();

            // Здесь нельзя менять порядок строк, так как переименование идет построчно
            for (int i = 0; i < table.Rows.Count; i++)
            {
                CsvTableRow row = table.Rows[i];

                TypeNamingCsvModel model = new TypeNamingCsvModel
                {
                    RowIndex = row.RowIndex,
                    Category = row.GetValue(categoryIndex),
                    FamilyOld = row.GetValue(familyOldIndex),
                    FamilyNew = row.GetValue(familyNewIndex),
                    TypeNameOld = row.GetValue(typeOldIndex),
                    TypeNameNew = row.GetValue(typeNewIndex),
                    DeleteType = ParseBoolean(row.GetValue(deleteIndex))
                };

                if (string.IsNullOrWhiteSpace(model.TypeNameOld))
                {
                    continue;
                }

                result.Add(model);
            }

            return result;
        }

        public TypeNamingExportResult WriteTypeNamingXlsxByGroups(
            string outputFolder,
            string documentTitle,
            List<ElementType> types,
            Document document)
        {
            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                throw new ArgumentException("Output folder is empty.");
            }

            if (types == null || types.Count == 0)
            {
                throw new ArgumentException("Element type list is empty.");
            }

            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            GroupedTypeNamingRows groupedRows = SplitRowsByGroups(types, document);
            TypeNamingExportResult exportResult = new TypeNamingExportResult();
            string safeDocumentName = MakeSafeFileName(documentTitle);

            WriteGroupWorkbook(
                exportResult,
                outputFolder,
                safeDocumentName + "_TYPE_NAMING_SYSTEM_FAMILIES.xlsx",
                SystemGroupName,
                groupedRows.SystemRows,
                true);

            WriteGroupWorkbook(
                exportResult,
                outputFolder,
                safeDocumentName + "_TYPE_NAMING_LOADABLE_FAMILIES.xlsx",
                LoadableGroupName,
                groupedRows.LoadableRows,
                false);

            WriteGroupWorkbook(
                exportResult,
                outputFolder,
                safeDocumentName + "_TYPE_NAMING_OTHER.xlsx",
                OtherGroupName,
                groupedRows.OtherRows,
                false);

            return exportResult;
        }

        public string WriteErrorReport(string importFilePath, List<NamingErrorCsvModel> errors)
        {
            if (errors == null || errors.Count == 0)
            {
                return string.Empty;
            }

            string folder = Path.GetDirectoryName(importFilePath);

            if (string.IsNullOrWhiteSpace(folder))
            {
                folder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            }

            string filePath = Path.Combine(folder, "Проблемные наименования.csv");

            List<string> header = new List<string> { "OldName", "NewName", "ErrorText" };
            List<List<string>> rows = new List<List<string>>();

            for (int i = 0; i < errors.Count; i++)
            {
                NamingErrorCsvModel error = errors[i];

                rows.Add(new List<string>
                {
                    error.OldName,
                    error.NewName,
                    error.ErrorText
                });
            }

            _csvTableService.Write(filePath, header, rows);
            return filePath;
        }

        private void WriteGroupWorkbook(
            TypeNamingExportResult exportResult,
            string outputFolder,
            string fileName,
            string groupName,
            List<GroupedTypeNamingRow> groupedRows,
            bool includeStructureColumn)
        {
            if (groupedRows == null || groupedRows.Count == 0)
            {
                return;
            }

            // Блок отвечает за настройку состава столбцов выгрузки
            List<string> headers = new List<string>
            {
                "Категория",
                "Старое семейство",
                "Новое семейство",
                "Старый типоразмер",
                "Новый типоразмер"
            };

            if (includeStructureColumn)
            {
                headers.Add("Структура");
            }

            headers.Add("Удалить");

            List<List<string>> rows = new List<List<string>>();

            for (int i = 0; i < groupedRows.Count; i++)
            {
                GroupedTypeNamingRow groupedRow = groupedRows[i];

                if (groupedRow == null || groupedRow.NamingModel == null)
                {
                    continue;
                }

                List<string> row = new List<string>
                {
                    groupedRow.NamingModel.Category,
                    groupedRow.NamingModel.FamilyOld,
                    groupedRow.NamingModel.FamilyNew,
                    groupedRow.NamingModel.TypeNameOld,
                    groupedRow.NamingModel.TypeNameNew
                };

                if (includeStructureColumn)
                {
                    row.Add(groupedRow.StructureText ?? string.Empty);
                }

                row.Add(groupedRow.NamingModel.DeleteType ? "1" : "0");

                rows.Add(row);
            }

            if (rows.Count == 0)
            {
                return;
            }

            string filePath = _namingSpreadsheetExportService.WriteNamingWorkbook(outputFolder, fileName, headers, rows);

            exportResult.Files.Add(new TypeNamingExportFileInfo
            {
                GroupName = groupName,
                FilePath = filePath,
                RowCount = rows.Count
            });
        }

        private GroupedTypeNamingRows SplitRowsByGroups(List<ElementType> types, Document document)
        {
            GroupedTypeNamingRows result = new GroupedTypeNamingRows();
            HashSet<int> systemCategoryIds = BuildSystemCategoryIdSet(document);
            HashSet<int> loadableCategoryIds = BuildLoadableCategoryIdSet(document);

            // Блок распределения строк по группам: системные / загружаемые / прочее
            for (int i = 0; i < types.Count; i++)
            {
                ElementType type = types[i];

                if (type == null || type.Category == null)
                {
                    continue;
                }

                TypeNamingCsvModel namingModel = BuildNamingModel(type);

                if (namingModel == null)
                {
                    continue;
                }

                int categoryId = type.Category.Id.IntegerValue;

                if (IsSystemNamingType(type, categoryId, systemCategoryIds))
                {
                    result.SystemRows.Add(new GroupedTypeNamingRow
                    {
                        NamingModel = namingModel,
                        StructureText = _elementLayerStructureService.GetLayerStructureText(type, document)
                    });

                    continue;
                }

                if (IsLoadableNamingType(type, categoryId, loadableCategoryIds))
                {
                    result.LoadableRows.Add(new GroupedTypeNamingRow
                    {
                        NamingModel = namingModel,
                        StructureText = string.Empty
                    });

                    continue;
                }

                result.OtherRows.Add(new GroupedTypeNamingRow
                {
                    NamingModel = namingModel,
                    StructureText = string.Empty
                });
            }

            return result;
        }

        private static bool IsSystemNamingType(ElementType type, int categoryId, HashSet<int> systemCategoryIds)
        {
            if (type == null)
            {
                return false;
            }

            if (type is FamilySymbol)
            {
                return false;
            }

            return systemCategoryIds.Contains(categoryId);
        }

        private static bool IsLoadableNamingType(ElementType type, int categoryId, HashSet<int> loadableCategoryIds)
        {
            if (!(type is FamilySymbol))
            {
                return false;
            }

            return loadableCategoryIds.Contains(categoryId);
        }

        private static HashSet<int> BuildSystemCategoryIdSet(Document document)
        {
            HashSet<int> ids = new HashSet<int>();

            AddCategoryIdIfAvailable(ids, document, "OST_Walls");
            AddCategoryIdIfAvailable(ids, document, "OST_Floors");
            AddCategoryIdIfAvailable(ids, document, "OST_Ceilings");
            AddCategoryIdIfAvailable(ids, document, "OST_Roofs");

            return ids;
        }

        private static HashSet<int> BuildLoadableCategoryIdSet(Document document)
        {
            HashSet<int> ids = new HashSet<int>();

            foreach (BuiltInCategory category in AllUsedCategoryList.categoryList)
            {
                try
                {
                    Category revitCategory = Category.GetCategory(document, category);

                    if (revitCategory != null)
                    {
                        ids.Add(revitCategory.Id.IntegerValue);
                    }
                }
                catch
                {
                    // Категория может отсутствовать в конкретном шаблоне проекта.
                }
            }

            return ids;
        }

        private static void AddCategoryIdIfAvailable(HashSet<int> ids, Document document, string categoryName)
        {
            if (ids == null || document == null || string.IsNullOrWhiteSpace(categoryName))
            {
                return;
            }

            BuiltInCategory builtInCategory;

            if (!Enum.TryParse(categoryName, out builtInCategory))
            {
                return;
            }

            if (!Enum.IsDefined(typeof(BuiltInCategory), builtInCategory))
            {
                return;
            }

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

        private static TypeNamingCsvModel BuildNamingModel(ElementType type)
        {
            if (type == null || type.Category == null)
            {
                return null;
            }

            string category = type.Category.Name ?? string.Empty;
            string family = type.FamilyName ?? string.Empty;
            string typeName = type.Name ?? string.Empty;

            return new TypeNamingCsvModel
            {
                Category = category,
                FamilyOld = family,
                FamilyNew = family,
                TypeNameOld = typeName,
                TypeNameNew = typeName,
                DeleteType = false
            };
        }

        private static int FindAnyHeaderIndex(CsvTable table, params string[] headerNames)
        {
            if (table == null || headerNames == null)
            {
                return -1;
            }

            for (int i = 0; i < headerNames.Length; i++)
            {
                int index = table.FindHeaderIndex(headerNames[i]);

                if (index >= 0)
                {
                    return index;
                }
            }

            return -1;
        }

        private static int FindRequiredHeaderIndex(CsvTable table, params string[] headerNames)
        {
            int index = FindAnyHeaderIndex(table, headerNames);

            if (index >= 0)
            {
                return index;
            }

            string headerText = headerNames != null ? string.Join(" / ", headerNames) : string.Empty;
            throw new InvalidOperationException("В таблице отсутствует обязательный столбец: " + headerText);
        }

        // Блок чтения признака удаления из naming-таблицы
        private static bool ParseBoolean(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = value.Trim().ToUpperInvariant();

            return normalized == "TRUE" ||
                   normalized == "1" ||
                   normalized == "YES" ||
                   normalized == "ДА";
        }

        private CsvTable ReadNamingTable(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Input file path is empty.");
            }

            string extension = Path.GetExtension(filePath) ?? string.Empty;

            if (string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                return _namingSpreadsheetImportService.ReadAsTable(filePath);
            }

            // Поддержка CSV оставлена как безопасный fallback совместимости
            return _csvTableService.Read(filePath);
        }

        private static string MakeSafeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "Unnamed";
            }

            foreach (char invalidChar in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalidChar, '_');
            }

            return name;
        }

        private class GroupedTypeNamingRows
        {
            public GroupedTypeNamingRows()
            {
                SystemRows = new List<GroupedTypeNamingRow>();
                LoadableRows = new List<GroupedTypeNamingRow>();
                OtherRows = new List<GroupedTypeNamingRow>();
            }

            public List<GroupedTypeNamingRow> SystemRows { get; private set; }

            public List<GroupedTypeNamingRow> LoadableRows { get; private set; }

            public List<GroupedTypeNamingRow> OtherRows { get; private set; }
        }

        private class GroupedTypeNamingRow
        {
            public TypeNamingCsvModel NamingModel { get; set; }

            public string StructureText { get; set; }
        }
    }

    public class TypeNamingExportResult
    {
        public TypeNamingExportResult()
        {
            Files = new List<TypeNamingExportFileInfo>();
        }

        public List<TypeNamingExportFileInfo> Files { get; private set; }

        public int TotalFilesCount
        {
            get { return Files.Count; }
        }
    }

    public class TypeNamingExportFileInfo
    {
        public string GroupName { get; set; }

        public string FilePath { get; set; }

        public int RowCount { get; set; }
    }
}
