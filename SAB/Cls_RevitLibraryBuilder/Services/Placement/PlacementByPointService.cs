using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitLibraryBuilder.Models;
using System.Collections.Generic;
using System.Linq;

namespace RevitLibraryBuilder.Services.Placement
{
    public class PlacementByPointService : IPlacementService
    {
        private readonly Document _doc;

        private readonly double _step = 2000 / 304.8;

        public PlacementByPointService(Document doc)
        {
            _doc = doc;
        }

        public void Place(List<ElementTypeCsvModel> elements, Level level)
        {
            using (Transaction t = new Transaction(_doc, "Point Placement"))
            {
                t.Start();

                XYZ p = new XYZ(0, 0, 0);

                foreach (var e in elements.Where(x => x.Include))
                {
                    var symbol = new FilteredElementCollector(_doc)
                        .OfClass(typeof(FamilySymbol))
                        .Cast<FamilySymbol>()
                        .FirstOrDefault(s => s.Name == e.TypeName);

                    if (symbol == null)
                        continue;

                    if (!symbol.IsActive)
                        symbol.Activate();

                    _doc.Create.NewFamilyInstance(p, symbol, level, StructuralType.NonStructural);

                    p = new XYZ(p.X + _step, p.Y, p.Z);
                }

                t.Commit();
            }
        }
    }
}