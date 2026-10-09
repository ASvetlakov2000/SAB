using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace SAB.FilledRegionFromMaterial
{
    [Transaction(TransactionMode.Manual)]
    public sealed class SettingsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDocument = commandData.Application.ActiveUIDocument;
            if (uiDocument == null || uiDocument.Document.IsFamilyDocument)
            {
                TaskDialog.Show(
                    "Область заливки из материала",
                    "Откройте проект Revit. В редакторе семейств настройки недоступны.");
                return Result.Cancelled;
            }

            try
            {
                Document document = uiDocument.Document;
                PluginSettings settings = PluginSettingsStorage.Load(document);
                ParameterSnapshot snapshot = ParameterSnapshot.Create(document);
                var window = new SettingsWindow(settings, snapshot);
                new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;

                if (window.ShowDialog() != true)
                {
                    return Result.Cancelled;
                }

                using (var transaction = new Transaction(document, "SAB: настройки областей заливки"))
                {
                    transaction.Start();
                    PluginSettingsStorage.Save(document, window.Settings);
                    if (transaction.Commit() != TransactionStatus.Committed)
                        throw new InvalidOperationException("Revit отменил сохранение настроек областей заливки.");
                }

                if (window.RunRequested)
                    return CreateFilledRegionTypesCommand.Run(document, window.Settings);

                TaskDialog.Show(
                    "Область заливки из материала",
                    "Настройки сохранены в текущем проекте Revit.");
                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                message = exception.Message;
                return Result.Failed;
            }
        }
    }
}
