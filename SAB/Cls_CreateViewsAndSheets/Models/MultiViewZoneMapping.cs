using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace SAB.CreateViewsAndSheets.Models
{
    public class MultiViewZoneMapping
    {
        public MultiViewZoneMapping()
        {
            ZoneName = string.Empty;
            SourceSheetId = ElementId.InvalidElementId;
            ViewportTypeId = ElementId.InvalidElementId;
            TitleBlockTypeId = ElementId.InvalidElementId;
            Floors = new List<MultiViewZoneFloorMapping>();
        }

        public string ZoneName { get; set; }

        public ElementId SourceSheetId { get; set; }

        public ElementId ViewportTypeId { get; set; }

        public ElementId TitleBlockTypeId { get; set; }

        public SheetBounds SheetBounds { get; set; }

        public List<MultiViewZoneFloorMapping> Floors { get; set; }
    }

    public class MultiViewZoneFloorMapping
    {
        public MultiViewZoneFloorMapping()
        {
            FloorName = string.Empty;
            SourceViewId = ElementId.InvalidElementId;
        }

        public string FloorName { get; set; }

        public ElementId SourceViewId { get; set; }
    }
}
