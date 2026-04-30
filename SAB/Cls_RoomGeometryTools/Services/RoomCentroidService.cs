using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using SAB.RoomGeometryTools.Models;
using SAB.RoomGeometryTools.Utils;

namespace SAB.RoomGeometryTools.Services
{
    /// <summary>
    /// Сервис расчета центроида помещения.
    /// </summary>
    public class RoomCentroidService
    {
        public bool TryCalculateCentroid(Room room, RoomBoundaryPolygon polygon, out XYZ centroid, out string errorMessage)
        {
            centroid = XYZ.Zero;
            errorMessage = string.Empty;

            if (room == null)
            {
                errorMessage = "Помещение не найдено.";
                return false;
            }

            if (polygon == null || polygon.OuterVertices == null || polygon.OuterVertices.Count < 3)
            {
                errorMessage = "Граница помещения недоступна для расчета центроида.";
                return false;
            }

            XYZ centroid2d;
            if (!PolygonUtils.TryCalculateCentroidXY(polygon.OuterVertices, out centroid2d))
            {
                errorMessage = "Не удалось вычислить центроид помещения.";
                return false;
            }

            // Блок определения рабочей отметки по точке размещения помещения.
            double z = 0.0;
            LocationPoint locationPoint = room.Location as LocationPoint;
            if (locationPoint != null && locationPoint.Point != null)
            {
                z = locationPoint.Point.Z;
            }

            centroid = new XYZ(centroid2d.X, centroid2d.Y, z);
            return true;
        }
    }
}

