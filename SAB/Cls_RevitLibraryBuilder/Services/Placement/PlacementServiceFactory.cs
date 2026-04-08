using Autodesk.Revit.DB;
using RevitLibraryBuilder.Services.Placement;

namespace RevitLibraryBuilder.Services.Placement
{
    public static class PlacementServiceFactory
    {
        public static IPlacementService Create(string mode, Document doc)
        {
            switch (mode)
            {
                case "Point": return new PlacementByPointService(doc);
                case "Line": return new PlacementByLineService(doc);
                case "Boundary": return new PlacementByBoundaryService(doc);
                default: throw new System.Exception("Unknown mode");
            }
        }
    }
}