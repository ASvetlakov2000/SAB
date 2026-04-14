using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Models;
using RevitLibraryBuilder.Services.Csv;
using RevitLibraryBuilder.Services.Placement;
using RevitLibraryBuilder.Services.PostActions;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace RevitLibraryBuilder.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ImportByLineCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = data.Application.ActiveUIDocument;

                if (uiDocument == null)
                {
                    message = "Active UIDocument is not available.";
                    TaskDialog.Show("Import By Line", message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null)
                {
                    message = "Document is not available.";
                    TaskDialog.Show("Import By Line", message);
                    return Result.Failed;
                }

                string csvFilePath = RequestCsvFilePath();

                if (string.IsNullOrWhiteSpace(csvFilePath))
                {
                    return Result.Cancelled;
                }

                List<ElementTypeCsvModel> csvRows = new CsvImportService().ImportFromCsv(csvFilePath);

                if (csvRows == null || csvRows.Count == 0)
                {
                    TaskDialog.Show("Import By Line", "CSV import failed or returned no rows.");
                    return Result.Cancelled;
                }

                // Block responsible for extracting category name from CSV
                string categoryName = ResolveCategoryFromCsv(csvRows);
                int includedRowCount = CountIncludedRows(csvRows);

                if (includedRowCount <= 0)
                {
                    TaskDialog.Show("Import By Line", "No rows with Include=TRUE were found.");
                    return Result.Cancelled;
                }

                Level level = ResolveLevel(document);

                if (level == null)
                {
                    TaskDialog.Show("Import By Line", "No valid Level was found for placement.");
                    return Result.Failed;
                }

                IPlacementService placementService = PlacementServiceFactory.Create("Line", document);
                placementService.Place(csvRows, level);
                ShowPlacementNotification(includedRowCount);

                // Block responsible for passing category into post-action workflow
                PostActionViewService.RunAfterPlacement(document, categoryName, includedRowCount, "ImportByLineCommand");

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                TaskDialog.Show("Import By Line", exception.ToString());
                return Result.Failed;
            }
        }

        private static string RequestCsvFilePath()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "CSV (*.csv)|*.csv";

                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return null;
                }

                return dialog.FileName;
            }
        }

        private static Level ResolveLevel(Document document)
        {
            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(Level));

            foreach (Element element in collector)
            {
                Level level = element as Level;

                if (level != null)
                {
                    return level;
                }
            }

            return null;
        }

        private static int CountIncludedRows(List<ElementTypeCsvModel> rows)
        {
            int count = 0;

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null && rows[i].Include)
                {
                    count++;
                }
            }

            return count;
        }

        private static string ResolveCategoryFromCsv(List<ElementTypeCsvModel> rows)
        {
            string resolvedCategory = string.Empty;

            for (int i = 0; i < rows.Count; i++)
            {
                ElementTypeCsvModel row = rows[i];

                if (row == null || string.IsNullOrWhiteSpace(row.Category))
                {
                    continue;
                }

                resolvedCategory = row.Category.Trim();
                break;
            }

            return resolvedCategory;
        }

        private static void ShowPlacementNotification(int placedCount)
        {
            string title = "Размещение элементов";
            string message = "Элементов размещено " + placedCount;

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
