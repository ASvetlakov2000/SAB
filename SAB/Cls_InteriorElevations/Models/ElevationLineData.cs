using Autodesk.Revit.DB;

namespace SAB.InteriorElevations.Models
{
    public class ElevationLineData
    {
        public ElementId LineElementId { get; set; }

        public Curve SourceCurve { get; set; }

        public XYZ StartPoint { get; set; }

        public XYZ EndPoint { get; set; }

        public XYZ MidPoint { get; set; }

        public XYZ LineDirection { get; set; }

        public XYZ InsideNormal { get; set; }

        public XYZ MarkerPoint { get; set; }

        public double LineLength { get; set; }

        public int Index { get; set; }

        public RoomData RoomData { get; set; }
    }
}
