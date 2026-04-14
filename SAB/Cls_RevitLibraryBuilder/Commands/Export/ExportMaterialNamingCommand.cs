using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Helpers.Notifications.ToastNotifications;
using RevitLibraryBuilder.Models;
using RevitLibraryBuilder.Services.Csv;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace RevitLibraryBuilder.Commands
{
    /// <summary>
    /// Выгрузка наименований материалов для переименования и удаления.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class ExportMaterialNamingCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uiDocument = commandData.Application.ActiveUIDocument;

                if (uiDocument == null)
                {
                    message = "Active UIDocument is not available.";
                    ShowErrorNotification("Выгрузка наименований материалов", message);
                    return Result.Failed;
                }

                Document document = uiDocument.Document;

                if (document == null || document.ActiveView == null)
                {
                    message = "Document or active view is not available.";
                    ShowErrorNotification("Выгрузка наименований материалов", message);
                    return Result.Failed;
                }

                List<MaterialNamingCsvModel> rows = BuildRows(document);

                if (rows.Count == 0)
                {
                    ToastNotifier.ShowWarning("Выгрузка наименований материалов", "Материалы не найдены.", 10);
                    return Result.Cancelled;
                }

                using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
                {
                    // Блок выбора папки для XLSX материалов
                    folderDialog.Description = "Выберите папку для XLSX выгрузки наименований материалов";

                    if (folderDialog.ShowDialog() != DialogResult.OK)
                    {
                        return Result.Cancelled;
                    }

                    string outputFolder = folderDialog.SelectedPath;

                    MaterialNamingCsvService csvService = new MaterialNamingCsvService();
                    string filePath = csvService.WriteMaterialXlsx(outputFolder, document.Title, rows);

                    string folderPath = System.IO.Path.GetDirectoryName(filePath) ?? outputFolder;
                    ToastNotifier.ShowFolderLinkSuccess(
                        "Выгрузка завершена",
                        "XLSX для переименования материалов сохранен:\n",
                        folderPath,
                        10);
                }

                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                ShowErrorNotification("Выгрузка наименований материалов", exception.Message);
                return Result.Failed;
            }
        }

        // Блок подготовки строк CSV по всем материалам проекта
        private static List<MaterialNamingCsvModel> BuildRows(Document document)
        {
            List<MaterialNamingCsvModel> rows = new List<MaterialNamingCsvModel>();

            FilteredElementCollector collector = new FilteredElementCollector(document);
            collector.OfClass(typeof(Material));

            foreach (Element element in collector)
            {
                Material material = element as Material;

                if (material == null)
                {
                    continue;
                }

                string description = GetDescription(material);

                rows.Add(new MaterialNamingCsvModel
                {
                    MaterialNameOld = material.Name,
                    MaterialNameNew = material.Name,
                    DescriptionOld = description,
                    DescriptionNew = description,
                    Manufacturer = GetMaterialText(material, BuiltInParameter.ALL_MODEL_MANUFACTURER, "Изготовитель"),
                    Model = GetMaterialText(material, BuiltInParameter.ALL_MODEL_MODEL, "Модель"),
                    Keynote = GetMaterialText(material, BuiltInParameter.KEYNOTE_PARAM, "Ключевая заметка"),
                    Marking = GetMaterialText(material, BuiltInParameter.ALL_MODEL_MARK, "Маркировка"),
                    DeleteMaterial = false
                });
            }

            rows.Sort(delegate (MaterialNamingCsvModel left, MaterialNamingCsvModel right)
            {
                return string.Compare(left.MaterialNameOld, right.MaterialNameOld, StringComparison.OrdinalIgnoreCase);
            });

            return rows;
        }

        private static string GetDescription(Material material)
        {
            Parameter descriptionParameter = material.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION);

            if (descriptionParameter == null)
            {
                descriptionParameter = material.LookupParameter("Description");
            }

            if (descriptionParameter == null)
            {
                return string.Empty;
            }

            return descriptionParameter.AsString() ?? string.Empty;
        }

        private static string GetMaterialText(Material material, BuiltInParameter builtInParameter, string fallbackName)
        {
            Parameter parameter = material.get_Parameter(builtInParameter);

            if (parameter == null)
            {
                parameter = material.LookupParameter(fallbackName);
            }

            if (parameter == null)
            {
                return string.Empty;
            }

            return parameter.AsString() ?? string.Empty;
        }

        private static void ShowErrorNotification(string title, string text)
        {
            ToastNotifier.ShowError(title, text, 12);
        }
    }
}
