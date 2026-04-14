using Autodesk.Revit.DB;
using RevitLibraryBuilder.Services.Csv;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace RevitLibraryBuilder.Services.Revit
{
    /// <summary>
    /// Сервис загрузки PNG пирогов в системный параметр "Изображение типоразмера".
    /// </summary>
    public class SystemTypePieImageLoadService
    {
        private const string PiePrefix = "Пирог";
        private const string TypeImageParameterNameRu = "Изображение типоразмера";
        private const string TypeImageParameterNameEn = "Type Image";

        private static readonly char[] ProhibitedNameCharacters = new char[] { '{', '}', '[', ']', ';', '<', '>', '?', '^', '~' };

        private readonly CsvTableService _csvTableService;

        public SystemTypePieImageLoadService()
        {
            _csvTableService = new CsvTableService();
        }

        public SystemTypePieImageLoadResult LoadFromFolder(Document document, string imageFolderPath)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (string.IsNullOrWhiteSpace(imageFolderPath) || !Directory.Exists(imageFolderPath))
            {
                throw new DirectoryNotFoundException("Папка с изображениями не найдена.");
            }

            SystemTypePieImageLoadResult result = new SystemTypePieImageLoadResult();
            List<SystemTypePieImageLoadIssue> errors = new List<SystemTypePieImageLoadIssue>();

            Dictionary<string, string> imagePathByKey = CollectImageFiles(imageFolderPath, errors);
            List<SystemTypeImageTarget> targets = CollectTargetTypes(document);

            result.TotalImagesInFolder = imagePathByKey.Count;
            result.TotalTargetTypes = targets.Count;

            if (targets.Count == 0)
            {
                errors.Add(new SystemTypePieImageLoadIssue
                {
                    ErrorText = "Не найдены целевые системные типы для загрузки изображений."
                });
            }

            Dictionary<string, ElementId> cachedImageTypeIdsByPath = new Dictionary<string, ElementId>(StringComparer.OrdinalIgnoreCase);
            HashSet<string> usedImageKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (Transaction transaction = new Transaction(document, "Load PNG pies into type image"))
            {
                transaction.Start();

                for (int i = 0; i < targets.Count; i++)
                {
                    SystemTypeImageTarget target = targets[i];

                    try
                    {
                        string imageKey = ResolveImageKeyForType(target, imagePathByKey);

                        if (string.IsNullOrWhiteSpace(imageKey))
                        {
                            errors.Add(new SystemTypePieImageLoadIssue
                            {
                                Category = target.Category,
                                Family = target.Family,
                                TypeName = target.TypeName,
                                ErrorText = "Подходящее изображение не найдено в выбранной папке."
                            });
                            continue;
                        }

                        string imagePath = imagePathByKey[imageKey];
                        usedImageKeys.Add(imageKey);
                        result.MatchedByNameCount++;

                        Parameter typeImageParameter = ResolveTypeImageParameter(target.ElementType);

                        if (typeImageParameter == null)
                        {
                            errors.Add(new SystemTypePieImageLoadIssue
                            {
                                Category = target.Category,
                                Family = target.Family,
                                TypeName = target.TypeName,
                                ImageFile = Path.GetFileName(imagePath),
                                ErrorText = "Параметр 'Изображение типоразмера' не найден."
                            });
                            continue;
                        }

                        if (typeImageParameter.IsReadOnly)
                        {
                            errors.Add(new SystemTypePieImageLoadIssue
                            {
                                Category = target.Category,
                                Family = target.Family,
                                TypeName = target.TypeName,
                                ImageFile = Path.GetFileName(imagePath),
                                ErrorText = "Параметр 'Изображение типоразмера' доступен только для чтения."
                            });
                            continue;
                        }

                        if (typeImageParameter.StorageType != StorageType.ElementId)
                        {
                            errors.Add(new SystemTypePieImageLoadIssue
                            {
                                Category = target.Category,
                                Family = target.Family,
                                TypeName = target.TypeName,
                                ImageFile = Path.GetFileName(imagePath),
                                ErrorText = "Параметр 'Изображение типоразмера' имеет неподдерживаемый тип хранения."
                            });
                            continue;
                        }

                        ElementId imageTypeId = GetOrCreateImageTypeId(document, imagePath, cachedImageTypeIdsByPath);

                        if (imageTypeId == ElementId.InvalidElementId)
                        {
                            errors.Add(new SystemTypePieImageLoadIssue
                            {
                                Category = target.Category,
                                Family = target.Family,
                                TypeName = target.TypeName,
                                ImageFile = Path.GetFileName(imagePath),
                                ErrorText = "Не удалось создать или получить тип изображения."
                            });
                            continue;
                        }

                        ElementId currentImageId = typeImageParameter.AsElementId();

                        if (currentImageId == imageTypeId)
                        {
                            result.AlreadyAssignedCount++;
                            continue;
                        }

                        typeImageParameter.Set(imageTypeId);
                        result.AssignedCount++;
                    }
                    catch (Exception exception)
                    {
                        errors.Add(new SystemTypePieImageLoadIssue
                        {
                            Category = target.Category,
                            Family = target.Family,
                            TypeName = target.TypeName,
                            ErrorText = exception.Message
                        });
                    }
                }

                transaction.Commit();
            }

            // Блок проверки лишних изображений: файл есть, но ни к одному типу не применился.
            foreach (KeyValuePair<string, string> imagePair in imagePathByKey)
            {
                if (usedImageKeys.Contains(imagePair.Key))
                {
                    continue;
                }

                errors.Add(new SystemTypePieImageLoadIssue
                {
                    ImageFile = Path.GetFileName(imagePair.Value),
                    ErrorText = "Изображение не сопоставлено ни с одним системным типом."
                });
            }

            result.Errors = errors;
            result.ReportPath = WriteErrorReport(imageFolderPath, errors);
            return result;
        }

        private static Dictionary<string, string> CollectImageFiles(string imageFolderPath, List<SystemTypePieImageLoadIssue> errors)
        {
            Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string[] imageFiles = Directory.GetFiles(imageFolderPath, "*.png", SearchOption.TopDirectoryOnly);

            for (int i = 0; i < imageFiles.Length; i++)
            {
                string imagePath = imageFiles[i];
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(imagePath) ?? string.Empty;
                string key = NormalizeKey(fileNameWithoutExtension);

                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (result.ContainsKey(key))
                {
                    if (errors != null)
                    {
                        errors.Add(new SystemTypePieImageLoadIssue
                        {
                            ImageFile = Path.GetFileName(imagePath),
                            ErrorText = "Обнаружены изображения с одинаковыми именами файла."
                        });
                    }

                    continue;
                }

                result.Add(key, imagePath);
            }

            return result;
        }

        // Блок отбора только целевых системных категорий и только системных типов.
        private static List<SystemTypeImageTarget> CollectTargetTypes(Document document)
        {
            List<SystemTypeImageTarget> result = new List<SystemTypeImageTarget>();
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

                if (elementType is FamilySymbol)
                {
                    continue;
                }

                string categoryName = elementType.Category.Name ?? string.Empty;
                string familyName = ResolveFamilyName(elementType, categoryName);
                string typeName = elementType.Name ?? string.Empty;
                string legendName = BuildLegendName(familyName, typeName);

                SystemTypeImageTarget item = new SystemTypeImageTarget();
                item.ElementType = elementType;
                item.Category = categoryName;
                item.Family = familyName;
                item.TypeName = typeName;
                item.ExpectedLegendName = legendName;
                result.Add(item);
            }

            result.Sort(delegate (SystemTypeImageTarget left, SystemTypeImageTarget right)
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

            return result;
        }

        private static HashSet<int> BuildTargetCategoryIdSet(Document document)
        {
            HashSet<int> result = new HashSet<int>();

            List<string> categoryNames = new List<string>();
            categoryNames.Add("OST_Ceilings");
            categoryNames.Add("OST_Floors");
            categoryNames.Add("OST_Walls");
            categoryNames.Add("OST_Roofs");
            categoryNames.Add("OST_RoofSoffit");
            categoryNames.Add("OST_StructuralFoundation");

            for (int i = 0; i < categoryNames.Count; i++)
            {
                try
                {
                    BuiltInCategory builtInCategory;

                    if (!Enum.TryParse(categoryNames[i], out builtInCategory))
                    {
                        continue;
                    }

                    if (!Enum.IsDefined(typeof(BuiltInCategory), builtInCategory))
                    {
                        continue;
                    }

                    Category category = Category.GetCategory(document, builtInCategory);

                    if (category != null)
                    {
                        result.Add(category.Id.IntegerValue);
                    }
                }
                catch
                {
                    // Категория может отсутствовать в конкретном шаблоне/версии Revit.
                }
            }

            return result;
        }

        private static string ResolveFamilyName(ElementType elementType, string fallbackCategory)
        {
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

            return string.IsNullOrWhiteSpace(fallbackCategory) ? "SystemFamily" : fallbackCategory;
        }

        private static string BuildLegendName(string familyName, string typeName)
        {
            // Формат наименования: Пирог_Семейство_Тип
            // Семейство и Тип передаются из параметров обрабатываемого ElementType.
            string safeFamily = string.IsNullOrWhiteSpace(familyName) ? "Family" : familyName.Trim();
            string safeType = string.IsNullOrWhiteSpace(typeName) ? "Type" : typeName.Trim();
            string rawName = PiePrefix + "_" + safeFamily + "_" + safeType;
            return SanitizeName(rawName);
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string result = value.Trim();

            for (int i = 0; i < ProhibitedNameCharacters.Length; i++)
            {
                result = result.Replace(ProhibitedNameCharacters[i].ToString(), "_");
            }

            char[] invalidFileChars = Path.GetInvalidFileNameChars();

            for (int i = 0; i < invalidFileChars.Length; i++)
            {
                result = result.Replace(invalidFileChars[i].ToString(), "_");
            }

            while (result.Contains("__"))
            {
                result = result.Replace("__", "_");
            }

            return result.Trim();
        }

        private static string ResolveImageKeyForType(SystemTypeImageTarget target, Dictionary<string, string> imagePathByKey)
        {
            string expectedLegendKey = NormalizeKey(target.ExpectedLegendName);

            if (!string.IsNullOrWhiteSpace(expectedLegendKey) && imagePathByKey.ContainsKey(expectedLegendKey))
            {
                return expectedLegendKey;
            }

            string typeNameKey = NormalizeKey(target.TypeName);

            if (!string.IsNullOrWhiteSpace(typeNameKey) && imagePathByKey.ContainsKey(typeNameKey))
            {
                return typeNameKey;
            }

            return string.Empty;
        }

        private static string NormalizeKey(string value)
        {
            string result = (value ?? string.Empty).Trim();
            result = result.Replace("\"", string.Empty);
            result = result.Replace("«", string.Empty);
            result = result.Replace("»", string.Empty);
            return result.ToLowerInvariant();
        }

        private static Parameter ResolveTypeImageParameter(ElementType elementType)
        {
            if (elementType == null)
            {
                return null;
            }

            Parameter parameter = elementType.LookupParameter(TypeImageParameterNameRu);

            if (parameter != null)
            {
                return parameter;
            }

            parameter = elementType.LookupParameter(TypeImageParameterNameEn);

            if (parameter != null)
            {
                return parameter;
            }

            ParameterSet parameters = elementType.Parameters;

            foreach (Parameter current in parameters)
            {
                if (current == null || current.Definition == null)
                {
                    continue;
                }

                string definitionName = current.Definition.Name ?? string.Empty;

                if (string.Equals(definitionName, TypeImageParameterNameRu, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(definitionName, TypeImageParameterNameEn, StringComparison.OrdinalIgnoreCase))
                {
                    return current;
                }
            }

            return null;
        }

        // Блок создания/повторного использования ImageType по PNG файлу.
        private static ElementId GetOrCreateImageTypeId(
            Document document,
            string imagePath,
            Dictionary<string, ElementId> cachedImageTypeIdsByPath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                return ElementId.InvalidElementId;
            }

            ElementId cachedId;

            if (cachedImageTypeIdsByPath.TryGetValue(imagePath, out cachedId))
            {
                return cachedId;
            }

            string imageName = Path.GetFileNameWithoutExtension(imagePath) ?? string.Empty;
            ElementId existingId = FindExistingImageTypeIdByName(document, imageName);

            if (existingId != ElementId.InvalidElementId)
            {
                cachedImageTypeIdsByPath[imagePath] = existingId;
                return existingId;
            }

            ImageTypeOptions imageTypeOptions = BuildImageTypeOptions(imagePath);

            if (imageTypeOptions == null)
            {
                return ElementId.InvalidElementId;
            }

            try
            {
                ImageType imageType = ImageType.Create(document, imageTypeOptions);

                if (imageType == null)
                {
                    return ElementId.InvalidElementId;
                }

                try
                {
                    if (!string.IsNullOrWhiteSpace(imageName))
                    {
                        imageType.Name = imageName;
                    }
                }
                catch
                {
                    // Если имя занято, оставляем автогенерированное имя Revit.
                }

                cachedImageTypeIdsByPath[imagePath] = imageType.Id;
                return imageType.Id;
            }
            finally
            {
                imageTypeOptions.Dispose();
            }
        }

        private static ElementId FindExistingImageTypeIdByName(Document document, string imageName)
        {
            if (string.IsNullOrWhiteSpace(imageName))
            {
                return ElementId.InvalidElementId;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(ImageType));

            foreach (Element element in collector)
            {
                ImageType imageType = element as ImageType;

                if (imageType == null)
                {
                    continue;
                }

                if (string.Equals(imageType.Name, imageName, StringComparison.OrdinalIgnoreCase))
                {
                    return imageType.Id;
                }
            }

            return ElementId.InvalidElementId;
        }

        private static ImageTypeOptions BuildImageTypeOptions(string sourcePngPath)
        {
            Type optionsType = typeof(ImageTypeOptions);
            ConstructorInfo constructor = optionsType.GetConstructor(new[] { typeof(string), typeof(bool), typeof(ImageTypeSource) });

            if (constructor != null)
            {
                return constructor.Invoke(new object[] { sourcePngPath, false, ImageTypeSource.Import }) as ImageTypeOptions;
            }

            constructor = optionsType.GetConstructor(new[] { typeof(string), typeof(bool) });

            if (constructor != null)
            {
                return constructor.Invoke(new object[] { sourcePngPath, false }) as ImageTypeOptions;
            }

            constructor = optionsType.GetConstructor(new[] { typeof(string) });

            if (constructor != null)
            {
                return constructor.Invoke(new object[] { sourcePngPath }) as ImageTypeOptions;
            }

            return null;
        }

        // Блок формирования отчета проблемных сопоставлений и ошибок присвоения.
        private string WriteErrorReport(string imageFolderPath, List<SystemTypePieImageLoadIssue> errors)
        {
            if (errors == null || errors.Count == 0)
            {
                return string.Empty;
            }

            string reportPath = Path.Combine(
                imageFolderPath,
                "Проблемные_типы_или_изображения_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture) + ".csv");

            List<string> header = new List<string>
            {
                "Category",
                "Family",
                "TypeName",
                "ImageFile",
                "ErrorText"
            };

            List<List<string>> rows = new List<List<string>>();

            for (int i = 0; i < errors.Count; i++)
            {
                SystemTypePieImageLoadIssue error = errors[i];
                rows.Add(new List<string>
                {
                    error.Category ?? string.Empty,
                    error.Family ?? string.Empty,
                    error.TypeName ?? string.Empty,
                    error.ImageFile ?? string.Empty,
                    error.ErrorText ?? string.Empty
                });
            }

            _csvTableService.Write(reportPath, header, rows);
            return reportPath;
        }

        private class SystemTypeImageTarget
        {
            public ElementType ElementType { get; set; }

            public string Category { get; set; }

            public string Family { get; set; }

            public string TypeName { get; set; }

            public string ExpectedLegendName { get; set; }
        }

    }

    public class SystemTypePieImageLoadResult
    {
        public int TotalTargetTypes { get; set; }

        public int TotalImagesInFolder { get; set; }

        public int MatchedByNameCount { get; set; }

        public int AssignedCount { get; set; }

        public int AlreadyAssignedCount { get; set; }

        public string ReportPath { get; set; }

        public List<SystemTypePieImageLoadIssue> Errors
        {
            get
            {
                return _errors;
            }
            set
            {
                _errors = value ?? new List<SystemTypePieImageLoadIssue>();
            }
        }

        private List<SystemTypePieImageLoadIssue> _errors = new List<SystemTypePieImageLoadIssue>();

        public string BuildSummaryText()
        {
            return
                "Целевых типов: " + TotalTargetTypes + Environment.NewLine +
                "PNG в папке: " + TotalImagesInFolder + Environment.NewLine +
                "Совпадений по именам: " + MatchedByNameCount + Environment.NewLine +
                "Присвоено изображений: " + AssignedCount + Environment.NewLine +
                "Уже было назначено: " + AlreadyAssignedCount + Environment.NewLine +
                "Ошибок: " + Errors.Count;
        }
    }

    public class SystemTypePieImageLoadIssue
    {
        public string Category { get; set; }

        public string Family { get; set; }

        public string TypeName { get; set; }

        public string ImageFile { get; set; }

        public string ErrorText { get; set; }
    }
}
