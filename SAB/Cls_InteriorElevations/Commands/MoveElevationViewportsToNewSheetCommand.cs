using System;
using System.Collections.Generic;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Helpers.Notifications.ToastNotifications;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Marks;
using SAB.InteriorElevations.Services.Sheets;

namespace SAB.InteriorElevations.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class MoveElevationViewportsToNewSheetCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;
                if (uiDocument == null || uiDocument.Document == null)
                {
                    ToastNotifier.ShowError("SAB Развертки", "Не удалось получить активный документ Revit.");
                    return Result.Failed;
                }

                Document document = uiDocument.Document;
                ViewSheet activeSheet = document.ActiveView as ViewSheet;
                if (activeSheet == null)
                {
                    ToastNotifier.ShowWarning("SAB Развертки", "Команда переноса работает только на активном листе.");
                    return Result.Cancelled;
                }

                ToastNotifier.ShowInfo(
                    "SAB Развертки",
                    "Выберите рамкой видовые экраны и марки SA_Марка угла_Развертки для переноса на новый лист.");

                IList<Reference> pickedReferences = uiDocument.Selection.PickObjects(
                    ObjectType.Element,
                    new ViewportAndSheetMarkSelectionFilter(activeSheet.Id),
                    "Выберите viewport и SA_Марка угла_Развертки для переноса");

                if (pickedReferences == null || pickedReferences.Count == 0)
                {
                    ToastNotifier.ShowWarning("SAB Развертки", "Не выбрано ни одного элемента для переноса.");
                    return Result.Cancelled;
                }

                List<Viewport> selectedViewports = new List<Viewport>();
                List<FamilyInstance> selectedSheetMarks = new List<FamilyInstance>();

                for (int index = 0; index < pickedReferences.Count; index++)
                {
                    Element element = document.GetElement(pickedReferences[index]);
                    Viewport viewport = element as Viewport;
                    if (viewport != null)
                    {
                        selectedViewports.Add(viewport);
                        continue;
                    }

                    FamilyInstance familyInstance = element as FamilyInstance;
                    if (familyInstance != null && IsSheetCornerMark(familyInstance))
                    {
                        selectedSheetMarks.Add(familyInstance);
                    }
                }

                if (selectedViewports.Count == 0 && selectedSheetMarks.Count == 0)
                {
                    ToastNotifier.ShowWarning("SAB Развертки", "Выбранные элементы не подходят для переноса.");
                    return Result.Cancelled;
                }

                List<string> warnings = new List<string>();
                ViewportTransferResult transferResult;

                using (TransactionGroup transactionGroup = new TransactionGroup(document, "SAB Перенос разверток и марок"))
                {
                    transactionGroup.Start();

                    using (Transaction transaction = new Transaction(document, "Перенести viewport и марки на новый лист"))
                    {
                        transaction.Start();

                        ViewportTransferService transferService = new ViewportTransferService();
                        transferResult = transferService.TransferViewports(
                            document,
                            activeSheet,
                            selectedViewports,
                            selectedSheetMarks,
                            warnings);

                        transaction.Commit();
                    }

                    transactionGroup.Assimilate();
                }

                ShowTransferReport(transferResult, warnings);
                return transferResult != null && transferResult.MovedCount > 0 ? Result.Succeeded : Result.Cancelled;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                ToastNotifier.ShowError("SAB Развертки", "Ошибка переноса элементов: " + exception.Message);
                return Result.Failed;
            }
        }

        private bool IsSheetCornerMark(FamilyInstance familyInstance)
        {
            if (familyInstance == null || familyInstance.Symbol == null)
            {
                return false;
            }

            string familyName = familyInstance.Symbol.Family != null
                ? familyInstance.Symbol.Family.Name
                : familyInstance.Symbol.FamilyName;

            return string.Equals(familyName, CornerMarkConstants.SheetFamilyName, StringComparison.OrdinalIgnoreCase);
        }

        private void ShowTransferReport(ViewportTransferResult result, IList<string> warnings)
        {
            if (result == null)
            {
                ToastNotifier.ShowWarning("SAB Развертки", "Команда завершена без результата.");
                return;
            }

            StringBuilder reportBuilder = new StringBuilder();
            reportBuilder.AppendLine("Отчет по переносу элементов листа");
            reportBuilder.AppendLine();
            reportBuilder.AppendLine("Выбрано viewport: " + result.SelectedViewportCount);
            reportBuilder.AppendLine("Выбрано марок: " + result.SelectedSheetMarkCount);
            reportBuilder.AppendLine("Перенесено viewport: " + result.MovedViewportCount);
            reportBuilder.AppendLine("Перенесено марок: " + result.MovedSheetMarkCount);
            reportBuilder.AppendLine("Ошибок viewport: " + result.FailedViewportCount);
            reportBuilder.AppendLine("Ошибок марок: " + result.FailedSheetMarkCount);

            if (result.SourceSheet != null)
            {
                reportBuilder.AppendLine("Исходный лист: " + result.SourceSheet.SheetNumber + " | " + result.SourceSheet.Name);
            }

            if (result.TargetSheet != null)
            {
                reportBuilder.AppendLine("Новый лист: " + result.TargetSheet.SheetNumber + " | " + result.TargetSheet.Name);
            }

            if (warnings != null && warnings.Count > 0)
            {
                reportBuilder.AppendLine();
                reportBuilder.AppendLine("Предупреждения: " + warnings.Count);
                int maxWarningsToShow = Math.Min(3, warnings.Count);
                for (int index = 0; index < maxWarningsToShow; index++)
                {
                    reportBuilder.AppendLine((index + 1) + ". " + warnings[index]);
                }

                if (warnings.Count > maxWarningsToShow)
                {
                    reportBuilder.AppendLine("... и еще " + (warnings.Count - maxWarningsToShow) + " предупреждений.");
                }
            }

            if (result.MovedCount > 0)
            {
                ToastNotifier.ShowSuccess("SAB Развертки", reportBuilder.ToString(), 12);
            }
            else
            {
                ToastNotifier.ShowWarning("SAB Развертки", reportBuilder.ToString(), 12);
            }
        }

        private class ViewportAndSheetMarkSelectionFilter : ISelectionFilter
        {
            private readonly ElementId _sheetId;

            public ViewportAndSheetMarkSelectionFilter(ElementId sheetId)
            {
                _sheetId = sheetId;
            }

            public bool AllowElement(Element element)
            {
                if (element == null)
                {
                    return false;
                }

                Viewport viewport = element as Viewport;
                if (viewport != null)
                {
                    return viewport.OwnerViewId == _sheetId;
                }

                FamilyInstance familyInstance = element as FamilyInstance;
                if (familyInstance == null || familyInstance.OwnerViewId != _sheetId || familyInstance.Symbol == null)
                {
                    return false;
                }

                string familyName = familyInstance.Symbol.Family != null
                    ? familyInstance.Symbol.Family.Name
                    : familyInstance.Symbol.FamilyName;

                return string.Equals(familyName, CornerMarkConstants.SheetFamilyName, StringComparison.OrdinalIgnoreCase);
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}
