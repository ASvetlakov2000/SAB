using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitLibraryBuilder.Services.Revit
{
    public class TypeAndFamilyDeletionService
    {
        public DeletionResult Delete(
            Document document,
            IList<ElementId> instanceIds,
            IList<ElementId> typeIds,
            IList<ElementId> familyIds,
            IList<ElementId> lineStyleIds,
            IList<ElementId> graphicsStyleIds,
            IList<ElementId> fillPatternIds)
        {
            DeletionResult result = new DeletionResult();

            if (document == null)
            {
                result.Errors.Add("Документ недоступен.");
                return result;
            }

            if (instanceIds == null) instanceIds = new List<ElementId>();
            if (typeIds == null) typeIds = new List<ElementId>();
            if (familyIds == null) familyIds = new List<ElementId>();
            if (lineStyleIds == null) lineStyleIds = new List<ElementId>();
            if (graphicsStyleIds == null) graphicsStyleIds = new List<ElementId>();
            if (fillPatternIds == null) fillPatternIds = new List<ElementId>();

            // Блок удаления выбранных экземпляров (как и раньше в пользовательском workflow)
            for (int i = 0; i < instanceIds.Count; i++)
            {
                TryDeleteElement(document, instanceIds[i], "Instance", result);
            }

            // Блок удаления DetailLine, основанных на выбранных LineStyle
            for (int i = 0; i < lineStyleIds.Count; i++)
            {
                DeleteDetailCurvesByLineStyle(document, lineStyleIds[i], result);
            }

            // Блок удаления FilledRegionType, использующих выбранные FillPattern
            for (int i = 0; i < fillPatternIds.Count; i++)
            {
                DeleteFilledRegionTypesByPattern(document, fillPatternIds[i], result);
            }

            // Блок удаления собранных типов и семейств
            for (int i = 0; i < typeIds.Count; i++)
            {
                TryDeleteElement(document, typeIds[i], "Type", result);
            }

            for (int i = 0; i < familyIds.Count; i++)
            {
                TryDeleteElement(document, familyIds[i], "Family", result);
            }

            // Блок удаления самих LineStyle
            for (int i = 0; i < lineStyleIds.Count; i++)
            {
                DeleteLineStyleType(document, lineStyleIds[i], result);
            }

            // Блок удаления GraphicsStyle, связанного с LineStyle
            for (int i = 0; i < graphicsStyleIds.Count; i++)
            {
                TryDeleteElement(document, graphicsStyleIds[i], "GraphicsStyle", result);
            }

            // Блок удаления самих FillPattern
            for (int i = 0; i < fillPatternIds.Count; i++)
            {
                TryDeleteElement(document, fillPatternIds[i], "FillPattern", result);
            }

            return result;
        }

        private static void DeleteDetailCurvesByLineStyle(Document document, ElementId lineStyleId, DeletionResult result)
        {
            if (lineStyleId == null || lineStyleId == ElementId.InvalidElementId)
            {
                return;
            }

            List<ElementId> detailCurveIds = new List<ElementId>();
            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(CurveElement));

            foreach (Element element in collector)
            {
                DetailCurve detailCurve = element as DetailCurve;

                if (detailCurve == null || detailCurve.LineStyle == null)
                {
                    continue;
                }

                // Удаляем только экземпляры линий текущего активного вида
                if (document.ActiveView != null &&
                    detailCurve.OwnerViewId != null &&
                    detailCurve.OwnerViewId != ElementId.InvalidElementId &&
                    detailCurve.OwnerViewId.IntegerValue != document.ActiveView.Id.IntegerValue)
                {
                    continue;
                }

                GraphicsStyle graphicsStyle = detailCurve.LineStyle as GraphicsStyle;

                bool sameStyleByElementId = detailCurve.LineStyle.Id.IntegerValue == lineStyleId.IntegerValue;
                bool sameStyleByCategoryId = graphicsStyle != null &&
                    graphicsStyle.GraphicsStyleCategory != null &&
                    graphicsStyle.GraphicsStyleCategory.Id.IntegerValue == lineStyleId.IntegerValue;

                if (sameStyleByElementId || sameStyleByCategoryId)
                {
                    detailCurveIds.Add(detailCurve.Id);
                }
            }

            for (int i = 0; i < detailCurveIds.Count; i++)
            {
                TryDeleteElement(document, detailCurveIds[i], "DetailLine", result);
            }
        }

        // Блок удаления типа LineStyle после удаления всех связанных DetailLine
        private static void DeleteLineStyleType(Document document, ElementId lineStyleTypeId, DeletionResult result)
        {
            if (lineStyleTypeId == null || lineStyleTypeId == ElementId.InvalidElementId)
            {
                return;
            }

            try
            {
                using (Transaction transaction = new Transaction(document, "Delete LineStyle"))
                {
                    transaction.Start();
                    document.Delete(lineStyleTypeId);
                    transaction.Commit();
                }

                result.DeletedCount++;
            }
            catch (Exception exception)
            {
                result.Errors.Add("LineStyle \"" + lineStyleTypeId.IntegerValue + "\" was not deleted. " + exception.Message);
            }
        }

        private static void DeleteFilledRegionTypesByPattern(Document document, ElementId fillPatternId, DeletionResult result)
        {
            if (fillPatternId == null || fillPatternId == ElementId.InvalidElementId)
            {
                return;
            }

            List<ElementId> typeIds = new List<ElementId>();
            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(FilledRegionType));

            foreach (Element element in collector)
            {
                FilledRegionType regionType = element as FilledRegionType;

                if (regionType == null)
                {
                    continue;
                }

                bool usesForeground = regionType.ForegroundPatternId != null &&
                    regionType.ForegroundPatternId != ElementId.InvalidElementId &&
                    regionType.ForegroundPatternId.IntegerValue == fillPatternId.IntegerValue;

                bool usesBackground = regionType.BackgroundPatternId != null &&
                    regionType.BackgroundPatternId != ElementId.InvalidElementId &&
                    regionType.BackgroundPatternId.IntegerValue == fillPatternId.IntegerValue;

                if (usesForeground || usesBackground)
                {
                    typeIds.Add(regionType.Id);
                }
            }

            for (int i = 0; i < typeIds.Count; i++)
            {
                TryDeleteElement(document, typeIds[i], "FilledRegionType", result);
            }
        }

        // Блок обработки ошибок удаления
        private static void TryDeleteElement(Document document, ElementId elementId, string groupName, DeletionResult result)
        {
            if (elementId == null || elementId == ElementId.InvalidElementId)
            {
                return;
            }

            Element element = document.GetElement(elementId);

            if (element == null)
            {
                return;
            }

            string elementName = element.Name;

            try
            {
                using (Transaction transaction = new Transaction(document, "Delete " + groupName))
                {
                    transaction.Start();
                    document.Delete(elementId);
                    transaction.Commit();
                }

                result.DeletedCount++;
            }
            catch (Exception exception)
            {
                result.Errors.Add(groupName + " \"" + elementName + "\" was not deleted. " + exception.Message);
            }
        }
    }

    public class DeletionResult
    {
        public int DeletedCount { get; set; }

        public List<string> Errors { get; private set; } = new List<string>();
    }
}
