using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitLibraryBuilder.Services.PostActions;
using System;

namespace RevitLibraryBuilder.Commands.FillPatterns
{
    [Transaction(TransactionMode.Manual)]
    public class PlaceFillPatternsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // ------------------------------------------------------------
                // 1. Получаем документ Revit
                // ------------------------------------------------------------
                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                Document doc = uidoc.Document;

                // ------------------------------------------------------------
                // 2. Получаем выбранный элемент
                // ------------------------------------------------------------
                Element selectedElement = doc.GetElement(uidoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.Element));

                if (selectedElement == null)
                {
                    TaskDialog.Show("Ошибка", "Элемент не выбран");
                    return Result.Failed;
                }

                // ------------------------------------------------------------
                // 3. Получаем категорию элемента
                // ------------------------------------------------------------
                Category category = selectedElement.Category;

                if (category == null)
                {
                    TaskDialog.Show("Ошибка", "У элемента нет категории");
                    return Result.Failed;
                }

                // ------------------------------------------------------------
                // 4. Здесь твоя логика размещения штриховок (Fill Patterns)
                // ------------------------------------------------------------
                using (Transaction t = new Transaction(doc, "Place Fill Patterns"))
                {
                    t.Start();

                    // TODO: твоя логика размещения штриховок
                    // (оставлено как заглушка, чтобы не ломать текущую систему)

                    t.Commit();
                }

                // ------------------------------------------------------------
                // 5. PostAction: создание вида с правильным именем
                // ------------------------------------------------------------
                PostActionViewService.AskAndCreateView(doc, category);

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Ошибка", ex.Message);
                return Result.Failed;
            }
        }
    }
}