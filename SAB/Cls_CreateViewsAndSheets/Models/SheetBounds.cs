using System;
using SAB.InteriorElevations.Utils;

namespace SAB.CreateViewsAndSheets.Models
{
    public class SheetBounds
    {
        public double MinXFeet { get; set; }

        public double MinYFeet { get; set; }

        public double MaxXFeet { get; set; }

        public double MaxYFeet { get; set; }

        public double WidthFeet
        {
            get { return Math.Abs(MaxXFeet - MinXFeet); }
        }

        public double HeightFeet
        {
            get { return Math.Abs(MaxYFeet - MinYFeet); }
        }

        public double WidthMm
        {
            get { return UnitConversionUtils.FeetToMillimeters(WidthFeet); }
        }

        public double HeightMm
        {
            get { return UnitConversionUtils.FeetToMillimeters(HeightFeet); }
        }

        public string OrientationName
        {
            get
            {
                if (WidthFeet > HeightFeet)
                {
                    return "Горизонтальная";
                }

                if (HeightFeet > WidthFeet)
                {
                    return "Вертикальная";
                }

                return "Квадратная";
            }
        }

        public string FormatName { get; set; }

        public bool ContainsPointMm(double xMm, double yMm)
        {
            double xFeet = UnitConversionUtils.MillimetersToFeet(xMm);
            double yFeet = UnitConversionUtils.MillimetersToFeet(yMm);

            return xFeet >= -1e-6 &&
                   yFeet >= -1e-6 &&
                   xFeet <= WidthFeet + 1e-6 &&
                   yFeet <= HeightFeet + 1e-6;
        }
    }
}
