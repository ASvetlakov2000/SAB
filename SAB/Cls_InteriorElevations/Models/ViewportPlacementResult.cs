using System.Collections.Generic;

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
    }
}
