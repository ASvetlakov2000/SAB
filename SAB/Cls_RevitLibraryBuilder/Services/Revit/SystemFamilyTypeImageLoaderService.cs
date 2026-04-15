using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace RevitLibraryBuilder.Services.Revit
{
    /// <summary>
    /// Service that loads exported image files into system family types by type name.
    /// </summary>
    public class SystemFamilyTypeImageLoaderService
    {
        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".png",
            ".jpg",
            ".jpeg",
            ".bmp"
        };

        private static readonly HashSet<string> KnownButUnsupportedImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".gif",
            ".tif",
            ".tiff",
            ".webp"
        };

        public SystemFamilyTypeImageLoaderResult LoadFromFolder(Document document, string folderPath)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (string.IsNullOrWhiteSpace(folderPath))
            {
                throw new ArgumentException("Image folder path is empty.", nameof(folderPath));
            }

            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException("Selected image folder was not found.");
            }

            SystemFamilyTypeImageLoaderResult result = new SystemFamilyTypeImageLoaderResult();

            // Block responsible for reading and validating image files from selected folder.
            Dictionary<string, string> imagePathByTypeName = CollectImageFiles(folderPath, result);

            // Block responsible for collecting target system family types from supported categories.
            List<ElementType> targetTypes = CollectTargetTypes(document);
            result.TotalSupportedRevitTypesFound = targetTypes.Count;

            HashSet<string> matchedImageNames = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, ElementId> imageTypeIdByPath = new Dictionary<string, ElementId>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ElementId> existingImageTypeIdByName = CollectExistingImageTypeIdsByName(document);

            using (Transaction transaction = new Transaction(document, "Load system family type images"))
            {
                transaction.Start();

                for (int i = 0; i < targetTypes.Count; i++)
                {
                    ElementType elementType = targetTypes[i];

                    if (elementType == null)
                    {
                        continue;
                    }

                    string typeName = elementType.Name ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(typeName))
                    {
                        result.Errors.Add("Skipped type with empty name. ElementId: " + elementType.Id.IntegerValue);
                        continue;
                    }

                    // Exact match rule: image file name without extension must equal Revit type name.
                    string imagePath;

                    if (!imagePathByTypeName.TryGetValue(typeName, out imagePath))
                    {
                        result.UnmatchedTypes.Add(BuildTypeDescription(elementType));
                        continue;
                    }

                    result.MatchedPairsCount++;
                    matchedImageNames.Add(typeName);

                    try
                    {
                        Parameter typeImageParameter = elementType.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_IMAGE);

                        if (typeImageParameter == null)
                        {
                            result.Errors.Add(BuildTypeDescription(elementType) + " -> parameter ALL_MODEL_TYPE_IMAGE was not found.");
                            continue;
                        }

                        if (typeImageParameter.IsReadOnly)
                        {
                            result.Errors.Add(BuildTypeDescription(elementType) + " -> parameter ALL_MODEL_TYPE_IMAGE is read-only.");
                            continue;
                        }

                        if (typeImageParameter.StorageType != StorageType.ElementId)
                        {
                            result.Errors.Add(BuildTypeDescription(elementType) + " -> parameter ALL_MODEL_TYPE_IMAGE has unsupported storage type.");
                            continue;
                        }

                        ElementId imageTypeId = GetOrCreateImageTypeId(
                            document,
                            imagePath,
                            imageTypeIdByPath,
                            existingImageTypeIdByName);

                        if (imageTypeId == null || imageTypeId == ElementId.InvalidElementId)
                        {
                            result.Errors.Add(BuildTypeDescription(elementType) + " -> failed to load image: " + Path.GetFileName(imagePath));
                            continue;
                        }

                        ElementId currentImageId = typeImageParameter.AsElementId();

                        if (currentImageId == imageTypeId)
                        {
                            result.AlreadyAssignedCount++;
                            continue;
                        }

                        typeImageParameter.Set(imageTypeId);
                        result.SuccessfullyAssignedCount++;
                    }
                    catch (Exception exception)
                    {
                        result.Errors.Add(BuildTypeDescription(elementType) + " -> " + exception.Message);
                    }
                }

                transaction.Commit();
            }

            // Block responsible for detecting images that were not matched to any target type.
            foreach (KeyValuePair<string, string> pair in imagePathByTypeName)
            {
                if (!matchedImageNames.Contains(pair.Key))
                {
                    result.UnmatchedImageFiles.Add(Path.GetFileName(pair.Value) ?? pair.Value);
                }
            }

            return result;
        }

        private static Dictionary<string, string> CollectImageFiles(string folderPath, SystemFamilyTypeImageLoaderResult result)
        {
            Dictionary<string, string> imagePathByTypeName = new Dictionary<string, string>(StringComparer.Ordinal);
            string[] files = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly);

            for (int i = 0; i < files.Length; i++)
            {
                string filePath = files[i];
                string extension = Path.GetExtension(filePath) ?? string.Empty;

                if (SupportedExtensions.Contains(extension))
                {
                    result.TotalImageFilesFound++;

                    string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
                    {
                        result.Errors.Add("Image file has empty file name key: " + Path.GetFileName(filePath));
                        continue;
                    }

                    if (imagePathByTypeName.ContainsKey(fileNameWithoutExtension))
                    {
                        result.Errors.Add("Duplicate image file key found: " + fileNameWithoutExtension);
                        continue;
                    }

                    imagePathByTypeName.Add(fileNameWithoutExtension, filePath);
                    continue;
                }

                if (KnownButUnsupportedImageExtensions.Contains(extension))
                {
                    result.TotalImageFilesFound++;
                    result.UnsupportedImageFiles.Add(Path.GetFileName(filePath) ?? filePath);
                }
            }

            return imagePathByTypeName;
        }

        private static List<ElementType> CollectTargetTypes(Document document)
        {
            List<ElementType> result = new List<ElementType>();
            HashSet<int> categoryIds = BuildTargetCategoryIdSet(document);

            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(ElementType));

            foreach (Element element in collector)
            {
                ElementType elementType = element as ElementType;

                if (elementType == null || elementType.Category == null)
                {
                    continue;
                }

                if (!categoryIds.Contains(elementType.Category.Id.IntegerValue))
                {
                    continue;
                }

                // Block responsible for keeping only system family types.
                if (elementType is FamilySymbol)
                {
                    continue;
                }

                result.Add(elementType);
            }

            result.Sort(delegate (ElementType left, ElementType right)
            {
                string leftCategory = left.Category != null ? left.Category.Name : string.Empty;
                string rightCategory = right.Category != null ? right.Category.Name : string.Empty;
                int categoryCompare = string.Compare(leftCategory, rightCategory, StringComparison.OrdinalIgnoreCase);

                if (categoryCompare != 0)
                {
                    return categoryCompare;
                }

                return string.Compare(left.Name ?? string.Empty, right.Name ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            });

            return result;
        }

        private static HashSet<int> BuildTargetCategoryIdSet(Document document)
        {
            HashSet<int> result = new HashSet<int>();
            AddCategoryId(result, document, BuiltInCategory.OST_Walls);
            AddCategoryId(result, document, BuiltInCategory.OST_Floors);
            AddCategoryId(result, document, BuiltInCategory.OST_Roofs);
            AddCategoryId(result, document, BuiltInCategory.OST_Ceilings);
            return result;
        }

        private static void AddCategoryId(HashSet<int> set, Document document, BuiltInCategory builtInCategory)
        {
            if (set == null || document == null)
            {
                return;
            }

            try
            {
                Category category = Category.GetCategory(document, builtInCategory);

                if (category != null)
                {
                    set.Add(category.Id.IntegerValue);
                }
            }
            catch
            {
                // Category can be unavailable in specific templates/documents.
            }
        }

        private static Dictionary<string, ElementId> CollectExistingImageTypeIdsByName(Document document)
        {
            Dictionary<string, ElementId> result = new Dictionary<string, ElementId>(StringComparer.OrdinalIgnoreCase);
            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(ImageType));

            foreach (Element element in collector)
            {
                ImageType imageType = element as ImageType;

                if (imageType == null || string.IsNullOrWhiteSpace(imageType.Name))
                {
                    continue;
                }

                if (!result.ContainsKey(imageType.Name))
                {
                    result.Add(imageType.Name, imageType.Id);
                }
            }

            return result;
        }

        // Block responsible for importing image into document and returning ImageType ElementId.
        private static ElementId GetOrCreateImageTypeId(
            Document document,
            string imagePath,
            Dictionary<string, ElementId> imageTypeIdByPath,
            Dictionary<string, ElementId> existingImageTypeIdByName)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return ElementId.InvalidElementId;
            }

            ElementId cachedImageTypeId;

            if (imageTypeIdByPath.TryGetValue(imagePath, out cachedImageTypeId))
            {
                return cachedImageTypeId;
            }

            string imageName = Path.GetFileNameWithoutExtension(imagePath) ?? string.Empty;
            ElementId existingImageTypeId;

            if (!string.IsNullOrWhiteSpace(imageName) && existingImageTypeIdByName.TryGetValue(imageName, out existingImageTypeId))
            {
                imageTypeIdByPath[imagePath] = existingImageTypeId;
                return existingImageTypeId;
            }

            ImageTypeOptions imageTypeOptions = BuildImageTypeOptions(imagePath);

            if (imageTypeOptions == null)
            {
                return ElementId.InvalidElementId;
            }

            try
            {
                ImageType createdImageType = ImageType.Create(document, imageTypeOptions);

                if (createdImageType == null)
                {
                    return ElementId.InvalidElementId;
                }

                if (!string.IsNullOrWhiteSpace(imageName))
                {
                    try
                    {
                        createdImageType.Name = imageName;
                    }
                    catch
                    {
                        // Revit can reject duplicate names. Keep auto-generated name in that case.
                    }
                }

                imageTypeIdByPath[imagePath] = createdImageType.Id;

                if (!string.IsNullOrWhiteSpace(createdImageType.Name) && !existingImageTypeIdByName.ContainsKey(createdImageType.Name))
                {
                    existingImageTypeIdByName.Add(createdImageType.Name, createdImageType.Id);
                }

                return createdImageType.Id;
            }
            finally
            {
                imageTypeOptions.Dispose();
            }
        }

        // Block responsible for Revit-version-safe ImageTypeOptions creation.
        private static ImageTypeOptions BuildImageTypeOptions(string sourceImagePath)
        {
            Type optionsType = typeof(ImageTypeOptions);

            ConstructorInfo constructor = optionsType.GetConstructor(new[] { typeof(string), typeof(bool), typeof(ImageTypeSource) });

            if (constructor != null)
            {
                return constructor.Invoke(new object[] { sourceImagePath, false, ImageTypeSource.Import }) as ImageTypeOptions;
            }

            constructor = optionsType.GetConstructor(new[] { typeof(string), typeof(bool) });

            if (constructor != null)
            {
                return constructor.Invoke(new object[] { sourceImagePath, false }) as ImageTypeOptions;
            }

            constructor = optionsType.GetConstructor(new[] { typeof(string) });

            if (constructor != null)
            {
                return constructor.Invoke(new object[] { sourceImagePath }) as ImageTypeOptions;
            }

            return null;
        }

        private static string BuildTypeDescription(ElementType elementType)
        {
            if (elementType == null)
            {
                return "Unknown type";
            }

            string categoryName = elementType.Category != null ? elementType.Category.Name : "NoCategory";
            string familyName = ResolveFamilyName(elementType);
            string typeName = elementType.Name ?? string.Empty;
            return "[" + categoryName + "] " + familyName + " | " + typeName;
        }

        private static string ResolveFamilyName(ElementType elementType)
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
                string familyName = familyNameParameter.AsString();

                if (!string.IsNullOrWhiteSpace(familyName))
                {
                    return familyName;
                }
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Result object for loading images into system family types.
    /// </summary>
    public class SystemFamilyTypeImageLoaderResult
    {
        public int TotalImageFilesFound { get; set; }

        public int TotalSupportedRevitTypesFound { get; set; }

        public int MatchedPairsCount { get; set; }

        public int SuccessfullyAssignedCount { get; set; }

        public int AlreadyAssignedCount { get; set; }

        public List<string> UnmatchedTypes { get; set; } = new List<string>();

        public List<string> UnmatchedImageFiles { get; set; } = new List<string>();

        public List<string> UnsupportedImageFiles { get; set; } = new List<string>();

        public List<string> Errors { get; set; } = new List<string>();
    }
}
