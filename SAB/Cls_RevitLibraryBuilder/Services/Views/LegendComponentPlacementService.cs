using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace RevitLibraryBuilder.Services.Views
{
    /// <summary>
    /// Service responsible for placing legend components by predefined categories
    /// on a single active Legend view.
    /// </summary>
    public class LegendComponentPlacementService
    {
        // Block responsible for vertical spacing between copied legend components.
        private const double VerticalOffsetMillimeters = 1000.0;

        /// <summary>
        /// Main placement workflow for active Legend view.
        /// </summary>
        public LegendComponentPlacementResult PlaceByCategories(Document document, View legendView)
        {
            LegendComponentPlacementResult result = new LegendComponentPlacementResult();
            List<BuiltInCategory> targetCategories = GetTargetCategories();
            result.RequestedCategoriesCount = targetCategories.Count;

            // Block responsible for input validation.
            if (document == null)
            {
                result.FatalError = "Документ недоступен.";
                return result;
            }

            if (legendView == null)
            {
                result.FatalError = "Активный вид недоступен.";
                return result;
            }

            if (legendView.ViewType != ViewType.Legend)
            {
                result.FatalError = "Активный вид должен быть типа Легенда.";
                return result;
            }

            // Block responsible for finding user-provided template component on active legend.
            Element templateComponent;
            string templateError;

            if (!TryFindTemplateComponent(document, legendView.Id, out templateComponent, out templateError))
            {
                result.FatalError = templateError;
                return result;
            }

            ElementId previousComponentId = templateComponent.Id;
            double offsetInternal = ConvertMillimetersToInternalUnits(VerticalOffsetMillimeters);
            XYZ downTranslation = new XYZ(0, -offsetInternal, 0);

            // Block responsible for iterative placement by predefined categories.
            // Rule: place all types of current category, then move to next category.
            for (int i = 0; i < targetCategories.Count; i++)
            {
                BuiltInCategory category = targetCategories[i];
                string categoryName = GetCategoryDisplayName(category);

                // Step 1: find all candidate types for current category.
                List<ElementId> candidateTypeIds = CollectCategoryTypeIds(document, category);
                result.RequestedTypeCount += candidateTypeIds.Count;

                if (candidateTypeIds.Count == 0)
                {
                    result.AddSkipped(categoryName, string.Empty, "Для этой категории не найдено ни одного типоразмера.");
                    continue;
                }

                // Step 2: place every type in this category one-by-one.
                for (int typeIndex = 0; typeIndex < candidateTypeIds.Count; typeIndex++)
                {
                    ElementId targetTypeId = candidateTypeIds[typeIndex];
                    string targetTypeName = GetElementTypeDisplayName(document, targetTypeId);

                    // Copy previous legend component down by 1000 mm.
                    ElementId copiedComponentId;
                    string copyError;

                    if (!TryCopyLegendComponent(
                            document,
                            legendView.Id,
                            previousComponentId,
                            downTranslation,
                            out copiedComponentId,
                            out copyError))
                    {
                        result.AddSkipped(categoryName, targetTypeName, copyError);
                        continue;
                    }

                    // Assign exact target type to copied component.
                    string assignError;

                    if (!TryAssignRepresentedType(document, copiedComponentId, targetTypeId, out assignError))
                    {
                        // Block responsible for cleaning failed copy to keep result view predictable.
                        TryDeleteElement(document, copiedComponentId);
                        result.AddSkipped(categoryName, targetTypeName, assignError);
                        continue;
                    }

                    // Move placement chain forward for next vertical copy.
                    previousComponentId = copiedComponentId;
                    result.AddPlaced(categoryName, targetTypeName);
                }
            }

            return result;
        }

        /// <summary>
        /// Builds target categories at runtime to avoid static array initialization issues.
        /// </summary>
        private static List<BuiltInCategory> GetTargetCategories()
        {
            List<BuiltInCategory> categories = new List<BuiltInCategory>();

            // Block responsible for safe category initialization.
            // Category names are parsed dynamically to avoid RuntimeHelpers.InitializeArray problems.
            string[] categoryNames =
            {
                "OST_Ceilings",
                "OST_Floors",
                "OST_Walls",
                "OST_Roofs",
                "OST_RoofSoffit"
            };

            for (int i = 0; i < categoryNames.Length; i++)
            {
                BuiltInCategory category;

                if (!Enum.TryParse(categoryNames[i], out category))
                {
                    continue;
                }

                if (!Enum.IsDefined(typeof(BuiltInCategory), category))
                {
                    continue;
                }

                categories.Add(category);
            }

            return categories;
        }

        /// <summary>
        /// Finds first legend component placed on active legend view.
        /// </summary>
        private static bool TryFindTemplateComponent(
            Document document,
            ElementId legendViewId,
            out Element templateComponent,
            out string errorText)
        {
            templateComponent = null;
            errorText = string.Empty;

            if (document == null || legendViewId == ElementId.InvalidElementId)
            {
                errorText = "Вид легенды недоступен для поиска шаблонного компонента.";
                return false;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document, legendViewId);
            collector.OfCategory(BuiltInCategory.OST_LegendComponents);
            collector.WhereElementIsNotElementType();

            foreach (Element element in collector)
            {
                if (!IsLegendComponentOnView(element, legendViewId))
                {
                    continue;
                }

                templateComponent = element;
                return true;
            }

            errorText = "На активном виде Легенда не найден шаблонный компонент. Разместите один компонент вручную и запустите команду повторно.";
            return false;
        }

        /// <summary>
        /// Copies source legend component within same legend view by provided translation.
        /// </summary>
        private static bool TryCopyLegendComponent(
            Document document,
            ElementId legendViewId,
            ElementId sourceComponentId,
            XYZ translation,
            out ElementId copiedComponentId,
            out string errorText)
        {
            copiedComponentId = ElementId.InvalidElementId;
            errorText = string.Empty;

            if (document == null)
            {
                errorText = "Документ недоступен для операции копирования.";
                return false;
            }

            if (legendViewId == ElementId.InvalidElementId || sourceComponentId == ElementId.InvalidElementId)
            {
                errorText = "Некорректный идентификатор вида легенды или исходного компонента.";
                return false;
            }

            View sourceLegendView = document.GetElement(legendViewId) as View;
            Element sourceComponent = document.GetElement(sourceComponentId);

            if (sourceLegendView == null || sourceLegendView.ViewType != ViewType.Legend)
            {
                errorText = "Исходный вид легенды недействителен для операции копирования.";
                return false;
            }

            if (!IsLegendComponentOnView(sourceComponent, legendViewId))
            {
                errorText = "Исходный компонент легенды недействителен или не принадлежит активному виду Легенда.";
                return false;
            }

            ICollection<ElementId> copiedIds;

            try
            {
                copiedIds = ElementTransformUtils.CopyElements(
                    sourceLegendView,
                    new List<ElementId> { sourceComponentId },
                    sourceLegendView,
                    Transform.CreateTranslation(translation),
                    new CopyPasteOptions());
            }
            catch (Exception exception)
            {
                errorText = "Не удалось скопировать компонент легенды: " + exception.Message;
                return false;
            }

            if (copiedIds == null || copiedIds.Count == 0)
            {
                errorText = "Операция копирования не вернула новых элементов.";
                return false;
            }

            // Block responsible for selecting new legend component id from copy result.
            foreach (ElementId copiedId in copiedIds)
            {
                if (copiedId == ElementId.InvalidElementId || copiedId == sourceComponentId)
                {
                    continue;
                }

                Element copiedElement = document.GetElement(copiedId);

                if (!IsLegendComponentOnView(copiedElement, legendViewId))
                {
                    continue;
                }

                copiedComponentId = copiedId;
                return true;
            }

            errorText = "Скопированный компонент легенды не найден среди возвращенных идентификаторов.";
            return false;
        }

        /// <summary>
        /// Assigns exact represented type to legend component.
        /// </summary>
        private static bool TryAssignRepresentedType(
            Document document,
            Element legendComponent,
            ElementId targetTypeId,
            out string errorText)
        {
            errorText = string.Empty;

            if (document == null || legendComponent == null || targetTypeId == ElementId.InvalidElementId)
            {
                errorText = "Недостаточно данных для назначения представляемого типоразмера.";
                return false;
            }

            if (!legendComponent.IsValidObject)
            {
                errorText = "Компонент легенды стал недействителен до назначения типоразмера.";
                return false;
            }

            ElementType targetType = document.GetElement(targetTypeId) as ElementType;

            if (targetType == null || !targetType.IsValidObject)
            {
                errorText = "Целевой типоразмер отсутствует или недействителен. TypeId=" + targetTypeId.IntegerValue;
                return false;
            }

            Parameter representedParameter = legendComponent.get_Parameter(BuiltInParameter.LEGEND_COMPONENT);

            if (representedParameter == null)
            {
                errorText = "Параметр LEGEND_COMPONENT не найден.";
                return false;
            }

            if (representedParameter.IsReadOnly)
            {
                errorText = "Параметр LEGEND_COMPONENT доступен только для чтения.";
                return false;
            }

            if (representedParameter.StorageType != StorageType.ElementId &&
                representedParameter.StorageType != StorageType.Integer)
            {
                errorText = "Неподдерживаемый тип хранения параметра LEGEND_COMPONENT.";
                return false;
            }

            try
            {
                if (representedParameter.StorageType == StorageType.ElementId)
                {
                    representedParameter.Set(targetTypeId);
                }
                else
                {
                    representedParameter.Set(targetTypeId.IntegerValue);
                }

                document.Regenerate();

                if (IsRepresentedTypeApplied(document, legendComponent.Id, targetTypeId))
                {
                    return true;
                }

                errorText = "Назначенный тип не применился к параметру LEGEND_COMPONENT.";
                return false;
            }
            catch (Exception exception)
            {
                errorText = "Не удалось назначить представляемый тип: " + exception.Message;
                return false;
            }
        }

        private static bool TryAssignRepresentedType(
            Document document,
            ElementId legendComponentId,
            ElementId targetTypeId,
            out string errorText)
        {
            errorText = string.Empty;

            Element legendComponent = document.GetElement(legendComponentId);

            if (legendComponent == null)
            {
                errorText = "Скопированный компонент легенды не найден перед назначением типа.";
                return false;
            }

            return TryAssignRepresentedType(document, legendComponent, targetTypeId, out errorText);
        }

        /// <summary>
        /// Verifies that parameter value has actually been applied after assignment.
        /// </summary>
        private static bool IsRepresentedTypeApplied(Document document, ElementId legendComponentId, ElementId targetTypeId)
        {
            Element component = document.GetElement(legendComponentId);

            if (component == null || !component.IsValidObject)
            {
                return false;
            }

            Parameter parameter = component.get_Parameter(BuiltInParameter.LEGEND_COMPONENT);

            if (parameter == null)
            {
                return false;
            }

            if (parameter.StorageType == StorageType.ElementId)
            {
                return parameter.AsElementId() == targetTypeId;
            }

            if (parameter.StorageType == StorageType.Integer)
            {
                return parameter.AsInteger() == targetTypeId.IntegerValue;
            }

            return false;
        }

        /// <summary>
        /// Collects all type ids for provided built-in category.
        /// </summary>
        private static List<ElementId> CollectCategoryTypeIds(Document document, BuiltInCategory category)
        {
            List<ElementId> result = new List<ElementId>();

            if (document == null)
            {
                return result;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(ElementType));

            foreach (Element element in collector)
            {
                ElementType type = element as ElementType;

                if (type == null)
                {
                    continue;
                }

                Category typeCategory = type.Category;

                if (typeCategory == null)
                {
                    continue;
                }

                if (typeCategory.Id.IntegerValue != (int)category)
                {
                    continue;
                }

                if (type.Id == ElementId.InvalidElementId)
                {
                    continue;
                }

                // Block responsible for skipping in-place model families.
                // In-place types are not required for legend export/placement pipeline.
                FamilySymbol familySymbol = type as FamilySymbol;

                if (familySymbol != null)
                {
                    Family family = familySymbol.Family;

                    if (family != null && family.IsInPlace)
                    {
                        continue;
                    }
                }

                result.Add(type.Id);
            }

            result.Sort(delegate (ElementId left, ElementId right)
            {
                string leftName = GetElementTypeDisplayName(document, left);
                string rightName = GetElementTypeDisplayName(document, right);
                return string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
            });

            return result;
        }

        /// <summary>
        /// Checks that element is a valid legend component owned by specified view.
        /// </summary>
        private static bool IsLegendComponentOnView(Element element, ElementId viewId)
        {
            if (element == null || !element.IsValidObject)
            {
                return false;
            }

            if (element.OwnerViewId != viewId)
            {
                return false;
            }

            if (element.Category == null)
            {
                return false;
            }

            return element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_LegendComponents;
        }

        /// <summary>
        /// Safe helper for deleting temporary failed placement element.
        /// </summary>
        private static void TryDeleteElement(Document document, ElementId elementId)
        {
            if (document == null || elementId == ElementId.InvalidElementId)
            {
                return;
            }

            try
            {
                document.Delete(elementId);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Converts millimeters to internal Revit units.
        /// </summary>
        private static double ConvertMillimetersToInternalUnits(double millimeters)
        {
            return UnitUtils.ConvertToInternalUnits(millimeters, UnitTypeId.Millimeters);
        }

        /// <summary>
        /// Friendly category caption for report and notifications.
        /// </summary>
        private static string GetCategoryDisplayName(BuiltInCategory category)
        {
            try
            {
                return LabelUtils.GetLabelFor(category);
            }
            catch
            {
                return category.ToString();
            }
        }

        /// <summary>
        /// Friendly type caption for report and notifications.
        /// </summary>
        private static string GetElementTypeDisplayName(Document document, ElementId typeId)
        {
            ElementType type = document.GetElement(typeId) as ElementType;

            if (type == null)
            {
                return "НеизвестныйТип(" + typeId.IntegerValue + ")";
            }

            string familyName = type.FamilyName ?? string.Empty;
            string typeName = type.Name ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(familyName) && !string.IsNullOrWhiteSpace(typeName))
            {
                return familyName + " | " + typeName;
            }

            if (!string.IsNullOrWhiteSpace(typeName))
            {
                return typeName;
            }

            return typeId.IntegerValue.ToString();
        }
    }

    /// <summary>
    /// Placement result DTO for command notification and diagnostics.
    /// </summary>
    public class LegendComponentPlacementResult
    {
        private readonly List<string> _placed = new List<string>();
        private readonly List<string> _skipped = new List<string>();
        private readonly List<LegendComponentPlacementIssue> _issues = new List<LegendComponentPlacementIssue>();

        public int RequestedCategoriesCount { get; set; }

        public int RequestedTypeCount { get; set; }

        public int PlacedCount
        {
            get { return _placed.Count; }
        }

        public string FatalError { get; set; }

        public IReadOnlyList<string> PlacedDetails
        {
            get { return _placed.AsReadOnly(); }
        }

        public IReadOnlyList<string> SkippedDetails
        {
            get { return _skipped.AsReadOnly(); }
        }

        public IReadOnlyList<LegendComponentPlacementIssue> Issues
        {
            get { return _issues.AsReadOnly(); }
        }

        public void AddPlaced(string categoryName, string typeDisplayName)
        {
            _placed.Add("[" + categoryName + "] " + typeDisplayName);
        }

        public void AddSkipped(string categoryName, string typeDisplayName, string reason)
        {
            string safeCategory = categoryName ?? string.Empty;
            string safeTypeName = typeDisplayName ?? string.Empty;
            string safeReason = reason ?? string.Empty;

            string formatted = "[" + safeCategory + "] ";

            if (!string.IsNullOrWhiteSpace(safeTypeName))
            {
                formatted += safeTypeName + " => ";
            }

            formatted += safeReason;
            _skipped.Add(formatted);

            _issues.Add(new LegendComponentPlacementIssue
            {
                Category = safeCategory,
                TypeName = safeTypeName,
                ErrorText = safeReason
            });
        }

        public string BuildSummaryText()
        {
            string summary =
                "Категорий в обработке: " + RequestedCategoriesCount +
                "\nТипов в обработке: " + RequestedTypeCount +
                "\nРасставлено компонентов: " + PlacedCount +
                "\nПропущено: " + _skipped.Count;

            if (_skipped.Count > 0)
            {
                summary += "\n\nПричины пропуска:";

                for (int i = 0; i < _skipped.Count; i++)
                {
                    summary += "\n" + (i + 1) + ". " + _skipped[i];
                }
            }

            return summary;
        }
    }

    /// <summary>
    /// Report row DTO for failed placement diagnostics.
    /// </summary>
    public class LegendComponentPlacementIssue
    {
        public string Category { get; set; }

        public string TypeName { get; set; }

        public string ErrorText { get; set; }
    }
}
