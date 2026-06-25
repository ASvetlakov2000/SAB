using Autodesk.Revit.DB;

namespace SAB.CreateViewsAndSheets.Models
{
    public class RevitElementItem
    {
        public RevitElementItem()
        {
            Id = ElementId.InvalidElementId;
            Name = string.Empty;
            ViewType = ViewType.Undefined;
        }

        public ElementId Id { get; set; }

        public ElementId RelatedElementId { get; set; }

        public string Name { get; set; }

        public string UniqueId { get; set; }

        public ViewType ViewType { get; set; }

        public bool ControlsScale { get; set; }

        public SheetBounds SheetBounds { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
