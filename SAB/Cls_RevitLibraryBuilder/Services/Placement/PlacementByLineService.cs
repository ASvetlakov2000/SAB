using Autodesk.Revit.DB;
using RevitLibraryBuilder.Models;
using System.Collections.Generic;
using System.Linq;

namespace RevitLibraryBuilder.Services.Placement
{
    public class PlacementByLineService : IPlacementService
    {
        private readonly Document _doc;

        private readonly double _length = 2000 / 304.8;
        private readonly double _height = 3000 / 304.8;

        public PlacementByLineService(Document doc)
        {
            _doc = doc;
        }

        public void Place(List<ElementTypeCsvModel> elements, Level level)
        {
            using (Transaction t = new Transaction(_doc, "Line Placement"))
            {
                t.Start();

                int i = 0;

                foreach (var e in elements.Where(x => x.Include))
                {
                    var wallType = new FilteredElementCollector(_doc)
                        .OfClass(typeof(WallType))
                        .Cast<WallType>()
                        .FirstOrDefault(w => w.Name == e.TypeName);

                    if (wallType == null)
                        continue;

                    Line line = Line.CreateBound(
                        new XYZ(0, i * 5, 0),
                        new XYZ(_length, i * 5, 0)
                    );

                    Wall.Create(_doc, line, wallType.Id, level.Id, _height, 0, false, false);

                    i++;
                }

                t.Commit();
            }
        }
    }
}