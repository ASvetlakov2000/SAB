using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SAB.CreateViewsAndSheets.Models
{
    public class SheetCreationSessionRow
    {
        public SheetCreationSessionRow()
        {
            PlanKind = SheetPlanKind.StandardPlan;
            FloorName = string.Empty;
            ViewName = string.Empty;
            ViewScaleText = "50";
            ViewTemplateId = ElementId.InvalidElementId;
            SheetNumber = string.Empty;
            SheetName = string.Empty;
            SheetBrowserParameterValue = string.Empty;
            SheetBrowserParameterValues = new List<SheetBrowserParameterValueItem>();
        }

        public SheetPlanKind PlanKind { get; set; }

        public string FloorName { get; set; }

        public string ViewName { get; set; }

        public string ViewScaleText { get; set; }

        public ElementId ViewTemplateId { get; set; }

        public string SheetNumber { get; set; }

        public string SheetName { get; set; }

        public string SheetBrowserParameterValue { get; set; }

        public List<SheetBrowserParameterValueItem> SheetBrowserParameterValues { get; set; }
    }
}
