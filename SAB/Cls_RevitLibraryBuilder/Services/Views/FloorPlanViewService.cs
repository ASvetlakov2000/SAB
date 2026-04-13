using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Autodesk.Revit.DB;

namespace RevitLibraryBuilder.Services.Views
{
    public class FloorPlanViewService
    {
        private const string ViewPrefix = "Библиотека_";
        private static readonly char[] ProhibitedViewNameCharacters = new char[] { '{', '}', '[', ']', ';', '<', '>', '?', '^', '~' };
        private static readonly Dictionary<string, string> CategoryTranslations =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Doors", "Двери" },
                { "Windows", "Окна" },
                { "Generic Models", "Общие модели" },
                { "Walls", "Стены" },
                { "Floors", "Перекрытия" },
                { "Ceilings", "Потолки" },
                { "Roofs", "Крыши" },
                { "Columns", "Колонны" },
                { "Structural Columns", "Несущие колонны" },
                { "Structural Framing", "Несущие конструкции" },
                { "Furniture", "Мебель" },
                { "Plumbing Fixtures", "Сантехнические приборы" },
                { "Electrical Fixtures", "Электрооборудование" }
            };

        public ViewPlan Create(Document document, string baseViewName)
        {
            return Create(document, baseViewName, document != null ? document.ActiveView : null);
        }

        public ViewPlan CreateByCategory(
            Document document,
            string categoryNameFromCsv,
            View sourceView)
        {
            return CreateByCategory(document, categoryNameFromCsv, sourceView, null, null, 0);
        }

        public ViewPlan CreateByCategory(
            Document document,
            string categoryNameFromCsv,
            View sourceView,
            string sourceCsvFilePath,
            string typeNameOriginal,
            int rowIndex)
        {
            NameResolutionResult naming = BuildAndSanitizeViewName(categoryNameFromCsv);

            if (string.IsNullOrWhiteSpace(naming.SanitizedViewName))
            {
                if (naming.HasInvalidCharacters)
                {
                    WriteInvalidNameReport(
                        sourceCsvFilePath,
                        rowIndex,
                        categoryNameFromCsv,
                        typeNameOriginal,
                        naming.GeneratedViewNameOriginal,
                        naming.SanitizedViewName,
                        naming.InvalidCharactersFound,
                        "Sanitized view name is empty");
                }

                return null;
            }

            ViewPlan viewPlan = Create(document, naming.SanitizedViewName, sourceView);
            ApplyCategoryVisibilityRules(document, viewPlan, categoryNameFromCsv);

            if (naming.HasInvalidCharacters)
            {
                WriteInvalidNameReport(
                    sourceCsvFilePath,
                    rowIndex,
                    categoryNameFromCsv,
                    typeNameOriginal,
                    naming.GeneratedViewNameOriginal,
                    naming.SanitizedViewName,
                    naming.InvalidCharactersFound,
                    "Processed with sanitized name");
            }

            return viewPlan;
        }

        public ViewPlan Create(Document document, string baseViewName, View sourceView)
        {
            if (document == null)
            {
                throw new ArgumentNullException("document");
            }

            if (string.IsNullOrWhiteSpace(baseViewName))
            {
                throw new ArgumentException("View name cannot be empty.", "baseViewName");
            }

            Level targetLevel = GetTargetLevel(document, sourceView);

            if (targetLevel == null)
            {
                return null;
            }

            ViewFamilyType floorPlanType = GetFloorPlanViewFamilyType(document);

            if (floorPlanType == null)
            {
                return null;
            }

            string uniqueViewName = GetUniqueViewName(document, baseViewName);

            // Block responsible for creating a new floor plan view on the selected level
            ViewPlan viewPlan = ViewPlan.Create(document, floorPlanType.Id, targetLevel.Id);

            if (viewPlan == null)
            {
                return null;
            }

            // Block responsible for applying the final unique name
            viewPlan.Name = uniqueViewName;

            return viewPlan;
        }

        // Block responsible for generating Floor Plan name by category
        public static string BuildViewNameByCategory(string categoryNameFromCsv)
        {
            NameResolutionResult result = BuildAndSanitizeViewName(categoryNameFromCsv);
            return result.SanitizedViewName;
        }

        // Block responsible for translating and sanitizing generated Revit view name
        public static string SanitizeViewName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string result = value.Trim();

            for (int i = 0; i < ProhibitedViewNameCharacters.Length; i++)
            {
                result = result.Replace(ProhibitedViewNameCharacters[i].ToString(), "_");
            }

            while (result.Contains("__"))
            {
                result = result.Replace("__", "_");
            }

            return result.Trim();
        }

        private static NameResolutionResult BuildAndSanitizeViewName(string categoryNameFromCsv)
        {
            string translatedCategory = TranslateCategoryToRussian(categoryNameFromCsv);

            if (string.IsNullOrWhiteSpace(translatedCategory))
            {
                translatedCategory = "Без категории";
            }

            string originalGenerated = ViewPrefix + translatedCategory;
            string invalidCharactersFound = GetInvalidCharactersFound(originalGenerated);
            string sanitized = SanitizeViewName(originalGenerated);

            NameResolutionResult result = new NameResolutionResult();
            result.GeneratedViewNameOriginal = originalGenerated;
            result.SanitizedViewName = sanitized;
            result.InvalidCharactersFound = invalidCharactersFound;
            result.HasInvalidCharacters = !string.IsNullOrWhiteSpace(invalidCharactersFound);

            return result;
        }

        private static string TranslateCategoryToRussian(string categoryNameFromCsv)
        {
            if (string.IsNullOrWhiteSpace(categoryNameFromCsv))
            {
                return string.Empty;
            }

            string normalized = categoryNameFromCsv.Trim();
            string translated;

            if (CategoryTranslations.TryGetValue(normalized, out translated))
            {
                return translated;
            }

            return normalized;
        }

        private static string GetInvalidCharactersFound(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            StringBuilder found = new StringBuilder();

            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];

                if (!IsProhibitedCharacter(character))
                {
                    continue;
                }

                if (found.ToString().IndexOf(character) >= 0)
                {
                    continue;
                }

                found.Append(character);
            }

            return found.ToString();
        }

        private static bool IsProhibitedCharacter(char character)
        {
            for (int i = 0; i < ProhibitedViewNameCharacters.Length; i++)
            {
                if (ProhibitedViewNameCharacters[i] == character)
                {
                    return true;
                }
            }

            return false;
        }

        private static void WriteInvalidNameReport(
            string sourceCsvFilePath,
            int rowIndex,
            string categoryNameOriginal,
            string typeNameOriginal,
            string generatedViewNameOriginal,
            string sanitizedViewName,
            string invalidCharactersFound,
            string skipReason)
        {
            if (string.IsNullOrWhiteSpace(sourceCsvFilePath))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(invalidCharactersFound))
            {
                return;
            }

            try
            {
                string sourceFolder = Path.GetDirectoryName(sourceCsvFilePath);

                if (string.IsNullOrWhiteSpace(sourceFolder))
                {
                    return;
                }

                string categoryForFileName = TranslateCategoryToRussian(categoryNameOriginal);

                if (string.IsNullOrWhiteSpace(categoryForFileName))
                {
                    categoryForFileName = "Категория";
                }

                string safeCategoryForFileName = SanitizeFileNamePart(categoryForFileName);
                string reportFileName = "Проблемные наименования_" + safeCategoryForFileName + ".csv";
                string reportPath = Path.Combine(sourceFolder, reportFileName);

                StringBuilder builder = new StringBuilder();
                builder.AppendLine("RowIndex,CategoryNameOriginal,TypeNameOriginal,GeneratedViewNameOriginal,SanitizedViewName,InvalidCharactersFound,SkipReason");
                builder.AppendLine(
                    rowIndex + "," +
                    EscapeCsv(categoryNameOriginal) + "," +
                    EscapeCsv(typeNameOriginal) + "," +
                    EscapeCsv(generatedViewNameOriginal) + "," +
                    EscapeCsv(sanitizedViewName) + "," +
                    EscapeCsv(invalidCharactersFound) + "," +
                    EscapeCsv(skipReason));

                File.WriteAllText(reportPath, builder.ToString(), Encoding.UTF8);
            }
            catch
            {
            }
        }

        private static string SanitizeFileNamePart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Категория";
            }

            string result = value.Trim();
            char[] invalidFileNameChars = Path.GetInvalidFileNameChars();

            for (int i = 0; i < invalidFileNameChars.Length; i++)
            {
                result = result.Replace(invalidFileNameChars[i].ToString(), "_");
            }

            while (result.Contains("__"))
            {
                result = result.Replace("__", "_");
            }

            return result.Trim();
        }

        private static string EscapeCsv(string value)
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

        // Block responsible for resolving the placed category for view visibility
        private static void ApplyCategoryVisibilityRules(Document document, ViewPlan viewPlan, string categoryNameFromCsv)
        {
            if (document == null || viewPlan == null)
            {
                return;
            }

            BuiltInCategory? mainCategory = ResolveMainCategory(document, categoryNameFromCsv);

            if (!mainCategory.HasValue)
            {
                return;
            }

            HashSet<int> allowedCategoryIds = BuildAllowedCategoryIds(document, mainCategory.Value);

            // Block responsible for applying category visibility rules to the created Floor Plan view
            Categories categories = document.Settings.Categories;

            if (categories == null)
            {
                return;
            }

            foreach (Category category in categories)
            {
                if (category == null)
                {
                    continue;
                }

                // Block responsible for excluding Annotation / Analytical / Imported / Filters / Linked Files from visibility processing
                if (!IsModelCategoryForVisibilityProcessing(category))
                {
                    continue;
                }

                if (!viewPlan.CanCategoryBeHidden(category.Id))
                {
                    continue;
                }

                bool shouldBeVisible = allowedCategoryIds.Contains(category.Id.IntegerValue);

                try
                {
                    viewPlan.SetCategoryHidden(category.Id, !shouldBeVisible);
                }
                catch
                {
                    // Some categories cannot be controlled for visibility in this view context.
                }
            }
        }

        private static HashSet<int> BuildAllowedCategoryIds(Document document, BuiltInCategory mainCategory)
        {
            HashSet<int> allowedCategoryIds = new HashSet<int>();
            Category mainRevitCategory = Category.GetCategory(document, mainCategory);

            if (mainRevitCategory != null)
            {
                allowedCategoryIds.Add(mainRevitCategory.Id.IntegerValue);
            }

            // Block responsible for Windows/Doors special exception with Walls visibility
            if (mainCategory == BuiltInCategory.OST_Windows || mainCategory == BuiltInCategory.OST_Doors)
            {
                Category wallsCategory = Category.GetCategory(document, BuiltInCategory.OST_Walls);

                if (wallsCategory != null)
                {
                    allowedCategoryIds.Add(wallsCategory.Id.IntegerValue);
                }
            }

            return allowedCategoryIds;
        }

        private static bool IsModelCategoryForVisibilityProcessing(Category category)
        {
            if (category == null)
            {
                return false;
            }

            if (category.CategoryType != CategoryType.Model)
            {
                return false;
            }

            string categoryName = category.Name;
            string parentCategoryName = category.Parent != null ? category.Parent.Name : string.Empty;

            if (ContainsImportedCategoryMarker(categoryName) || ContainsImportedCategoryMarker(parentCategoryName))
            {
                return false;
            }

            if (ContainsLinkedCategoryMarker(categoryName) || ContainsLinkedCategoryMarker(parentCategoryName))
            {
                return false;
            }

            return true;
        }

        private static bool ContainsImportedCategoryMarker(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = NormalizeCategoryName(value);

            if (normalized.Contains("IMPORT"))
            {
                return true;
            }

            if (normalized.Contains("ИМПОРТ"))
            {
                return true;
            }

            return false;
        }

        private static bool ContainsLinkedCategoryMarker(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string normalized = NormalizeCategoryName(value);

            if (normalized.Contains("REVIT LINK"))
            {
                return true;
            }

            if (normalized.Contains("RVT LINK"))
            {
                return true;
            }

            if (normalized.Contains("СВЯЗИ REVIT"))
            {
                return true;
            }

            return false;
        }

        private static BuiltInCategory? ResolveMainCategory(Document document, string categoryNameFromCsv)
        {
            if (string.IsNullOrWhiteSpace(categoryNameFromCsv))
            {
                return null;
            }

            string normalizedCategoryName = NormalizeCategoryName(categoryNameFromCsv);
            Dictionary<string, BuiltInCategory> map = BuildCategoryMap();
            BuiltInCategory mappedCategory;

            if (map.TryGetValue(normalizedCategoryName, out mappedCategory))
            {
                return mappedCategory;
            }

            Categories categories = document.Settings.Categories;

            if (categories == null)
            {
                return null;
            }

            foreach (Category category in categories)
            {
                if (category == null || string.IsNullOrWhiteSpace(category.Name))
                {
                    continue;
                }

                if (!string.Equals(
                    NormalizeCategoryName(category.Name),
                    normalizedCategoryName,
                    StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                int categoryIdValue = category.Id.IntegerValue;

                if (!Enum.IsDefined(typeof(BuiltInCategory), categoryIdValue))
                {
                    return null;
                }

                return (BuiltInCategory)categoryIdValue;
            }

            return null;
        }

        private static Dictionary<string, BuiltInCategory> BuildCategoryMap()
        {
            Dictionary<string, BuiltInCategory> map = new Dictionary<string, BuiltInCategory>(StringComparer.OrdinalIgnoreCase);
            map[NormalizeCategoryName("Doors")] = BuiltInCategory.OST_Doors;
            map[NormalizeCategoryName("Двери")] = BuiltInCategory.OST_Doors;
            map[NormalizeCategoryName("Windows")] = BuiltInCategory.OST_Windows;
            map[NormalizeCategoryName("Окна")] = BuiltInCategory.OST_Windows;
            map[NormalizeCategoryName("Walls")] = BuiltInCategory.OST_Walls;
            map[NormalizeCategoryName("Стены")] = BuiltInCategory.OST_Walls;
            map[NormalizeCategoryName("Generic Models")] = BuiltInCategory.OST_GenericModel;
            map[NormalizeCategoryName("Общие модели")] = BuiltInCategory.OST_GenericModel;
            map[NormalizeCategoryName("Furniture")] = BuiltInCategory.OST_Furniture;
            map[NormalizeCategoryName("Мебель")] = BuiltInCategory.OST_Furniture;
            map[NormalizeCategoryName("Floors")] = BuiltInCategory.OST_Floors;
            map[NormalizeCategoryName("Перекрытия")] = BuiltInCategory.OST_Floors;
            map[NormalizeCategoryName("Ceilings")] = BuiltInCategory.OST_Ceilings;
            map[NormalizeCategoryName("Потолки")] = BuiltInCategory.OST_Ceilings;
            map[NormalizeCategoryName("Roofs")] = BuiltInCategory.OST_Roofs;
            map[NormalizeCategoryName("Крыши")] = BuiltInCategory.OST_Roofs;
            map[NormalizeCategoryName("Columns")] = BuiltInCategory.OST_Columns;
            map[NormalizeCategoryName("Колонны")] = BuiltInCategory.OST_Columns;
            map[NormalizeCategoryName("Structural Columns")] = BuiltInCategory.OST_StructuralColumns;
            map[NormalizeCategoryName("Несущие колонны")] = BuiltInCategory.OST_StructuralColumns;
            map[NormalizeCategoryName("Plumbing Fixtures")] = BuiltInCategory.OST_PlumbingFixtures;
            map[NormalizeCategoryName("Сантехнические приборы")] = BuiltInCategory.OST_PlumbingFixtures;
            map[NormalizeCategoryName("Electrical Fixtures")] = BuiltInCategory.OST_ElectricalFixtures;
            map[NormalizeCategoryName("Электрооборудование")] = BuiltInCategory.OST_ElectricalFixtures;

            return map;
        }

        private static string NormalizeCategoryName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string result = value.Trim();
            while (result.Contains("  "))
            {
                result = result.Replace("  ", " ");
            }

            return result.ToUpperInvariant();
        }

        // Block responsible for selecting the most suitable level for the new view
        private static Level GetTargetLevel(Document document, View sourceView)
        {
            if (sourceView != null)
            {
                ViewPlan sourcePlanView = sourceView as ViewPlan;

                if (sourcePlanView != null && sourcePlanView.GenLevel != null)
                {
                    return sourcePlanView.GenLevel;
                }

                Parameter levelParameter = sourceView.get_Parameter(BuiltInParameter.PLAN_VIEW_LEVEL);

                if (levelParameter != null && levelParameter.HasValue)
                {
                    ElementId levelId = levelParameter.AsElementId();

                    if (levelId != ElementId.InvalidElementId)
                    {
                        Level levelFromView = document.GetElement(levelId) as Level;

                        if (levelFromView != null)
                        {
                            return levelFromView;
                        }
                    }
                }
            }

            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(Level));

            Level firstLevel = null;

            foreach (Element element in collector)
            {
                Level level = element as Level;

                if (level == null)
                {
                    continue;
                }

                if (firstLevel == null)
                {
                    firstLevel = level;
                }

                if (Math.Abs(level.Elevation) < 0.0001)
                {
                    return level;
                }
            }

            return firstLevel;
        }

        private static ViewFamilyType GetFloorPlanViewFamilyType(Document document)
        {
            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(ViewFamilyType));

            foreach (Element element in collector)
            {
                ViewFamilyType viewFamilyType = element as ViewFamilyType;

                if (viewFamilyType != null && viewFamilyType.ViewFamily == ViewFamily.FloorPlan)
                {
                    return viewFamilyType;
                }
            }

            return null;
        }

        // Block responsible for preventing duplicate Revit view names
        private static string GetUniqueViewName(Document document, string baseViewName)
        {
            string candidateName = baseViewName.Trim();
            int index = 1;

            while (ViewNameExists(document, candidateName))
            {
                candidateName = baseViewName.Trim() + " " + index;
                index++;
            }

            return candidateName;
        }

        private static bool ViewNameExists(Document document, string viewName)
        {
            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(View));

            foreach (Element element in collector)
            {
                View view = element as View;

                if (view == null || view.IsTemplate)
                {
                    continue;
                }

                if (string.Equals(view.Name, viewName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private class NameResolutionResult
        {
            public string GeneratedViewNameOriginal { get; set; }

            public string SanitizedViewName { get; set; }

            public string InvalidCharactersFound { get; set; }

            public bool HasInvalidCharacters { get; set; }
        }
    }
}
