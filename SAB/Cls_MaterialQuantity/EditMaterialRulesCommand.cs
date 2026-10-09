using System;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace SAB.MaterialQuantity
{
    [Transaction(TransactionMode.Manual)]
    public sealed class EditMaterialRulesCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uiDocument = commandData.Application.ActiveUIDocument;
            if (uiDocument == null || uiDocument.Document.IsFamilyDocument)
            {
                message = "Откройте проект Revit, а не семейство.";
                return Result.Failed;
            }

            int savedRules = 0;
            try
            {
                Document project = uiDocument.Document;
                Material[] materials = new FilteredElementCollector(project)
                    .OfClass(typeof(Material)).Cast<Material>().ToArray();
                var model = new MaterialRulesViewModel(
                    materials,
                    MaterialUsageCollector.CollectUsedMaterialIds(project));
                var window = new MaterialRulesWindow(
                    model,
                    () =>
                    {
                        int count = SaveRules(commandData.Application, project, model);
                        savedRules += count;
                        return count;
                    });
                new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
                if (window.ShowDialog() != true)
                {
                    // Revit rolls back the whole external command when it returns
                    // Cancelled or Failed, including transactions committed by SaveRules.
                    // Once at least one rule was saved, closing the window must keep it.
                    if (window.Failure == null)
                        return savedRules > 0 ? Result.Succeeded : Result.Cancelled;

                    message = window.Failure.Message;
                    return savedRules > 0 ? Result.Succeeded : Result.Failed;
                }

                if (!window.UpdateRequested)
                    return savedRules > 0 ? Result.Succeeded : Result.Cancelled;

                SyncReport report = MaterialQuantityUpdateWorkflow.RunWithProgressWindow(
                    commandData.Application, project);

                TaskDialog.Show(
                    "SAB — материалы и ведомости",
                    MaterialQuantityUpdateWorkflow.BuildSummary(report, savedRules));
                return Result.Succeeded;
            }
            catch (Exception exception)
            {
                DiagnosticLog.Write(version: commandData.Application.Application.VersionNumber,
                    message: "Material rules window failed: " + exception);
                message = exception.Message;
                if (savedRules > 0)
                {
                    TaskDialog.Show("SAB — материалы и ведомости",
                        "Правила материалов сохранены, но пересчёт не завершён:\n" + exception.Message);
                    return Result.Succeeded;
                }
                return Result.Failed;
            }
        }

        private static int SaveRules(
            UIApplication application,
            Document project,
            MaterialRulesViewModel model)
        {
            MaterialRuleRow[] changed = model.Rows.Where(row => row.IsChanged).ToArray();
            if (changed.Length == 0) return 0;

            using (var transaction = new Transaction(project, "SAB: параметры подсчёта материалов"))
            {
                transaction.Start();
                MaterialRuleParameter.EnsureBound(application.Application, project);
                foreach (MaterialRuleRow row in changed)
                {
                    Material material = project.GetElement(row.MaterialId) as Material;
                    Parameter parameter = material == null
                        ? null
                        : material.get_Parameter(MaterialRuleParameter.Id);
                    if (parameter == null || parameter.IsReadOnly)
                    {
                        throw new InvalidOperationException(
                            "Недоступен параметр единицы у материала: " + row.Name);
                    }

                    string value = row.Rule == MaterialUnitResolver.Auto ? "" : row.Rule;
                    if (!parameter.Set(value))
                    {
                        throw new InvalidOperationException(
                            "Revit не записал правило единицы у материала: " + row.Name);
                    }
                }
                transaction.Commit();
            }

            model.AcceptChanges(changed);
            DiagnosticLog.Write(
                project.Application.VersionNumber,
                "Material rules saved: " + changed.Length +
                "; DLL=" + typeof(EditMaterialRulesCommand).Assembly.Location);
            return changed.Length;
        }
    }
}
