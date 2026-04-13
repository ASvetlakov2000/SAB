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

                if (document == null)
                {
                    message = "Document is not available.";
                    TaskDialog.Show("Delete Types/Families", message);
                    return Result.Failed;
                }

                if (document.ActiveView == null)
                {
                    message = "Active view is not available.";
                    TaskDialog.Show("Delete Types/Families", message);
                    return Result.Failed;
                }

                // Block responsible for selection processing
                ICollection<ElementId> selectedIds = uiDocument.Selection.GetElementIds();

                if (selectedIds == null || selectedIds.Count == 0)
                {
                    TaskDialog.Show("Delete Types/Families", "No instances selected.");
                    return Result.Cancelled;
                }

                // Block responsible for type/family extraction
                List<ElementId> typeIds;
                List<ElementId> familyIds;
                BuildDeletionLists(document, selectedIds, out typeIds, out familyIds);

                if (typeIds.Count == 0 && familyIds.Count == 0)
                {
                    TaskDialog.Show("Delete Types/Families", "No deletable types or families found in selection.");
                    return Result.Cancelled;
                }

                // Block responsible for confirmation dialog
                bool confirmed = ConfirmationDialogService.Ask(
                    "Подтверждение",
                    "Подтверждаете удаление?");

                if (!confirmed)
                {
                    return Result.Cancelled;
                }

                TypeAndFamilyDeletionService deletionService = new TypeAndFamilyDeletionService();
                DeletionResult result = deletionService.DeleteTypesAndFamilies(document, typeIds, familyIds);

                if (result.Errors.Count > 0)
                {
                    TaskDialog.Show(
                        "Delete Types/Families",
                        "Some items were not deleted:\n\n" + string.Join("\n", result.Errors));
                }

                ShowCompletionNotification(
                    "Delete Types/Families",
                    "Deleted: " + result.DeletedCount);

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
            out List<ElementId> typeIds,
            out List<ElementId> familyIds)
        {
            typeIds = new List<ElementId>();
            familyIds = new List<ElementId>();
            HashSet<int> usedTypeIds = new HashSet<int>();
            HashSet<int> usedFamilyIds = new HashSet<int>();

            foreach (ElementId selectedId in selectedIds)
            {
                if (selectedId == null || selectedId == ElementId.InvalidElementId)
                {
                    continue;
                }

                Element instance = document.GetElement(selectedId);

                if (instance == null)
                {
                    continue;
                }

                ElementId typeId = instance.GetTypeId();

                if (typeId != null && typeId != ElementId.InvalidElementId && !usedTypeIds.Contains(typeId.IntegerValue))
                {
                    usedTypeIds.Add(typeId.IntegerValue);
                    typeIds.Add(typeId);
                }

                ElementType elementType = null;

                if (typeId != null && typeId != ElementId.InvalidElementId)
                {
                    elementType = document.GetElement(typeId) as ElementType;
                }

                FamilySymbol familySymbol = elementType as FamilySymbol;

                if (familySymbol == null || familySymbol.Family == null)
                {
                    continue;
                }

                ElementId familyId = familySymbol.Family.Id;

                if (familyId == null || familyId == ElementId.InvalidElementId)
                {
                    continue;
                }

                if (usedFamilyIds.Contains(familyId.IntegerValue))
                {
                    continue;
                }

                usedFamilyIds.Add(familyId.IntegerValue);
                familyIds.Add(familyId);
            }
        }

        private static void ShowCompletionNotification(string title, string message)
        {
            try
            {
                ToastNotifier.ShowSuccess(title, message, 5);
            }
            catch
            {
                TaskDialog.Show(title, message);
            }
        }
    }
}
