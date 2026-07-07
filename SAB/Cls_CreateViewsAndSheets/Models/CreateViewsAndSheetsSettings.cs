using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace SAB.CreateViewsAndSheets.Models
{
    public class CreateViewsAndSheetsSettings
    {
        public CreateViewsAndSheetsSettings()
        {
            SourceViewId = ElementId.InvalidElementId;
            SourceSheetId = ElementId.InvalidElementId;
            CeilingSourceViewId = ElementId.InvalidElementId;
            CeilingSourceSheetId = ElementId.InvalidElementId;
            ViewportTypeId = ElementId.InvalidElementId;
            TitleBlockTypeId = ElementId.InvalidElementId;
            SheetBrowserParameterId = ElementId.InvalidElementId;
            SheetBrowserParameterIds = new List<ElementId>();
            Placement = new PlacementSettings();
            DetailCopy = new SheetDetailCopySettings();
            StructureMode = CreateViewsAndSheetsStructureMode.SingleStory;
            FloorMappings = new List<FloorSourceMapping>();
            MultiViewZoneMappings = new List<MultiViewZoneMapping>();
            SessionRows = new List<SheetCreationSessionRow>();
        }

        public CreateViewsAndSheetsStructureMode StructureMode { get; set; }

        public ElementId SourceViewId { get; set; }

        public ElementId SourceSheetId { get; set; }

        public ElementId CeilingSourceViewId { get; set; }

        public ElementId CeilingSourceSheetId { get; set; }

        public ElementId ViewportTypeId { get; set; }

        public ElementId TitleBlockTypeId { get; set; }

        public ElementId SheetBrowserParameterId { get; set; }

        public List<ElementId> SheetBrowserParameterIds { get; set; }

        public SheetBounds SheetBounds { get; set; }

        public PlacementSettings Placement { get; set; }

        public SheetDetailCopySettings DetailCopy { get; set; }

        public List<FloorSourceMapping> FloorMappings { get; set; }

        public List<MultiViewZoneMapping> MultiViewZoneMappings { get; set; }

        public List<SheetCreationSessionRow> SessionRows { get; set; }
    }
}
