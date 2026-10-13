using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace SAB.InteriorElevations.Models
{
    public enum ElevationAnnotationSide
    {
        Left = 0,
        Right = 1
    }

    public class ElevationDecorationSettings
    {
        public IEnumerable<ElevationDecorationCatalogRole> RequiredCatalogRoles()
        {
            var roles = new HashSet<ElevationDecorationCatalogRole>();
            if (PlaceSpotElevations && PlaceFloorSpots) {
                if (PlaceFinishFloorSpotElevation) roles.Add(ElevationDecorationCatalogRole.FinishFloor);
                if (PlaceSubfloorSpotElevations) roles.Add(ElevationDecorationCatalogRole.Subfloor);
                if (PlaceStructuralBaseSpotElevation) roles.Add(ElevationDecorationCatalogRole.StructuralSlab);
            }
            if (PlaceSpotElevations && PlaceCeilingSpots) roles.Add(ElevationDecorationCatalogRole.Ceiling);
            if (PlaceSpotElevations && PlaceSlabSpots) roles.Add(ElevationDecorationCatalogRole.StructuralSlab);
            if (PlaceDimensions) {
                if (PlaceHorizontalDimensions) roles.Add(ElevationDecorationCatalogRole.FinishWall);
                if (PlaceVerticalDimensions) { roles.Add(ElevationDecorationCatalogRole.FinishFloor); roles.Add(ElevationDecorationCatalogRole.Ceiling); roles.Add(ElevationDecorationCatalogRole.StructuralSlab); }
            }
            if (PlaceTags) {
                if (PlaceWallTags) roles.Add(ElevationDecorationCatalogRole.FinishWall);
                if (PlaceFloorTags) roles.Add(ElevationDecorationCatalogRole.FinishFloor);
                if (PlaceCeilingTags) roles.Add(ElevationDecorationCatalogRole.Ceiling);
                if (PlacePlinthTags) roles.Add(ElevationDecorationCatalogRole.Plinth);
                if (PlaceDoorTags) roles.Add(ElevationDecorationCatalogRole.Door);
                if (PlaceWindowTags) roles.Add(ElevationDecorationCatalogRole.Window);
            }
            return roles;
        }

        public bool PlaceSpotElevations { get; set; }

        public bool PlaceFloorSpots { get; set; } = true;

        public bool PlaceCeilingSpots { get; set; } = true;

        public bool PlaceSlabSpots { get; set; } = true;

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
