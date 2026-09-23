using Autodesk.Revit.DB;
using SAB.RoomGeometryTools.Models;
using System;
using System.Collections.Generic;

namespace SAB.RoomGeometryTools.Services
{
    /// <summary>
    /// Сервис определения направлений осей помещения.
    /// </summary>
    public class RoomAxisDirectionService
    {
        public bool TryGetAxisDirections(RoomBoundaryPolygon polygon, out XYZ mainDirection, out XYZ secondaryDirection, out string errorMessage)
        {
            mainDirection = XYZ.BasisX;
            secondaryDirection = XYZ.BasisY;
            errorMessage = string.Empty;

            if (polygon == null || polygon.OuterLines == null || polygon.OuterLines.Count == 0)
            {
                errorMessage = "Не найдены линейные сегменты для определения направления осей.";
                return false;
            }

            Line longestLine = GetLongestLine(polygon.OuterLines);
            if (longestLine == null)
            {
                errorMessage = "Не удалось определить самый длинный сегмент границы.";
                return false;
            }

            XYZ vector = longestLine.GetEndPoint(1) - longestLine.GetEndPoint(0);
            XYZ horizontal = new XYZ(vector.X, vector.Y, 0.0);

            if (horizontal.GetLength() < 1e-9)
            {
                errorMessage = "Главное направление оси имеет нулевую длину.";
                return false;
            }

            mainDirection = horizontal.Normalize();
            secondaryDirection = new XYZ(-mainDirection.Y, mainDirection.X, 0.0).Normalize();

            // Блок дополнительной проверки перпендикулярности направлений.
            double dot = Math.Abs(mainDirection.DotProduct(secondaryDirection));
            if (dot > 1e-8)
            {
                errorMessage = "Внутренняя ошибка построения перпендикулярного направления.";
                return false;
            }

            return true;
        }

        private static Line GetLongestLine(IList<Line> lines)
        {
            if (lines == null || lines.Count == 0)
            {
                return null;
            }

            Line longest = null;
            double bestLength = 0.0;

            for (int i = 0; i < lines.Count; i++)
            {
                Line line = lines[i];
                if (line == null)
                {
                    continue;
                }

                double length = line.Length;
                if (length > bestLength)
                {
                    bestLength = length;
                    longest = line;
                }
            }

            return longest;
        }
    }
}

