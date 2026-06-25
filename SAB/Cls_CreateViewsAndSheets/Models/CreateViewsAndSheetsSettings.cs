using Autodesk.Revit.DB;

namespace SAB.CreateViewsAndSheets.Models
{
    public class CreateViewsAndSheetsSettings
    {
        public CreateViewsAndSheetsSettings()
        {
            SourceViewId = ElementId.InvalidElementId;
            SourceSheetId = ElementId.InvalidElementId;
            ViewportTypeId = ElementId.InvalidElementId;
            TitleBlockTypeId = ElementId.InvalidElementId;
            Placement = new PlacementSettings();
        }

        public ElementId SourceViewId { get; set; }

        public ElementId SourceSheetId { get; set; }

        public ElementId ViewportTypeId { get; set; }

        public ElementId TitleBlockTypeId { get; set; }

        public SheetBounds SheetBounds { get; set; }

        public PlacementSettings Placement { get; set; }
    }
}
