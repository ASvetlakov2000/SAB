using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace RevitLibraryBuilder.Services.Revit
{
    /// <summary>
    /// Exports PNG thumbnails for loadable family types.
    /// </summary>
    public class LoadableFamilyThumbnailExportService
    {
        public const string OutputFolderName = "PNG_Family";

        public LoadableFamilyThumbnailExportResult ExportToFolder(
            Document document,
            List<ElementType> allTypes,
            string selectedRootFolder)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (allTypes == null || allTypes.Count == 0)
            {
                throw new ArgumentException("Element type list is empty.", nameof(allTypes));
            }

            if (string.IsNullOrWhiteSpace(selectedRootFolder))
            {
                throw new ArgumentException("Root folder path is empty.", nameof(selectedRootFolder));
            }

            LoadableFamilyThumbnailExportResult result = new LoadableFamilyThumbnailExportResult();
            result.OutputFolderPath = Path.Combine(selectedRootFolder, OutputFolderName);

            Directory.CreateDirectory(result.OutputFolderPath);

            for (int i = 0; i < allTypes.Count; i++)
            {
                ElementType elementType = allTypes[i];

                if (!(elementType is FamilySymbol))
                {
                    continue;
                }

                try
                {
                    if (!TryExportOneType(result.OutputFolderPath, elementType))
                    {
                        result.SkippedCount++;
                        continue;
                    }

                    result.ExportedCount++;
                }
                catch
                {
                    result.SkippedCount++;
                }
            }

            return result;
        }

        public bool HasLoadableFamilies(List<ElementType> types)
        {
            if (types == null)
            {
                return false;
            }

            for (int i = 0; i < types.Count; i++)
            {
                if (types[i] is FamilySymbol)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryExportOneType(string outputFolderPath, ElementType elementType)
        {
            if (string.IsNullOrWhiteSpace(outputFolderPath) || elementType == null)
            {
                return false;
            }

            using (Bitmap previewImage = elementType.GetPreviewImage(new Size(256, 256)))
            {
                if (previewImage == null)
                {
                    return false;
                }

                string safeTypeName = MakeSafeFileName(elementType.Name ?? string.Empty);
                string safeFamilyName = MakeSafeFileName(GetFamilyName(elementType));
                string safeFamilyTypeName = MakeSafeFileName(safeFamilyName + "_" + safeTypeName);

                if (string.IsNullOrWhiteSpace(safeTypeName) || string.IsNullOrWhiteSpace(safeFamilyTypeName))
                {
                    return false;
                }

                string familyTypePath = EnsureUniqueFilePath(Path.Combine(outputFolderPath, safeFamilyTypeName + ".png"));
                previewImage.Save(familyTypePath, ImageFormat.Png);

                string typeNamePath = Path.Combine(outputFolderPath, safeTypeName + ".png");

                if (!File.Exists(typeNamePath))
                {
                    File.Copy(familyTypePath, typeNamePath, false);
                }

                return true;
            }
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

            if (familyNameParameter == null)
            {
                return string.Empty;
            }

            string value = familyNameParameter.AsString();
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
        }

        private static string MakeSafeFileName(string value)
        {
            string result = value ?? string.Empty;

            if (string.IsNullOrWhiteSpace(result))
            {
                return string.Empty;
            }

            char[] invalidChars = Path.GetInvalidFileNameChars();

            for (int i = 0; i < invalidChars.Length; i++)
            {
                result = result.Replace(invalidChars[i], '_');
            }

            return result.Trim();
        }
    }

    public class LoadableFamilyThumbnailExportResult
    {
        public string OutputFolderPath { get; set; }

        public int ExportedCount { get; set; }

        public int SkippedCount { get; set; }
    }
}
