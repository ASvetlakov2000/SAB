using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Linq;

namespace Commands
{
    [Transaction(TransactionMode.Manual)]
    public class PlaceLineStylesCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            // ------------------------------------------------------------
            // 1. Получаем документ
            // ------------------------------------------------------------
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // ------------------------------------------------------------
            // 2. Получаем активный вид
            // ------------------------------------------------------------
            View view = doc.ActiveView;

            // ------------------------------------------------------------
            // 3. Собираем все DetailCurve на виде
            // ------------------------------------------------------------
            var curves = new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(DetailCurve))
                .Cast<DetailCurve>()
                .ToList();

            if (!curves.Any())
                return Result.Succeeded;

            // ------------------------------------------------------------
            // 4. Получаем категорию Line Styles (OST_Lines)
            // ------------------------------------------------------------
            Category linesCategory = doc.Settings.Categories
                .get_Item(BuiltInCategory.OST_Lines);

            // ------------------------------------------------------------
            // 5. Создаём транзакцию
            // ------------------------------------------------------------
            using (Transaction t = new Transaction(doc, "Apply Line Styles"))
            {
                t.Start();

                foreach (var dc in curves)
                {
                    // ------------------------------------------------------------
                    // 6. Берём текущий стиль линии
                    // ------------------------------------------------------------
                    GraphicsStyle gs = dc.LineStyle as GraphicsStyle;

                    if (gs == null)
                        continue;

                    string styleName = gs.GraphicsStyleCategory?.Name;

                    if (string.IsNullOrEmpty(styleName))
                        continue;

                    // ------------------------------------------------------------
                    // 7. Ищем соответствующий subcategory в OST_Lines
                    // ------------------------------------------------------------
                    Category targetSubCategory = linesCategory
                        .SubCategories
                        .Cast<Category>()
                        .FirstOrDefault(x => x.Name == styleName);

                    if (targetSubCategory == null)
                        continue;

                    // ------------------------------------------------------------
                    // 8. Получаем GraphicsStyle из категории
                    // ------------------------------------------------------------
                    GraphicsStyle targetGs =
                        targetSubCategory.GetGraphicsStyle(GraphicsStyleType.Projection);

                    if (targetGs == null)
                        continue;

                    // ------------------------------------------------------------
                    // 9. ПРИМЕНЯЕМ СТИЛЬ (правильный API способ)
                    // ------------------------------------------------------------
                    dc.LineStyle = targetGs;
                }

                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}