using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SAB.InteriorElevations.Models
{
    public class ViewportPlacementResult
    {
        public ViewportPlacementResult()
        {
            PlacedViewports = new List<PlacedViewportData>();
        }

        public int PlacedCount { get; set; }

        public List<PlacedViewportData> PlacedViewports { get; private set; }

        public List<ViewSheet> Sheets { get; private set; } = new List<ViewSheet>();

        public List<ElementId> UnplacedViewIds { get; private set; } = new List<ElementId>();

        public int PlacedSheetMarkCount { get; set; }

        public bool ForcedPlacementUsed { get; set; }

        public bool AutomaticFallbackUsed { get; set; }
    }
}
