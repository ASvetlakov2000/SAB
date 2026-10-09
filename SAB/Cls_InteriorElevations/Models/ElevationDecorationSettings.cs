using Autodesk.Revit.DB;

namespace SAB.InteriorElevations.Models
{
    public enum ElevationAnnotationSide
    {
        Left = 0,
        Right = 1
    }

    public class ElevationDecorationSettings
    {
        public bool PlaceSpotElevations { get; set; }

        public bool PlaceTags { get; set; }

        public bool PlaceDimensions { get; set; }

        public bool PlaceWallTags { get; set; }

        public bool PlaceFloorTags { get; set; }

        public bool PlaceCeilingTags { get; set; }

        public bool PlacePlinthTags { get; set; }

        public bool PlaceDoorTags { get; set; }

        public bool PlaceWindowTags { get; set; }

        public bool PlaceVerticalDimensions { get; set; }

        public bool PlaceHorizontalDimensions { get; set; }

        public bool PlaceFinishFloorSpotElevation { get; set; }

        public bool PlaceSubfloorSpotElevations { get; set; }

        public bool PlaceStructuralBaseSpotElevation { get; set; }

        public ElevationAnnotationSide AnnotationSide { get; set; }

        public double SpotOffsetPaperMm { get; set; }

        public double TagOffsetPaperMm { get; set; }

        public double DetailedDimensionOffsetPaperMm { get; set; }

        public double OverallDimensionOffsetPaperMm { get; set; }

        public double WidthDimensionOffsetPaperMm { get; set; }

        public ElementId FloorSpotElevationTypeId { get; set; }

        public ElementId OverheadSpotElevationTypeId { get; set; }

        public ElementId SpotElevationRelativeBaseLevelId { get; set; }

        public ElementId DetailedDimensionTypeId { get; set; }

        public ElementId OverallDimensionTypeId { get; set; }

        public ElementId WidthDimensionTypeId { get; set; }

        public ElementId WallTagTypeId { get; set; }

        public ElementId FloorTagTypeId { get; set; }

        public ElementId CeilingTagTypeId { get; set; }

        public ElementId PlinthTagTypeId { get; set; }

        public ElementId DoorTagTypeId { get; set; }

        public ElementId WindowTagTypeId { get; set; }

        public bool WallTagHasLeader { get; set; }

        public bool FloorTagHasLeader { get; set; }

        public bool CeilingTagHasLeader { get; set; }

        public bool PlinthTagHasLeader { get; set; }

        public bool DoorTagHasLeader { get; set; }

        public bool WindowTagHasLeader { get; set; }
    }

    public class ElevationDecorationResult
    {
        public int ViewsProcessed { get; set; }

        public int DeletedOwnedAnnotations { get; set; }

        public int SpotElevationsCreated { get; set; }

        public int TagsCreated { get; set; }

        public int DimensionsCreated { get; set; }

        public int FailedItems { get; set; }
    }
}
