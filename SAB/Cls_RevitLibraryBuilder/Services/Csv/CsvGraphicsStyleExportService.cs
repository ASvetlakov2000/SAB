using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

public class LineStyleExportService
{
    private readonly Document _doc;

    public LineStyleExportService(Document doc)
    {
        _doc = doc;
    }

    public List<LineStyleDto> GetLineStyles()
    {
        List<LineStyleDto> result = new List<LineStyleDto>();

        // 1. Берём категорию Lines
        Category linesCategory = _doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);

        if (linesCategory == null)
            return result;

        // 2. Все Line Styles = SubCategories
        foreach (Category subCat in linesCategory.SubCategories)
        {
            // =========================
            // 🔹 GraphicsStyle (ВАЖНО!)
            // =========================
            GraphicsStyle gs = subCat.GetGraphicsStyle(GraphicsStyleType.Projection);

            if (gs == null)
                continue;

            Category gsCategory = gs.GraphicsStyleCategory;

            if (gsCategory == null)
                continue;

            // =========================
            // 🔹 Name
            // =========================
            string name = gsCategory.Name;

            // =========================
            // 🔹 Color (из Category)
            // =========================
            Color color = gsCategory.LineColor;

            int r = 0, g = 0, b = 0;

            if (color != null && color.IsValid)
            {
                r = color.Red;
                g = color.Green;
                b = color.Blue;
            }



            // =========================
            // 🔹 Line Pattern
            // =========================
            string patternName = "Solid";

            ElementId patternId = gsCategory.GetLinePatternId(GraphicsStyleType.Projection);

            if (patternId != ElementId.InvalidElementId)
            {
                LinePatternElement pattern = _doc.GetElement(patternId) as LinePatternElement;

                if (pattern != null)
                    patternName = pattern.Name;
            }

            // =========================
            // 🔹 Result
            // =========================
            result.Add(new LineStyleDto
            {
                Name = name,
                ColorR = r,
                ColorG = g,
                ColorB = b,

                PatternName = patternName
            });
        }

        return result;
    }
}

public class LineStyleDto
{
    public string Name { get; set; }

    public int ColorR { get; set; }
    public int ColorG { get; set; }
    public int ColorB { get; set; }

    public int LineWeight { get; set; }

    public string PatternName { get; set; }
}