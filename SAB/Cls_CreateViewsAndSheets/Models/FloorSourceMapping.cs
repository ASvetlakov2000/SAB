using Autodesk.Revit.DB;

namespace SAB.CreateViewsAndSheets.Models
{
    public class FloorSourceMapping
    {
        public FloorSourceMapping()
        {
            FloorId = ElementId.InvalidElementId;
            FloorName = string.Empty;
            SourceViewId = ElementId.InvalidElementId;
            SourceSheetId = ElementId.InvalidElementId;
            CeilingSourceViewId = ElementId.InvalidElementId;
            CeilingSourceSheetId = ElementId.InvalidElementId;
        }

        public ElementId FloorId { get; set; }

        public string FloorName { get; set; }

        public ElementId SourceViewId { get; set; }

        public ElementId SourceSheetId { get; set; }

        public ElementId CeilingSourceViewId { get; set; }

        public ElementId CeilingSourceSheetId { get; set; }

        public SheetBounds SheetBounds { get; set; }

        public SheetBounds CeilingSheetBounds { get; set; }
    }
}
