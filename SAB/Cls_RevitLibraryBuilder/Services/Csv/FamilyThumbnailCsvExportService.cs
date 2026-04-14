using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace RevitLibraryBuilder.Services.Csv
{
    /// <summary>
    /// Сервис экспорта CSV для миниатюр семейств (загружаемые и системные).
    /// </summary>
    public class FamilyThumbnailCsvExportService
    {
        private const string ThumbnailSubFolder = "thumbnails";
        private const string LoadableThumbnailSubFolder = "loadable";

        private readonly CsvTableService _csvTableService;

        public FamilyThumbnailCsvExportService()
        {
            _csvTableService = new CsvTableService();
        }

        public string WriteLoadableFamilyThumbnails(string outputFolder, string documentTitle, List<ElementType> types)
        {
            ValidateInput(outputFolder, types);

            List<FamilyThumbnailRow> rows = BuildRows(types, includeLoadableFamilies: true);

            if (rows.Count == 0)
            {
                throw new InvalidOperationException("Loadable family types were not found.");
            }

            SortRows(rows);

            string safeDocumentName = MakeSafeFileName(documentTitle);
            string csvFileName = safeDocumentName + "_LOADABLE_FAMILY_THUMBNAILS.csv";
            string csvFilePath = Path.Combine(outputFolder, csvFileName);
            string thumbnailsFolder = Path.Combine(outputFolder, ThumbnailSubFolder, LoadableThumbnailSubFolder);

            Directory.CreateDirectory(outputFolder);
            Directory.CreateDirectory(thumbnailsFolder);

            List<string> header = BuildHeader();
            List<List<string>> dataRows = new List<List<string>>();

            // Блок формирования CSV строк и сохранения PNG миниатюр.
            for (int i = 0; i < rows.Count; i++)
            {
                FamilyThumbnailRow row = rows[i];

                string thumbnailRelativePath = string.Empty;
                string fileNameWithoutExtension = BuildThumbnailFileName(row, i + 1);
                string savedImagePath = TrySavePreviewImage(row.ElementType, thumbnailsFolder, fileNameWithoutExtension);

                if (!string.IsNullOrWhiteSpace(savedImagePath))
                {
                    thumbnailRelativePath = MakeRelativeCsvPath(outputFolder, savedImagePath);
                }

                dataRows.Add(new List<string>
                {
                    row.Category,
                    row.Family,
                    row.TypeName,
                    thumbnailRelativePath
                });
            }

            _csvTableService.Write(csvFilePath, header, dataRows);
            return csvFilePath;
        }

        public string WriteSystemFamilyThumbnailTemplate(string outputFolder, string documentTitle, List<ElementType> types)
        {
            ValidateInput(outputFolder, types);

            List<FamilyThumbnailRow> rows = BuildRows(types, includeLoadableFamilies: false);

            if (rows.Count == 0)
            {
                throw new InvalidOperationException("System family types were not found.");
            }

            SortRows(rows);
            Directory.CreateDirectory(outputFolder);

            string safeDocumentName = MakeSafeFileName(documentTitle);
            string csvFileName = safeDocumentName + "_SYSTEM_FAMILY_THUMBNAIL_TEMPLATE.csv";
            string csvFilePath = Path.Combine(outputFolder, csvFileName);

            List<string> header = BuildHeader();
            List<List<string>> dataRows = new List<List<string>>();

            // Блок шаблона CSV: путь миниатюры оставляем пустым для ручного заполнения.
            for (int i = 0; i < rows.Count; i++)
            {
                FamilyThumbnailRow row = rows[i];

                dataRows.Add(new List<string>
                {
                    row.Category,
                    row.Family,
                    row.TypeName,
                    string.Empty
                });
            }

            _csvTableService.Write(csvFilePath, header, dataRows);
            return csvFilePath;
        }

        public bool HasLoadableFamilies(List<ElementType> types)
        {
            if (types == null)
            {
                return false;
            }

            for (int i = 0; i < types.Count; i++)
            {
                ElementType elementType = types[i];

                if (elementType == null || elementType.Category == null)
                {
                    continue;
                }

                if (elementType is FamilySymbol)
                {
                    return true;
                }
            }

            return false;
        }

        public bool HasSystemFamilies(List<ElementType> types)
        {
            if (types == null)
            {
                return false;
            }

            for (int i = 0; i < types.Count; i++)
            {
                ElementType elementType = types[i];

                if (elementType == null || elementType.Category == null)
                {
                    continue;
                }

                if (!(elementType is FamilySymbol))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<FamilyThumbnailRow> BuildRows(List<ElementType> types, bool includeLoadableFamilies)
        {
            List<FamilyThumbnailRow> result = new List<FamilyThumbnailRow>();

            for (int i = 0; i < types.Count; i++)
            {
                ElementType type = types[i];

                if (type == null || type.Category == null)
                {
                    continue;
                }

                bool isLoadable = type is FamilySymbol;

                if (includeLoadableFamilies && !isLoadable)
                {
                    continue;
                }

                if (!includeLoadableFamilies && isLoadable)
                {
                    continue;
                }

                FamilyThumbnailRow row = new FamilyThumbnailRow();
                row.ElementType = type;
                row.Category = type.Category.Name ?? string.Empty;
                row.Family = GetFamilyName(type);
                row.TypeName = type.Name ?? string.Empty;

                result.Add(row);
            }

            return result;
        }

        // Блок детерминированной сортировки строк экспорта.
        private static void SortRows(List<FamilyThumbnailRow> rows)
        {
            rows.Sort(delegate (FamilyThumbnailRow left, FamilyThumbnailRow right)
            {
                int categoryCompare = string.Compare(left.Category, right.Category, StringComparison.OrdinalIgnoreCase);

                if (categoryCompare != 0)
                {
                    return categoryCompare;
                }

                int familyCompare = string.Compare(left.Family, right.Family, StringComparison.OrdinalIgnoreCase);

                if (familyCompare != 0)
                {
                    return familyCompare;
                }

                return string.Compare(left.TypeName, right.TypeName, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static string TrySavePreviewImage(ElementType elementType, string thumbnailsFolder, string fileNameWithoutExtension)
        {
            if (elementType == null || string.IsNullOrWhiteSpace(thumbnailsFolder))
            {
                return string.Empty;
            }

            try
            {
                using (Bitmap previewImage = elementType.GetPreviewImage(new Size(256, 256)))
                {
                    if (previewImage == null)
                    {
                        return string.Empty;
                    }

                    string initialPath = Path.Combine(thumbnailsFolder, fileNameWithoutExtension + ".png");
                    string uniquePath = EnsureUniqueFilePath(initialPath);
                    previewImage.Save(uniquePath, ImageFormat.Png);
                    return uniquePath;
                }
            }
            catch
            {
                // Для отдельных типов Revit миниатюра может быть недоступна.
                return string.Empty;
            }
        }

        private static string BuildThumbnailFileName(FamilyThumbnailRow row, int sequence)
        {
            string safeCategory = MakeSafeFileName(row.Category);
            string safeFamily = MakeSafeFileName(row.Family);
            string safeTypeName = MakeSafeFileName(row.TypeName);

            string baseName = sequence.ToString("D5") + "_" + safeCategory + "_" + safeFamily + "_" + safeTypeName;
            return MakeSafeFileName(baseName);
        }

        private static string EnsureUniqueFilePath(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return filePath;
            }

            string directory = Path.GetDirectoryName(filePath) ?? string.Empty;
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);

            int suffix = 2;

            while (true)
            {
                string candidate = Path.Combine(directory, fileNameWithoutExtension + "_" + suffix + extension);

                if (!File.Exists(candidate))
                {
                    return candidate;
                }

                suffix++;
            }
        }

        private static string MakeRelativeCsvPath(string rootFolder, string targetPath)
        {
            if (string.IsNullOrWhiteSpace(rootFolder) || string.IsNullOrWhiteSpace(targetPath))
            {
                return string.Empty;
            }

            Uri rootUri = new Uri(Path.GetFullPath(rootFolder).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            Uri targetUri = new Uri(Path.GetFullPath(targetPath));
            string relative = Uri.UnescapeDataString(rootUri.MakeRelativeUri(targetUri).ToString());

            return relative.Replace('\\', '/');
        }

        private static string GetFamilyName(ElementType elementType)
        {
            if (elementType == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(elementType.FamilyName))
            {
                return elementType.FamilyName;
            }

            Parameter familyNameParameter = elementType.get_Parameter(BuiltInParameter.SYMBOL_FAMILY_NAME_PARAM);

            if (familyNameParameter != null)
            {
                string value = familyNameParameter.AsString();

                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return "SystemFamily";
        }

        private static List<string> BuildHeader()
        {
            return new List<string>
            {
                "Category",
                "Family",
                "TypeName",
                "ThumbnailPath"
            };
        }

        private static void ValidateInput(string outputFolder, List<ElementType> types)
        {
            if (string.IsNullOrWhiteSpace(outputFolder))
            {
                throw new ArgumentException("Output folder is empty.");
            }

            if (types == null || types.Count == 0)
            {
                throw new ArgumentException("Element type list is empty.");
            }
        }

        private static string MakeSafeFileName(string value)
        {
            string result = value ?? string.Empty;

            if (string.IsNullOrWhiteSpace(result))
            {
                result = "Unnamed";
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();

            for (int i = 0; i < invalidChars.Length; i++)
            {
                result = result.Replace(invalidChars[i], '_');
            }

            return result.Trim();
        }

        private class FamilyThumbnailRow
        {
            public ElementType ElementType { get; set; }

            public string Category { get; set; }

            public string Family { get; set; }

            public string TypeName { get; set; }
        }
    }
}
