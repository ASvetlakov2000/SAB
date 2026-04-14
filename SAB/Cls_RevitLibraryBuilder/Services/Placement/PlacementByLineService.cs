using Autodesk.Revit.DB;
using RevitLibraryBuilder.Models;
using System.Collections.Generic;

namespace RevitLibraryBuilder.Services.Placement
{
    public class PlacementByLineService : IPlacementService
    {
        private readonly Document _doc;

        // Настраиваемая длина создаваемой стены (мм)
        private readonly double _length = 2000 / 304.8;
        // Настраиваемая высота создаваемой стены (мм)
        private readonly double _height = 3000 / 304.8;
        // Настраиваемый шаг между линиями размещения по оси Y (футы)
        private readonly double _lineOffset = 5;

        public PlacementByLineService(Document doc)
        {
            _doc = doc;
        }

        public void Place(List<ElementTypeCsvModel> elements, Level level)
        {
            using (Transaction transaction = new Transaction(_doc, "Line Placement"))
            {
                transaction.Start();

                int index = 0;

                for (int i = 0; i < elements.Count; i++)
                {
                    ElementTypeCsvModel row = elements[i];

                    if (row == null || !row.Include)
                    {
                        continue;
                    }

                    WallType wallType = FindWallTypeByName(row.TypeName);

                    if (wallType == null)
                    {
                        continue;
                    }

                    double currentOffsetY = index * _lineOffset;

                    Line line = Line.CreateBound(
                        new XYZ(0, currentOffsetY, 0),
                        new XYZ(_length, currentOffsetY, 0));

                    Wall.Create(_doc, line, wallType.Id, level.Id, _height, 0, false, false);
                    index++;
                }

                transaction.Commit();
            }
        }

        private WallType FindWallTypeByName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            FilteredElementCollector collector = new FilteredElementCollector(_doc);
            collector.OfClass(typeof(WallType));

            foreach (Element element in collector)
            {
                WallType wallType = element as WallType;

                if (wallType == null)
                {
                    continue;
                }

                if (wallType.Name == typeName)
                {
                    return wallType;
                }
            }

            return null;
        }
    }
}
