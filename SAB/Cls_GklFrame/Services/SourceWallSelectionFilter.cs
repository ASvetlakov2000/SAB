using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace SAB.GklFrame.Services
{
    public sealed class SourceWallSelectionFilter : ISelectionFilter
    {
        private readonly IFrameMetadataService _metadataService;

        public SourceWallSelectionFilter(IFrameMetadataService metadataService)
        {
            _metadataService = metadataService;
        }

        public bool AllowElement(Element element)
        {
            Wall wall = element as Wall;
            return wall != null &&
                   wall.WallType != null &&
                   wall.WallType.Kind != WallKind.Curtain &&
                   wall.CurtainGrid == null &&
                   !_metadataService.IsCalculationFrame(wall);
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}
