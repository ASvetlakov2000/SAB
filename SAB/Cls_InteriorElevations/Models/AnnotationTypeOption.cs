using Autodesk.Revit.DB;

namespace SAB.InteriorElevations.Models
{
    public class AnnotationTypeOption
    {
        public ElementId Id { get; set; }

        public string DisplayName { get; set; }

        public override string ToString()
        {
            return DisplayName ?? string.Empty;
        }
    }
}
