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
using SAB.InteriorElevations.Services.Settings;
using SAB.InteriorElevations.Views;

namespace SAB.InteriorElevations.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AlignPlanCornerMarksCommand : IExternalCommand
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
                ViewPlan activePlanView = document.ActiveView as ViewPlan;
                if (activePlanView == null)
                {
                    ToastNotifier.ShowWarning("SAB Развертки", "Команда выравнивания марок работает только на активном плане.");
                    return Result.Cancelled;
                }

                List<string> warnings = new List<string>();
                PlanCornerMarkAlignmentSettingsStorageService storageService = new PlanCornerMarkAlignmentSettingsStorageService();
                PlanCornerMarkAlignmentSettings savedSettings = null;

                try
                {
                    savedSettings = storageService.LoadSettings();
                }
                catch (Exception loadException)
                {
                    warnings.Add("Не удалось загрузить сохраненные настройки: " + loadException.Message);
                }

                PlanCornerMarkAlignmentWindow settingsWindow = new PlanCornerMarkAlignmentWindow(savedSettings);
                bool? settingsDialogResult = settingsWindow.ShowDialog();
                if (settingsDialogResult != true || settingsWindow.SelectedSettings == null)
                {
                    return Result.Cancelled;
                }

                PlanCornerMarkAlignmentSettings settings = settingsWindow.SelectedSettings;

                try
                {
                    storageService.SaveSettings(settings);
                }
                catch (Exception saveException)
                {
                    warnings.Add("Не удалось сохранить настройки: " + saveException.Message);
                }

                ToastNotifier.ShowInfo(
                    "SAB Развертки",
                    "Выберите марки семейства SA_Марка угла_План, которые нужно выровнять.");

                IList<Reference> pickedReferences = uiDocument.Selection.PickObjects(
                    ObjectType.Element,
                    new PlanCornerMarkSelectionFilter(activePlanView.Id),
                    "Выберите SA_Марка угла_План для выравнивания");

                if (pickedReferences == null || pickedReferences.Count == 0)
                {
                    ToastNotifier.ShowWarning("SAB Развертки", "Не выбрано ни одной марки для выравнивания.");
                    return Result.Cancelled;
                }

                List<FamilyInstance> selectedMarks = new List<FamilyInstance>();
                for (int index = 0; index < pickedReferences.Count; index++)
                {
                    Element element = document.GetElement(pickedReferences[index]);
                    FamilyInstance familyInstance = element as FamilyInstance;
                    if (familyInstance == null)
                    {
                        continue;
                    }

                    if (!IsPlanCornerMarkFamily(familyInstance))
                    {
                        continue;
                    }

                    selectedMarks.Add(familyInstance);
                }

                if (selectedMarks.Count == 0)
                {
                    ToastNotifier.ShowWarning("SAB Развертки", "Выбранные элементы не относятся к SA_Марка угла_План.");
                    return Result.Cancelled;
                }

                PlanCornerMarkAlignmentResult alignmentResult;

                using (Transaction transaction = new Transaction(document, "SAB Выравнивание марок углов плана"))
                {
                    transaction.Start();

                    PlanCornerMarkAlignmentService alignmentService = new PlanCornerMarkAlignmentService();
                    alignmentResult = alignmentService.AlignSelectedMarks(
                        document,
                        activePlanView,
                        selectedMarks,
                        settings,
                        warnings);

                    transaction.Commit();
                }

                ShowResultToast(alignmentResult, settings, warnings);
                return alignmentResult != null && alignmentResult.AlignedCount > 0
                    ? Result.Succeeded
                    : Result.Cancelled;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                ToastNotifier.ShowError("SAB Развертки", "Ошибка выравнивания марок: " + exception.Message);
                return Result.Failed;
            }
        }

        private bool IsPlanCornerMarkFamily(FamilyInstance familyInstance)
        {
            if (familyInstance == null || familyInstance.Symbol == null)
            {
                return false;
            }

            string familyName = familyInstance.Symbol.Family != null
                ? familyInstance.Symbol.Family.Name
                : familyInstance.Symbol.FamilyName;

            return string.Equals(familyName, CornerMarkConstants.PlanFamilyName, StringComparison.OrdinalIgnoreCase);
        }

        private void ShowResultToast(
            PlanCornerMarkAlignmentResult alignmentResult,
            PlanCornerMarkAlignmentSettings settings,
            IList<string> warnings)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Отчет по выравниванию марок углов на плане");
            builder.AppendLine();

            if (settings != null)
            {
                builder.AppendLine("Отступ марки от угла: " + settings.CornerOffsetMm.ToString("F1") + " мм");
                builder.AppendLine();
            }

            if (alignmentResult != null)
            {
                builder.AppendLine("Выбрано марок: " + alignmentResult.SelectedCount);
                builder.AppendLine("Успешно выровнено: " + alignmentResult.AlignedCount);
                builder.AppendLine("Ошибок: " + alignmentResult.FailedCount);
            }

            if (warnings != null && warnings.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Предупреждения: " + warnings.Count);

                int maxWarningsToShow = Math.Min(3, warnings.Count);
                for (int index = 0; index < maxWarningsToShow; index++)
                {
                    builder.AppendLine((index + 1) + ". " + warnings[index]);
                }

                if (warnings.Count > maxWarningsToShow)
                {
                    builder.AppendLine("... и еще " + (warnings.Count - maxWarningsToShow) + " предупреждений.");
                }
            }

            if (alignmentResult != null && alignmentResult.AlignedCount > 0)
            {
                ToastNotifier.ShowSuccess("SAB Развертки", builder.ToString(), 12);
            }
            else
            {
                ToastNotifier.ShowWarning("SAB Развертки", builder.ToString(), 12);
            }
        }

        private class PlanCornerMarkSelectionFilter : ISelectionFilter
        {
            private readonly ElementId _viewId;

            public PlanCornerMarkSelectionFilter(ElementId viewId)
            {
                _viewId = viewId;
            }

            public bool AllowElement(Element element)
            {
                if (element == null)
                {
                    return false;
                }

                FamilyInstance familyInstance = element as FamilyInstance;
                if (familyInstance == null || familyInstance.Symbol == null || familyInstance.OwnerViewId != _viewId)
                {
                    return false;
                }

                string familyName = familyInstance.Symbol.Family != null
                    ? familyInstance.Symbol.Family.Name
                    : familyInstance.Symbol.FamilyName;

                return string.Equals(familyName, CornerMarkConstants.PlanFamilyName, StringComparison.OrdinalIgnoreCase);
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}
