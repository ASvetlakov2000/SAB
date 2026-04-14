using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services.Revit;
using SAB.Cls_RevitLibraryBuilder.UI.Dialogs;

namespace RevitLibraryBuilder.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class DeleteSelectedTypesAndFamiliesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = data.Application.ActiveUIDocument;

                if (uiDocument == null)
                {
                    message = "Active UIDocument is not available.";
                    TaskDialog.Show("Delete Types/Families", message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null || document.ActiveView == null)
                {
                    message = "Document or active view is not available.";
                    TaskDialog.Show("Delete Types/Families", message);
                    return Result.Failed;
                }

                // Блок обработки выделения в активном виде
                ICollection<ElementId> selectedIds = uiDocument.Selection.GetElementIds();

                if (selectedIds == null || selectedIds.Count == 0)
                {
                    TaskDialog.Show("Delete Types/Families", "No elements selected.");
                    return Result.Cancelled;
                }

                // Блок сбора типов/семейств и связанных LineStyle/FillPattern
                List<ElementId> instanceIds;
                List<ElementId> typeIds;
                List<ElementId> familyIds;
                List<ElementId> lineStyleIds;
                List<ElementId> graphicsStyleIds;
                List<ElementId> fillPatternIds;

                BuildDeletionLists(
                    document,
                    selectedIds,
                    out instanceIds,
                    out typeIds,
                    out familyIds,
                    out lineStyleIds,
                    out graphicsStyleIds,
                    out fillPatternIds);

                if (instanceIds.Count == 0 &&
                    typeIds.Count == 0 &&
                    familyIds.Count == 0 &&
                    lineStyleIds.Count == 0 &&
                    graphicsStyleIds.Count == 0 &&
                    fillPatternIds.Count == 0)
                {
                    TaskDialog.Show("Delete Types/Families", "No deletable objects were found in selection.");
                    return Result.Cancelled;
                }

                // Блок подтверждения удаления через WPF-диалог
                ConfirmViewCreationDialog dialog = new ConfirmViewCreationDialog(
                    "Подтверждаете удаление?",
                    "Да",
                    "Нет");

                bool confirmed = dialog.ShowDialog() == true && dialog.Result;

                if (!confirmed)
                {
                    return Result.Cancelled;
                }

                TypeAndFamilyDeletionService service = new TypeAndFamilyDeletionService();
                DeletionResult result = service.Delete(
                    document,
                    instanceIds,
                    typeIds,
                    familyIds,
                    lineStyleIds,
                    graphicsStyleIds,
                    fillPatternIds);

                if (result.Errors.Count > 0)
                {
                    TaskDialog.Show("Delete Types/Families", string.Join("\n", result.Errors));
                }

                ShowNotification("Delete Types/Families", "Deleted: " + result.DeletedCount);
                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Delete Types/Families", exception.ToString());
                return Result.Failed;
            }
        }

        private static void BuildDeletionLists(
            Document document,
            ICollection<ElementId> selectedIds,
            out List<ElementId> instanceIds,
            out List<ElementId> typeIds,
            out List<ElementId> familyIds,
            out List<ElementId> lineStyleIds,
            out List<ElementId> graphicsStyleIds,
            out List<ElementId> fillPatternIds)
        {
            instanceIds = new List<ElementId>();
            typeIds = new List<ElementId>();
            familyIds = new List<ElementId>();
            lineStyleIds = new List<ElementId>();
            graphicsStyleIds = new List<ElementId>();
            fillPatternIds = new List<ElementId>();

            HashSet<int> seenInstanceIds = new HashSet<int>();
            HashSet<int> seenTypeIds = new HashSet<int>();
            HashSet<int> seenFamilyIds = new HashSet<int>();
            HashSet<int> seenLineStyleIds = new HashSet<int>();
            HashSet<int> seenGraphicsStyleIds = new HashSet<int>();
            HashSet<int> seenFillPatternIds = new HashSet<int>();

            foreach (ElementId selectedId in selectedIds)
            {
                if (selectedId == null || selectedId == ElementId.InvalidElementId)
                {
                    continue;
                }

                if (!seenInstanceIds.Contains(selectedId.IntegerValue))
                {
                    seenInstanceIds.Add(selectedId.IntegerValue);
                    instanceIds.Add(selectedId);
                }

                Element instance = document.GetElement(selectedId);

                if (instance == null)
                {
                    continue;
                }

                // Блок сбора Type/Family по выбранному экземпляру
                ElementId typeId = instance.GetTypeId();

                if (typeId != null &&
                    typeId != ElementId.InvalidElementId &&
                    !seenTypeIds.Contains(typeId.IntegerValue))
                {
                    seenTypeIds.Add(typeId.IntegerValue);
                    typeIds.Add(typeId);
                }

                ElementType elementType = document.GetElement(typeId) as ElementType;
                FamilySymbol symbol = elementType as FamilySymbol;

                if (symbol != null && symbol.Family != null)
                {
                    ElementId familyId = symbol.Family.Id;

                    if (familyId != null &&
                        familyId != ElementId.InvalidElementId &&
                        !seenFamilyIds.Contains(familyId.IntegerValue))
                    {
                        seenFamilyIds.Add(familyId.IntegerValue);
                        familyIds.Add(familyId);
                    }
                }

                // Блок сбора LineStyle из выбранных DetailLine
                DetailCurve detailCurve = instance as DetailCurve;

                if (detailCurve != null && detailCurve.LineStyle != null)
                {
                    ElementId graphicsStyleId = detailCurve.LineStyle.Id;

                    if (graphicsStyleId != null &&
                        graphicsStyleId != ElementId.InvalidElementId &&
                        !seenGraphicsStyleIds.Contains(graphicsStyleId.IntegerValue))
                    {
                        seenGraphicsStyleIds.Add(graphicsStyleId.IntegerValue);
                        graphicsStyleIds.Add(graphicsStyleId);
                    }

                    ElementId lineStyleId = ResolveLineStyleTypeId(detailCurve.LineStyle);

                    if (lineStyleId != null &&
                        lineStyleId != ElementId.InvalidElementId &&
                        !seenLineStyleIds.Contains(lineStyleId.IntegerValue))
                    {
                        seenLineStyleIds.Add(lineStyleId.IntegerValue);
                        lineStyleIds.Add(lineStyleId);
                    }
                }

                // Блок сбора FillPattern из выбранных FilledRegion/FilledRegionType
                FilledRegionType filledRegionType = null;
                FilledRegion filledRegion = instance as FilledRegion;

                if (filledRegion != null)
                {
                    filledRegionType = document.GetElement(filledRegion.GetTypeId()) as FilledRegionType;
                }
                else
                {
                    filledRegionType = instance as FilledRegionType;
                }

                if (filledRegionType != null)
                {
                    TryAddFillPatternId(filledRegionType.ForegroundPatternId, seenFillPatternIds, fillPatternIds);
                    TryAddFillPatternId(filledRegionType.BackgroundPatternId, seenFillPatternIds, fillPatternIds);
                }
            }
        }

        private static void TryAddFillPatternId(
            ElementId fillPatternId,
            HashSet<int> seenFillPatternIds,
            List<ElementId> fillPatternIds)
        {
            if (fillPatternId == null || fillPatternId == ElementId.InvalidElementId)
            {
                return;
            }

            if (seenFillPatternIds.Contains(fillPatternId.IntegerValue))
            {
                return;
            }

            seenFillPatternIds.Add(fillPatternId.IntegerValue);
            fillPatternIds.Add(fillPatternId);
        }

        // Блок получения идентификатора типа LineStyle для последующего удаления
        private static ElementId ResolveLineStyleTypeId(Element lineStyleElement)
        {
            if (lineStyleElement == null)
            {
                return ElementId.InvalidElementId;
            }

            GraphicsStyle graphicsStyle = lineStyleElement as GraphicsStyle;

            if (graphicsStyle != null &&
                graphicsStyle.GraphicsStyleCategory != null &&
                graphicsStyle.GraphicsStyleCategory.Id != ElementId.InvalidElementId)
            {
                return graphicsStyle.GraphicsStyleCategory.Id;
            }

            return lineStyleElement.Id;
        }

        private static void ShowNotification(string title, string message)
        {
            try
            {
                ToastNotifier.ShowSuccess(title, message, 10);
            }
            catch
            {
                TaskDialog.Show(title, message);
            }
        }
    }
}
