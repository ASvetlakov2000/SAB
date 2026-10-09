using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace SAB.ParameterTools
{
    internal static class CommandRunner
    {
        internal static Result Run(UIApplication app, Action action, ref string message)
        {
            try
            {
                if (app.ActiveUIDocument == null || app.ActiveUIDocument.Document.IsFamilyDocument)
                    throw new InvalidOperationException("Команда доступна в проектной модели.");
                if (app.ActiveUIDocument.Document.IsReadOnly) throw new InvalidOperationException("Модель доступна только для чтения.");
                ParameterToolsModule.Host.Busy = true; action(); return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
            catch (System.OperationCanceledException) { return Result.Cancelled; }
            catch (Exception ex)
            {
                message = ex.Message; ParameterToolsModule.Host.Log(ex);
                var dialog = new TaskDialog("SAB — параметры") { MainInstruction = "Не удалось выполнить команду параметров", MainContent = ex.Message,
                    ExpandedContent = ex + "\n\nЖурнал: " + System.IO.Path.Combine(RuntimeHost.LogFolder, "errors.log"), CommonButtons = TaskDialogCommonButtons.Close };
                dialog.Show(); return Result.Cancelled;
            }
            finally { ParameterToolsModule.Host.Busy = false; }
        }
    }
    [Transaction(TransactionMode.Manual)]
    public sealed class FillParametersCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        { return CommandRunner.Run(data.Application, () => Workflow.Fill(data.Application), ref message); }
    }
    [Transaction(TransactionMode.Manual)]
    public sealed class CheckParametersCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        { return CommandRunner.Run(data.Application, () => Workflow.Check(data.Application), ref message); }
    }
    [Transaction(TransactionMode.Manual)]
    public sealed class ClearHighlightCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        { return CommandRunner.Run(data.Application, () => ParameterToolsModule.Host.Highlight.Clear(data.Application.ActiveUIDocument), ref message); }
    }
    [Transaction(TransactionMode.Manual)]
    public sealed class SettingsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        { return CommandRunner.Run(data.Application, () => Workflow.Settings(data.Application), ref message); }
    }
    [Transaction(TransactionMode.Manual)]
    public sealed class AssignCorpusCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            return CommandRunner.Run(data.Application, () => Workflow.AssignCorpus(data.Application), ref message);
        }
    }
}
