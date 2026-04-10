using Autodesk.Revit.DB;
using System.Linq;

namespace Services.Placement
{
    public class LineStyleApplier
    {
        /// <summary>
        /// Назначает Line Style (GraphicsStyle) для DetailCurve
        /// </summary>
        public static void ApplyLineStyle(Document doc, DetailCurve dc, string lineStyleName)
        {
            // ------------------------------------------------------------
            // 1. Получаем категорию Lines
            // ------------------------------------------------------------
            Category linesCat = doc.Settings.Categories
                .get_Item(BuiltInCategory.OST_Lines);

            // ------------------------------------------------------------
            // 2. Ищем нужный subcategory (это и есть GraphicsStyle)
            // ------------------------------------------------------------
            Category subCat = linesCat
                .SubCategories
                .Cast<Category>()
                .FirstOrDefault(x => x.Name == lineStyleName);

            if (subCat == null)
            {
                subCat = linesCat.SubCategories
                    .Cast<Category>()
                    .FirstOrDefault();
            }

            if (subCat == null)
                return;

            // ------------------------------------------------------------
            // 3. Получаем GraphicsStyle
            // ------------------------------------------------------------
            GraphicsStyle gs = subCat.GetGraphicsStyle(GraphicsStyleType.Projection);

            if (gs == null)
                return;

            // ------------------------------------------------------------
            // 4. ПРАВИЛЬНОЕ назначение стиля
            // ------------------------------------------------------------
            dc.LineStyle = gs;
        }
    }
}