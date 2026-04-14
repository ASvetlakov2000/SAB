using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitLibraryBuilder.Models;
using System.Collections.Generic;

namespace RevitLibraryBuilder.Services.Placement
{
    public class PlacementByPointService : IPlacementService
    {
        private readonly Document _doc;

        // Настраиваемый шаг между экземплярами при размещении по точкам (мм)
        private readonly double _step = 2000 / 304.8;

        public PlacementByPointService(Document doc)
        {
            _doc = doc;
        }

        public void Place(List<ElementTypeCsvModel> elements, Level level)
        {
            using (Transaction transaction = new Transaction(_doc, "Point Placement"))
            {
                transaction.Start();

                XYZ point = new XYZ(0, 0, 0);

                for (int i = 0; i < elements.Count; i++)
                {
                    ElementTypeCsvModel row = elements[i];

                    if (row == null || !row.Include)
                    {
                        continue;
                    }

                    FamilySymbol symbol = FindFamilySymbolByTypeName(row.TypeName);

                    if (symbol == null)
                    {
                        continue;
                    }

                    if (!symbol.IsActive)
                    {
                        symbol.Activate();
                    }

                    _doc.Create.NewFamilyInstance(point, symbol, level, StructuralType.NonStructural);
                    point = new XYZ(point.X + _step, point.Y, point.Z);
                }

                transaction.Commit();
            }
        }

        private FamilySymbol FindFamilySymbolByTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            collector.OfClass(typeof(FamilySymbol));

            foreach (Element element in collector)
            {
                FamilySymbol symbol = element as FamilySymbol;

                if (symbol == null)
                {
                    continue;
                }

                if (symbol.Name == typeName)
                {
                    return symbol;
                }
            }

            return null;
        }
    }
}
