namespace RevitLibraryBuilder.Models
{
    /// <summary>
    /// Identifies an operation available from the unified Library Builder window.
    /// </summary>
    public enum LibraryToolId
    {
        ExportSystemFamilies,
        ExportLoadableFamilies,
        ExportLoadableFamilyThumbnails,
        ExportTypeNaming,
        ImportTypeNaming,
        ExportMaterialNaming,
        ImportMaterialNaming,
        ExportLineStyles,
        PlaceLineStyles,
        ExportLineStylesPreviewPng,
        ExportFillPatterns,
        PlaceFillPatterns,
        ExportFillPatternsPreviewPng,
        PlaceLegendComponentsByCategories,
        ExportSystemFamilyThumbnailTemplate,
        LoadSystemFamilyTypeImages,
        ImportByPoint,
        ImportByLine,
        ImportByBoundary,
        DeleteSelectedTypesAndFamilies,
        GenerateDashboard
    }
}
