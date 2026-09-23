using Autodesk.Revit.DB;

namespace SAB.RoomGeometryTools.Models
{
    /// <summary>
    /// Настройки инструмента проверки геометрии помещений.
    /// </summary>
    public class RoomGeometryToolsSettings
    {
        public double AreaDeviationThresholdPercent { get; set; } = 10.0;

        public ElementId SelectedAxisLineStyleId { get; set; } = ElementId.InvalidElementId;

        public string SelectedAxisLineStyleName { get; set; } = string.Empty;

        public ElementId SelectedAngularDimensionTypeId { get; set; } = ElementId.InvalidElementId;

        public string SelectedAngularDimensionTypeName { get; set; } = string.Empty;

        public bool DeletePreviousAxesBeforeCreation { get; set; } = true;

        public bool SkipRoomsWithGeometryErrors { get; set; } = true;
    }
}

