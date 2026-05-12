using Autodesk.Revit.DB;

namespace SAB.InteriorElevations.Models
{
    /// <summary>
    /// Настройки модуля создания план-схем разверток помещений.
    /// </summary>
    public class RoomPlanSchemeSettings
    {
        public string NamePart1 { get; set; }

        public string NamePart2 { get; set; }

        public string NamePart3 { get; set; }

        public ElementId ViewTemplateId { get; set; }

        public int ViewScale { get; set; }

        public double CropOffsetMm { get; set; }
    }
}
