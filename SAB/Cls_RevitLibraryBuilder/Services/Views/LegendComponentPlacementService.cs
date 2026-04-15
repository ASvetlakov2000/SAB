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
        // Block responsible for fixed placement categories order.
        // The command places items in this exact sequence.
        private static readonly BuiltInCategory[] TargetCategories =
        {
            BuiltInCategory.OST_Ceilings,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Roofs,
            BuiltInCategory.OST_RoofSoffit
        };

        // Block responsible for vertical spacing between copied legend components.
        private const double VerticalOffsetMillimeters = 1000.0;

        /// <summary>
        /// Main placement workflow for active Legend view.
        /// </summary>
        public LegendComponentPlacementResult PlaceByCategories(Document document, View legendView)
        {
            LegendComponentPlacementResult result = new LegendComponentPlacementResult();
            result.RequestedCategoriesCount = TargetCategories.Length;

            // Block responsible for input validation.
            if (document == null)
            {
                result.FatalError = "Document is not available.";
                return result;
            }

            if (legendView == null)
            {
                result.FatalError = "Active view is not available.";
                return result;
            }

            if (legendView.ViewType != ViewType.Legend)
            {
                result.FatalError = "Active view must be a Legend view.";
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
            for (int i = 0; i < TargetCategories.Length; i++)
            {
                BuiltInCategory category = TargetCategories[i];
                string categoryName = GetCategoryDisplayName(category);

                // Step 1: find all candidate types for current category.
                List<ElementId> candidateTypeIds = CollectCategoryTypeIds(document, category);
                result.RequestedTypeCount += candidateTypeIds.Count;

                if (candidateTypeIds.Count == 0)
                {
                    result.AddSkipped(categoryName, "No ElementType found for this category.");
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
                        result.AddSkipped(categoryName, targetTypeName + " => " + copyError);
                        continue;
                    }

                    // Assign exact target type to copied component.
                    string assignError;

                    if (!TryAssignRepresentedType(document, copiedComponentId, targetTypeId, out assignError))
                    {
                        // Block responsible for cleaning failed copy to keep result view predictable.
                        TryDeleteElement(document, copiedComponentId);
                        result.AddSkipped(categoryName, targetTypeName + " => " + assignError);
                        continue;
                    }

                    // Move placement chain forward for next vertical copy.
                    previousComponentId = copiedComponentId;
                    result.PlacedCount++;
                    result.AddPlaced(categoryName, targetTypeName);
                }
            }

            return result;
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
                errorText = "Legend view is not available for template search.";
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

            errorText = "Template legend component was not found on active Legend view. Place one legend component manually and rerun command.";
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
                errorText = "Document is not available for copy operation.";
                return false;
            }

            if (legendViewId == ElementId.InvalidElementId || sourceComponentId == ElementId.InvalidElementId)
            {
                errorText = "Legend view id or source component id is invalid.";
                return false;
            }

            View sourceLegendView = document.GetElement(legendViewId) as View;
            Element sourceComponent = document.GetElement(sourceComponentId);

            if (sourceLegendView == null || sourceLegendView.ViewType != ViewType.Legend)
            {
                errorText = "Source legend view is invalid for copy operation.";
                return false;
            }

            if (!IsLegendComponentOnView(sourceComponent, legendViewId))
            {
                errorText = "Source legend component is invalid or not owned by active Legend view.";
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
                errorText = "Failed to copy legend component: " + exception.Message;
                return false;
            }

            if (copiedIds == null || copiedIds.Count == 0)
            {
                errorText = "Copy operation returned no new elements.";
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

            errorText = "Copied legend component was not found among returned copied ids.";
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
                errorText = "Not enough data to assign represented type.";
                return false;
            }

            if (!legendComponent.IsValidObject)
            {
                errorText = "Legend component became invalid before type assignment.";
                return false;
            }

            ElementType targetType = document.GetElement(targetTypeId) as ElementType;

            if (targetType == null || !targetType.IsValidObject)
            {
                errorText = "Target ElementType is missing or invalid. TypeId=" + targetTypeId.IntegerValue;
                return false;
            }

            Parameter representedParameter = legendComponent.get_Parameter(BuiltInParameter.LEGEND_COMPONENT);

            if (representedParameter == null)
            {
                errorText = "Parameter LEGEND_COMPONENT was not found.";
                return false;
            }

            if (representedParameter.IsReadOnly)
            {
                errorText = "Parameter LEGEND_COMPONENT is read-only.";
                return false;
            }

            if (representedParameter.StorageType != StorageType.ElementId &&
                representedParameter.StorageType != StorageType.Integer)
            {
                errorText = "Unsupported storage type of LEGEND_COMPONENT parameter.";
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

                errorText = "Assigned type was not applied to LEGEND_COMPONENT parameter.";
                return false;
            }
            catch (Exception exception)
            {
                errorText = "Failed to assign represented type: " + exception.Message;
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
                errorText = "Copied legend component was not found before type assignment.";
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
                return "UnknownType(" + typeId.IntegerValue + ")";
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

        public int RequestedCategoriesCount { get; set; }

        public int RequestedTypeCount { get; set; }

        public int PlacedCount { get; set; }

        public string FatalError { get; set; }

        public IReadOnlyList<string> PlacedDetails
        {
            get { return _placed.AsReadOnly(); }
        }

        public IReadOnlyList<string> SkippedDetails
        {
            get { return _skipped.AsReadOnly(); }
        }

        public void AddPlaced(string categoryName, string typeDisplayName)
        {
            _placed.Add("[" + categoryName + "] " + typeDisplayName);
        }

        public void AddSkipped(string categoryName, string reason)
        {
            _skipped.Add("[" + categoryName + "] " + reason);
        }

        public string BuildSummaryText()
        {
            string summary =
                "Requested categories: " + RequestedCategoriesCount +
                "\nRequested types: " + RequestedTypeCount +
                "\nPlaced components: " + PlacedCount +
                "\nSkipped items: " + _skipped.Count;

            if (_skipped.Count > 0)
            {
                summary += "\n\nSkip details:";

                for (int i = 0; i < _skipped.Count; i++)
                {
                    summary += "\n" + (i + 1) + ". " + _skipped[i];
                }
            }

            return summary;
        }
    }
}
