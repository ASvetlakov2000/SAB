using Autodesk.Revit.DB;

namespace SAB.CreateViewsAndSheets.Models
{
    public class SheetCreationItem
    {
        public SheetCreationItem()
        {
            RowNumber = 0;
            FloorId = ElementId.InvalidElementId;
            FloorName = string.Empty;
            ViewName = string.Empty;
            ViewTemplateId = ElementId.InvalidElementId;
            ViewScale = 0;
            SheetNumber = string.Empty;
            SheetName = string.Empty;
        }

        public int RowNumber { get; set; }

        public ElementId FloorId { get; set; }

        public string FloorName { get; set; }

        public string ViewName { get; set; }

        public int ViewScale { get; set; }

        public ElementId ViewTemplateId { get; set; }

        public string SheetNumber { get; set; }

        public string SheetName { get; set; }
    }
}
