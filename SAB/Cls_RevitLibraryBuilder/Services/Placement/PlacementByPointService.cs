using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitLibraryBuilder.Models;
using System.Collections.Generic;

namespace RevitLibraryBuilder.Services.Placement
{
    public class PlacementByPointService : IPlacementService
    {
        private readonly Document _doc;

        // ÐÐ°ÑÑ‚Ñ€Ð°Ð¸Ð²Ð°ÐµÐ¼Ñ‹Ð¹ ÑˆÐ°Ð³ Ð¼ÐµÐ¶Ð´Ñƒ ÑÐºÐ·ÐµÐ¼Ð¿Ð»ÑÑ€Ð°Ð¼Ð¸ Ð¿Ñ€Ð¸ Ñ€Ð°Ð·Ð¼ÐµÑ‰ÐµÐ½Ð¸Ð¸ Ð¿Ð¾ Ñ‚Ð¾Ñ‡ÐºÐ°Ð¼ (Ð¼Ð¼)
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

                    if (row == null)
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

