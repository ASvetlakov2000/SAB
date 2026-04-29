using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Services;
using RevitLibraryBuilder.Services.Views;
using System;
using System.IO;
using asBIM;

namespace RevitLibraryBuilder.Commands
{
    /// <summary>
    /// Экспорт PNG предпросмотра для расставленных линий.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ExportLineStylesPreviewPngCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                if (uiDocument == null || uiDocument.Document == null)
                {
                    message = "Активный документ недоступен.";
                    TaskDialog.Show("Экспорт PNG линий", message);
                    return Result.Failed;
                }

                string selectedFolder = OpenFolder.SelectFolderPath(
                    "Выберите папку для экспорта PNG линий",
                    "ctg_lines-patterns");

                if (string.IsNullOrWhiteSpace(selectedFolder))
                {
                    return Result.Cancelled;
                }

                ViewDrafting activeDraftingView = uiDocument.ActiveView as ViewDrafting;

                if (activeDraftingView == null)
                {
                    TaskDialog.Show(
                        "Экспорт PNG линий",
                        "Перед запуском сделайте активным чертежный вид со стилями линий.");
                    return Result.Cancelled;
                }

                if (!HasPlacedLineStyles(uiDocument.Document, activeDraftingView))
                {
                    TaskDialog.Show(
                        "Экспорт PNG линий",
                        "На активном виде не найдены размещенные линии.\n" +
                        "Сначала выполните команду \"Размещение линий\".");
                    return Result.Cancelled;
                }

                string lineFillFolder = ExportFolderRoutingService.ResolveLineFillExportFolder(selectedFolder);
                string outputFolder = Path.Combine(lineFillFolder, "PNG_Lines");

                DraftingElementImageExportService service = new DraftingElementImageExportService();
                DraftingImageExportResult result = service.ExportLineStyles(uiDocument, activeDraftingView, outputFolder);

                if (!string.IsNullOrWhiteSpace(result.FatalError))
                {
                    TaskDialog.Show("Экспорт PNG линий", result.FatalError);
                    return Result.Failed;
                }

                ThumbnailFoldersRuntimeStore.SetLineImagesFolder(outputFolder);

                string summary = "Всего элементов: " + result.TotalCount +
                                 "\nЭкспортировано: " + result.ExportedCount +
                                 "\nПропущено: " + result.SkippedCount;

                if (result.SkippedCount > 0)
                {
                    ToastNotifier.ShowFolderLinkWarning("Экспорт PNG линий завершен с замечаниями", summary, outputFolder, 12);
                }
                else
                {
                    ToastNotifier.ShowFolderLinkSuccess("Экспорт PNG линий завершен", summary, outputFolder, 10);
                }

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Экспорт PNG линий", exception.ToString());
                return Result.Failed;
            }
        }

        private static bool HasPlacedLineStyles(Document document, ViewDrafting view)
        {
            if (document == null || view == null)
            {
                return false;
            }

            FilteredElementCollector collector = new FilteredElementCollector(document, view.Id);
            collector.OfClass(typeof(CurveElement));

            foreach (Element element in collector)
            {
                DetailCurve detailCurve = element as DetailCurve;

                if (detailCurve == null)
                {
                    continue;
                }

                if (detailCurve.OwnerViewId == view.Id)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
