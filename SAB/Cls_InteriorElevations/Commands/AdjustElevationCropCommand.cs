using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Helpers.Notifications.ToastNotifications;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Services.Elevations;
using SAB.InteriorElevations.Services.Marks;
using SAB.InteriorElevations.Utils;
using SAB.InteriorElevations.Views;

namespace SAB.InteriorElevations.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AdjustElevationCropCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApplication = commandData.Application;
                UIDocument uiDocument = uiApplication.ActiveUIDocument;
                if (uiDocument == null || uiDocument.Document == null)
                {
                    ToastNotifier.ShowError("SAB Развертки", "Не удалось получить активный документ Revit.");
                    return Result.Failed;
                }

                Document document = uiDocument.Document;
                ViewSheet activeSheet = document.ActiveView as ViewSheet;
                if (activeSheet == null)
                {
                    ToastNotifier.ShowWarning("SAB Развертки", "Откройте лист с развертками и повторите команду.");
                    return Result.Cancelled;
                }

                ElevationCropAdjustmentWindow window = new ElevationCropAdjustmentWindow();
                if (uiApplication.MainWindowHandle != IntPtr.Zero)
                {
                    new WindowInteropHelper(window).Owner = uiApplication.MainWindowHandle;
                }

                bool? dialogResult = window.ShowDialog();
                if (dialogResult != true || window.SelectedSettings == null)
                {
                    return Result.Cancelled;
                }

                ElevationCropAdjustmentSettings settings = window.SelectedSettings;

                ToastNotifier.ShowInfo(
                    "SAB Развертки",
                    "Выберите видовые экраны разверток и связанные с ними марки углов, затем нажмите Готово.");

                IList<Reference> pickedReferences = uiDocument.Selection.PickObjects(
                    ObjectType.Element,
                    new ElevationCropElementSelectionFilter(document, activeSheet.Id),
                    "Выберите развертки и марки углов");

                List<Viewport> selectedViewports;
                List<FamilyInstance> selectedCornerMarks;
                GetSelectedElements(
                    document,
                    activeSheet.Id,
                    pickedReferences,
                    out selectedViewports,
                    out selectedCornerMarks);

                if (selectedViewports.Count == 0)
                {
                    ToastNotifier.ShowWarning("SAB Развертки", "Не выбрано ни одной развертки.");
                    return Result.Cancelled;
                }

                List<string> warnings = new List<string>();
                ElevationCropAdjustmentResult result;

                using (Transaction transaction = new Transaction(document, "SAB Изменение границ разверток"))
                {
                    transaction.Start();

                    ElevationCropAdjustmentService service = new ElevationCropAdjustmentService();
                    result = service.Apply(
                        document,
                        selectedViewports,
                        selectedCornerMarks,
                        settings,
                        warnings);

                    transaction.Commit();
                }

                ShowResult(result, warnings);
                return result != null && result.UpdatedCount > 0
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
                ToastNotifier.ShowError("SAB Развертки", "Ошибка изменения границ вида: " + exception.Message);
                return Result.Failed;
            }
        }

        private static void GetSelectedElements(
            Document document,
            ElementId sheetId,
            IList<Reference> pickedReferences,
            out List<Viewport> viewports,
            out List<FamilyInstance> cornerMarks)
        {
            viewports = new List<Viewport>();
            cornerMarks = new List<FamilyInstance>();
            HashSet<long> addedViewIds = new HashSet<long>();
            HashSet<long> addedMarkIds = new HashSet<long>();

            if (pickedReferences == null)
            {
                return;
            }

            for (int index = 0; index < pickedReferences.Count; index++)
            {
                Element element = document.GetElement(pickedReferences[index]);
                Viewport viewport = element as Viewport;
                if (viewport != null &&
                    RevitElementIdUtils.AreEqual(viewport.OwnerViewId, sheetId) &&
                    IsInteriorElevation(document.GetElement(viewport.ViewId) as ViewSection))
                {
                    long viewId = RevitElementIdUtils.GetElementIdValue(viewport.ViewId);
                    if (addedViewIds.Add(viewId))
                    {
                        viewports.Add(viewport);
                    }

                    continue;
                }

                FamilyInstance mark = element as FamilyInstance;
                if (IsSheetCornerMark(mark, sheetId))
                {
                    long markId = RevitElementIdUtils.GetElementIdValue(mark.Id);
                    if (addedMarkIds.Add(markId))
                    {
                        cornerMarks.Add(mark);
                    }
                }
            }
        }

        private static bool IsInteriorElevation(ViewSection view)
        {
            return view != null && !view.IsTemplate &&
                   (view.ViewType == ViewType.Elevation || view.ViewType == ViewType.Section);
        }

        private static void ShowResult(ElevationCropAdjustmentResult result, IList<string> warnings)
        {
            if (result == null)
            {
                ToastNotifier.ShowWarning("SAB Развертки", "Команда завершена без результата.");
                return;
            }

            if (result.UpdatedCount > 0 &&
                result.FailedCount == 0 &&
                result.SelectedMarkCount > 0 &&
                result.FailedMarkCount == 0 &&
                result.FailedGridCount == 0)
            {
                ToastNotifier.ShowSuccess(
                    "SAB Развертки",
                    "Границы обновлены. Видов: " + result.UpdatedCount +
                    ". Марок синхронизировано: " + result.UpdatedMarkCount +
                    ". Осей подрезано: " + result.UpdatedGridCount + ".");
                return;
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("Обновлено видов: " + result.UpdatedCount);
            builder.AppendLine("Не удалось обновить: " + result.FailedCount);
            builder.AppendLine("Синхронизировано марок: " + result.UpdatedMarkCount);
            builder.AppendLine("Ошибок марок: " + result.FailedMarkCount);
            builder.AppendLine("Подрезано осей в режиме 2D: " + result.UpdatedGridCount);
            builder.AppendLine("Ошибок осей: " + result.FailedGridCount);

            if (warnings != null && warnings.Count > 0)
            {
                int maximumWarnings = Math.Min(3, warnings.Count);
                for (int index = 0; index < maximumWarnings; index++)
                {
                    builder.AppendLine((index + 1) + ". " + warnings[index]);
                }

                if (warnings.Count > maximumWarnings)
                {
                    builder.AppendLine("... и еще " + (warnings.Count - maximumWarnings) + ".");
                }
            }

            ToastNotifier.ShowWarning("SAB Развертки", builder.ToString(), 12);
        }

        private static bool IsSheetCornerMark(FamilyInstance mark, ElementId sheetId)
        {
            return mark != null &&
                   RevitElementIdUtils.AreEqual(mark.OwnerViewId, sheetId) &&
                   CornerMarkConstants.IsAnnotationInstance(mark) &&
                   mark.LookupParameter(CornerMarkConstants.CornerNumberParameterName) != null;
        }

        private class ElevationCropElementSelectionFilter : ISelectionFilter
        {
            private readonly Document _document;
            private readonly ElementId _sheetId;

            public ElevationCropElementSelectionFilter(Document document, ElementId sheetId)
            {
                _document = document;
                _sheetId = sheetId;
            }

            public bool AllowElement(Element element)
            {
                Viewport viewport = element as Viewport;
                if (viewport != null)
                {
                    return RevitElementIdUtils.AreEqual(viewport.OwnerViewId, _sheetId) &&
                           IsInteriorElevation(_document.GetElement(viewport.ViewId) as ViewSection);
                }

                return IsSheetCornerMark(element as FamilyInstance, _sheetId);
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return false;
            }
        }
    }
}
