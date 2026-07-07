using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace SAB.CreateViewsAndSheets.Models
{
    public class SheetDeletionItem
    {
        public SheetDeletionItem()
        {
            RowNumber = 0;
            SheetId = ElementId.InvalidElementId;
            SheetNumber = string.Empty;
            SheetName = string.Empty;
            PlacedViewIds = new List<ElementId>();
            PlacedViewNames = new List<string>();
            SheetBrowserParameterValue = string.Empty;
            SheetBrowserParameterValues = new List<SheetBrowserParameterValueItem>();
        }

        public int RowNumber { get; set; }

        public ElementId SheetId { get; set; }

        public string SheetNumber { get; set; }

        public string SheetName { get; set; }

        public List<ElementId> PlacedViewIds { get; set; }

        public List<string> PlacedViewNames { get; set; }

        public string SheetBrowserParameterValue { get; set; }

        public List<SheetBrowserParameterValueItem> SheetBrowserParameterValues { get; set; }
    }
}
