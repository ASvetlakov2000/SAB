using System;
using System.IO;
using Autodesk.Revit.DB;
using Newtonsoft.Json;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Settings
{
    public class ElevationDecorationSettingsStorageService
    {
        private readonly string _settingsFilePath;

        public ElevationDecorationSettingsStorageService()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _settingsFilePath = Path.Combine(
                appDataPath,
                "SAB",
                "InteriorElevations",
                "decoration-settings.json");
        }

        public ElevationDecorationSettings LoadSettings()
        {
            if (!File.Exists(_settingsFilePath))
            {
                return null;
            }

            PersistedSettings persisted = JsonConvert.DeserializeObject<PersistedSettings>(
                File.ReadAllText(_settingsFilePath));
            if (persisted == null || persisted.SchemaVersion < 1 || persisted.SchemaVersion > 8)
            {
                return null;
            }

            return new ElevationDecorationSettings
            {
                PlaceSpotElevations = persisted.PlaceSpotElevations,
                PlaceTags = persisted.PlaceTags,
                PlaceDimensions = persisted.SchemaVersion >= 2 && persisted.PlaceDimensions,
                PlaceWallTags = persisted.SchemaVersion >= 8
                    ? persisted.PlaceWallTags
                    : persisted.PlaceTags,
                PlaceFloorTags = persisted.SchemaVersion >= 8
                    ? persisted.PlaceFloorTags
                    : persisted.PlaceTags,
                PlaceCeilingTags = persisted.SchemaVersion >= 8
                    ? persisted.PlaceCeilingTags
                    : persisted.PlaceTags,
                PlacePlinthTags = persisted.SchemaVersion >= 8
                    ? persisted.PlacePlinthTags
                    : persisted.PlaceTags,
                PlaceDoorTags = persisted.SchemaVersion >= 8
                    ? persisted.PlaceDoorTags
                    : persisted.PlaceTags,
                PlaceWindowTags = persisted.SchemaVersion >= 8
                    ? persisted.PlaceWindowTags
                    : persisted.PlaceTags,
                PlaceVerticalDimensions = persisted.SchemaVersion >= 8
                    ? persisted.PlaceVerticalDimensions
                    : persisted.PlaceDimensions,
                PlaceHorizontalDimensions = persisted.SchemaVersion >= 8
                    ? persisted.PlaceHorizontalDimensions
                    : persisted.PlaceDimensions,
                PlaceFinishFloorSpotElevation = persisted.SchemaVersion >= 5
                    ? persisted.PlaceFinishFloorSpotElevation
                    : true,
                PlaceSubfloorSpotElevations = persisted.SchemaVersion >= 5 &&
                    persisted.PlaceSubfloorSpotElevations,
                PlaceStructuralBaseSpotElevation = persisted.SchemaVersion >= 5
                    ? persisted.PlaceStructuralBaseSpotElevation
                    : true,
                AnnotationSide = persisted.AnnotationSide,
                SpotOffsetPaperMm = persisted.SpotOffsetPaperMm,
                TagOffsetPaperMm = persisted.TagOffsetPaperMm,
                DetailedDimensionOffsetPaperMm = persisted.SchemaVersion >= 2
                    ? persisted.DetailedDimensionOffsetPaperMm
                    : 8.0,
                OverallDimensionOffsetPaperMm = persisted.SchemaVersion >= 2
                    ? persisted.OverallDimensionOffsetPaperMm
                    : 16.0,
                WidthDimensionOffsetPaperMm = persisted.SchemaVersion >= 3
                    ? persisted.WidthDimensionOffsetPaperMm
                    : 10.0,
                FloorSpotElevationTypeId = ToElementId(
                    persisted.SchemaVersion >= 4
                        ? persisted.FloorSpotElevationTypeId
                        : persisted.SpotElevationTypeId),
                OverheadSpotElevationTypeId = ToElementId(
                    persisted.SchemaVersion >= 4
                        ? persisted.OverheadSpotElevationTypeId
                        : persisted.SpotElevationTypeId),
                SpotElevationRelativeBaseLevelId = ToElementId(
                    persisted.SchemaVersion >= 6
                        ? persisted.SpotElevationRelativeBaseLevelId
                        : 0),
                DetailedDimensionTypeId = ToElementId(persisted.DetailedDimensionTypeId),
                OverallDimensionTypeId = ToElementId(persisted.OverallDimensionTypeId),
                WidthDimensionTypeId = ToElementId(persisted.WidthDimensionTypeId),
                WallTagTypeId = ToElementId(persisted.WallTagTypeId),
                FloorTagTypeId = ToElementId(persisted.FloorTagTypeId),
                CeilingTagTypeId = ToElementId(persisted.CeilingTagTypeId),
                PlinthTagTypeId = ToElementId(persisted.PlinthTagTypeId),
                DoorTagTypeId = ToElementId(persisted.DoorTagTypeId),
                WindowTagTypeId = ToElementId(
                    persisted.SchemaVersion >= 7 ? persisted.WindowTagTypeId : 0),
                WallTagHasLeader = persisted.WallTagHasLeader,
                FloorTagHasLeader = persisted.FloorTagHasLeader,
                CeilingTagHasLeader = persisted.CeilingTagHasLeader,
                PlinthTagHasLeader = persisted.PlinthTagHasLeader,
                DoorTagHasLeader = persisted.DoorTagHasLeader,
                WindowTagHasLeader = persisted.SchemaVersion >= 7 &&
                    persisted.WindowTagHasLeader
            };
        }

        public void SaveSettings(ElevationDecorationSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            PersistedSettings persisted = new PersistedSettings
            {
                SchemaVersion = 8,
                PlaceSpotElevations = settings.PlaceSpotElevations,
                PlaceTags = settings.PlaceTags,
                PlaceDimensions = settings.PlaceDimensions,
                PlaceWallTags = settings.PlaceWallTags,
                PlaceFloorTags = settings.PlaceFloorTags,
                PlaceCeilingTags = settings.PlaceCeilingTags,
                PlacePlinthTags = settings.PlacePlinthTags,
                PlaceDoorTags = settings.PlaceDoorTags,
                PlaceWindowTags = settings.PlaceWindowTags,
                PlaceVerticalDimensions = settings.PlaceVerticalDimensions,
                PlaceHorizontalDimensions = settings.PlaceHorizontalDimensions,
                PlaceFinishFloorSpotElevation = settings.PlaceFinishFloorSpotElevation,
                PlaceSubfloorSpotElevations = settings.PlaceSubfloorSpotElevations,
                PlaceStructuralBaseSpotElevation = settings.PlaceStructuralBaseSpotElevation,
                AnnotationSide = settings.AnnotationSide,
                SpotOffsetPaperMm = settings.SpotOffsetPaperMm,
                TagOffsetPaperMm = settings.TagOffsetPaperMm,
                DetailedDimensionOffsetPaperMm = settings.DetailedDimensionOffsetPaperMm,
                OverallDimensionOffsetPaperMm = settings.OverallDimensionOffsetPaperMm,
                WidthDimensionOffsetPaperMm = settings.WidthDimensionOffsetPaperMm,
                FloorSpotElevationTypeId = RevitElementIdUtils.GetElementIdValue(
                    settings.FloorSpotElevationTypeId),
                OverheadSpotElevationTypeId = RevitElementIdUtils.GetElementIdValue(
                    settings.OverheadSpotElevationTypeId),
                SpotElevationRelativeBaseLevelId = RevitElementIdUtils.GetElementIdValue(
                    settings.SpotElevationRelativeBaseLevelId),
                DetailedDimensionTypeId = RevitElementIdUtils.GetElementIdValue(settings.DetailedDimensionTypeId),
                OverallDimensionTypeId = RevitElementIdUtils.GetElementIdValue(settings.OverallDimensionTypeId),
                WidthDimensionTypeId = RevitElementIdUtils.GetElementIdValue(settings.WidthDimensionTypeId),
                WallTagTypeId = RevitElementIdUtils.GetElementIdValue(settings.WallTagTypeId),
                FloorTagTypeId = RevitElementIdUtils.GetElementIdValue(settings.FloorTagTypeId),
                CeilingTagTypeId = RevitElementIdUtils.GetElementIdValue(settings.CeilingTagTypeId),
                PlinthTagTypeId = RevitElementIdUtils.GetElementIdValue(settings.PlinthTagTypeId),
                DoorTagTypeId = RevitElementIdUtils.GetElementIdValue(settings.DoorTagTypeId),
                WindowTagTypeId = RevitElementIdUtils.GetElementIdValue(settings.WindowTagTypeId),
                WallTagHasLeader = settings.WallTagHasLeader,
                FloorTagHasLeader = settings.FloorTagHasLeader,
                CeilingTagHasLeader = settings.CeilingTagHasLeader,
                PlinthTagHasLeader = settings.PlinthTagHasLeader,
                DoorTagHasLeader = settings.DoorTagHasLeader,
                WindowTagHasLeader = settings.WindowTagHasLeader
            };

            Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath));
            File.WriteAllText(
                _settingsFilePath,
                JsonConvert.SerializeObject(persisted, Formatting.Indented));
        }

        private ElementId ToElementId(long value)
        {
            return value > 0
                ? RevitElementIdUtils.CreateElementIdFromLong(value)
                : ElementId.InvalidElementId;
        }

        private class PersistedSettings
        {
            public int SchemaVersion { get; set; }
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
            public long SpotElevationTypeId { get; set; }
            public long FloorSpotElevationTypeId { get; set; }
            public long OverheadSpotElevationTypeId { get; set; }
            public long SpotElevationRelativeBaseLevelId { get; set; }
            public long DetailedDimensionTypeId { get; set; }
            public long OverallDimensionTypeId { get; set; }
            public long WidthDimensionTypeId { get; set; }
            public long WallTagTypeId { get; set; }
            public long FloorTagTypeId { get; set; }
            public long CeilingTagTypeId { get; set; }
            public long PlinthTagTypeId { get; set; }
            public long DoorTagTypeId { get; set; }
            public long WindowTagTypeId { get; set; }
            public bool WallTagHasLeader { get; set; }
            public bool FloorTagHasLeader { get; set; }
            public bool CeilingTagHasLeader { get; set; }
            public bool PlinthTagHasLeader { get; set; }
            public bool DoorTagHasLeader { get; set; }
            public bool WindowTagHasLeader { get; set; }
        }
    }
}
