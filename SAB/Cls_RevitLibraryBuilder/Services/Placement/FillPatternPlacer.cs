using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Services.Placement
{
    public class FillPatternPlacer
    {
        public static void Place(Document doc, View view)
        {
            var patterns = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .ToList();

            int i = 0;

            double mm = 1.0 / 304.8;

            foreach (var pattern in patterns)
            {
                FillPattern fp = pattern.GetFillPattern();
                if (fp == null) continue;

                XYZ basePt = Helpers.PlacementMath.Grid(i, 1500);

                XYZ p1 = basePt;
                XYZ p2 = basePt + new XYZ(1000 * mm, 0, 0);
                XYZ p3 = basePt + new XYZ(1000 * mm, 1000 * mm, 0);
                XYZ p4 = basePt + new XYZ(0, 1000 * mm, 0);

                // 🔥 ВАЖНО: CurveLoop (НЕ CurveArray и НЕ IList<Curve>)
                CurveLoop loop = new CurveLoop();
                loop.Append(Line.CreateBound(p1, p2));
                loop.Append(Line.CreateBound(p2, p3));
                loop.Append(Line.CreateBound(p3, p4));
                loop.Append(Line.CreateBound(p4, p1));

                IList<CurveLoop> loops = new List<CurveLoop> { loop };

                FilledRegion.Create(
                    doc,
                    view.Id,
                    GetOrCreateType(doc, pattern).Id,
                    loops
                );

                i++;
            }
        }

        private static FilledRegionType GetOrCreateType(Document doc, FillPatternElement pattern)
        {
            var existing = new FilteredElementCollector(doc)
                .OfClass(typeof(FilledRegionType))
                .Cast<FilledRegionType>()
                .FirstOrDefault(x => x.Name == pattern.Name);

            if (existing != null)
                return existing;

            FilledRegionType baseType = new FilteredElementCollector(doc)
                .OfClass(typeof(FilledRegionType))
                .Cast<FilledRegionType>()
                .First();

            FilledRegionType newType = baseType.Duplicate(pattern.Name) as FilledRegionType;

            newType.ForegroundPatternId = pattern.Id;

            return newType;
        }
    }
}