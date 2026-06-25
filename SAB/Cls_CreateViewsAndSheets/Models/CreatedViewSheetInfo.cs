using Autodesk.Revit.DB;

namespace SAB.CreateViewsAndSheets.Models
{
    public class CreatedViewSheetInfo
    {
        public int RowNumber { get; set; }

        public ElementId ViewId { get; set; }

        public string ViewName { get; set; }

        public ElementId SheetId { get; set; }

        public string SheetNumber { get; set; }

        public string SheetName { get; set; }
    }
}
