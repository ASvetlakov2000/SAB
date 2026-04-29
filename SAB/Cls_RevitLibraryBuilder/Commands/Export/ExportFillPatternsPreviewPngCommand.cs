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
    /// Экспорт PNG предпросмотра для расставленных штриховок.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ExportFillPatternsPreviewPngCommand : IExternalCommand
    {
        private const string SourceViewName = "Библиотека_Штриховки";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                if (uiDocument == null || uiDocument.Document == null)
                {
                    message = "Активный документ недоступен.";
                    TaskDialog.Show("Экспорт PNG штриховок", message);
                    return Result.Failed;
                }

                string selectedFolder = OpenFolder.SelectFolderPath(
                    "Выберите папку для экспорта PNG штриховок",
                    "ctg_lines-patterns");

                if (string.IsNullOrWhiteSpace(selectedFolder))
                {
                    return Result.Cancelled;
                }

                string lineFillFolder = ExportFolderRoutingService.ResolveLineFillExportFolder(selectedFolder);
                string outputFolder = Path.Combine(lineFillFolder, "PNG_Fills");

                ViewDrafting sourceView = FindDraftingViewByName(uiDocument.Document, SourceViewName);

                if (sourceView == null)
                {
                    TaskDialog.Show("Экспорт PNG штриховок", "Не найден вид \"" + SourceViewName + "\".");
                    return Result.Failed;
                }

                DraftingElementImageExportService service = new DraftingElementImageExportService();
                DraftingImageExportResult result = service.ExportFillPatterns(uiDocument, sourceView, outputFolder);

                if (!string.IsNullOrWhiteSpace(result.FatalError))
                {
                    TaskDialog.Show("Экспорт PNG штриховок", result.FatalError);
                    return Result.Failed;
                }

                ThumbnailFoldersRuntimeStore.SetFillImagesFolder(outputFolder);

                string summary = "Всего элементов: " + result.TotalCount +
                                 "\nЭкспортировано: " + result.ExportedCount +
                                 "\nПропущено: " + result.SkippedCount;

                if (result.SkippedCount > 0)
                {
                    ToastNotifier.ShowFolderLinkWarning("Экспорт PNG штриховок завершен с замечаниями", summary, outputFolder, 12);
                }
                else
                {
                    ToastNotifier.ShowFolderLinkSuccess("Экспорт PNG штриховок завершен", summary, outputFolder, 10);
                }

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Экспорт PNG штриховок", exception.ToString());
                return Result.Failed;
            }
        }

        private static ViewDrafting FindDraftingViewByName(Document document, string viewName)
        {
            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(ViewDrafting));

            foreach (Element element in collector)
            {
                ViewDrafting view = element as ViewDrafting;

                if (view == null || view.IsTemplate)
                {
                    continue;
                }

                if (string.Equals(view.Name, viewName, StringComparison.OrdinalIgnoreCase))
                {
                    return view;
                }
            }

            return null;
        }
    }
}
