using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SAB.InteriorElevations.Models
{
    /// <summary>
    /// Результат создания план-схем помещений.
    /// </summary>
    public class RoomPlanSchemeCreationSummary
    {
        public RoomPlanSchemeCreationSummary()
        {
            Warnings = new List<string>();
            CreatedViewIds = new List<ElementId>();
        }

        public int ProcessedRoomsCount { get; set; }

        public int CreatedViewsCount { get; set; }

        public int SkippedRoomsCount { get; set; }

        public List<ElementId> CreatedViewIds { get; private set; }

        public List<string> Warnings { get; private set; }

        public bool ManualBoundaryRequired { get; set; }

        public int HelperBoundaryLinesCount { get; set; }
    }
}
