using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SAB.GklFrame.Models
{
    public enum FrameMemberPurpose
    {
        Stud,
        BottomTrack,
        TopTrack,
        DoorJamb,
        DoorHeader,
        ReinforcedDoorJamb,
        OpeningJamb,
        OpeningHeader,
        OpeningSill,
        HorizontalBrace,
        Custom
    }

    public enum OpeningType
    {
        Door,
        Window,
        Custom
    }

    public enum FrameCommandMode
    {
        Generate,
        Calculate
    }

    public sealed class ElementTypeOption
    {
        public ElementTypeOption(ElementId id, string displayName)
        {
            Id = id;
            DisplayName = displayName ?? string.Empty;
        }

        public ElementId Id { get; private set; }
        public string DisplayName { get; private set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public sealed class FrameGenerationOptions
    {
        public FrameGenerationOptions()
        {
            StudSpacingMm = FrameModuleConstants.DefaultStudSpacingMm;
            CalculationCurtainWallTypeName = FrameModuleConstants.DefaultCurtainWallTypeName;
            ProcessDoors = true;
        }

        public double StudSpacingMm { get; set; }
        public string CalculationCurtainWallTypeName { get; set; }
        public ElementId CalculationCurtainWallTypeId { get; set; }
        public Document MullionCatalogDocument { get; set; }
        public bool ProcessDoors { get; set; }
    }

    public sealed class FrameCalculationOptions
    {
        public FrameCalculationOptions()
        {
            CwPurchaseLengthMm = FrameModuleConstants.DefaultPurchaseLengthMm;
            UwPurchaseLengthMm = FrameModuleConstants.DefaultPurchaseLengthMm;
            CutLossMm = 0.0;
            MinimumReusableOffcutMm = FrameModuleConstants.DefaultMinimumReusableOffcutMm;
            PerformCutting = true;
        }

        public double CwPurchaseLengthMm { get; set; }
        public double UwPurchaseLengthMm { get; set; }
        public double CutLossMm { get; set; }
        public double MinimumReusableOffcutMm { get; set; }
        public bool PerformCutting { get; set; }

        public double GetPurchaseLengthMm(string profileFamily)
        {
            return string.Equals(profileFamily, "UW", StringComparison.OrdinalIgnoreCase)
                ? UwPurchaseLengthMm
                : CwPurchaseLengthMm;
        }
    }

    public sealed class WallOpeningData
    {
        public ElementId SourceElementId { get; set; }
        public string SourceUniqueId { get; set; }
        public double StartOffsetInternal { get; set; }
        public double EndOffsetInternal { get; set; }
        public double BottomOffsetInternal { get; set; }
        public double TopOffsetInternal { get; set; }
        public OpeningType OpeningType { get; set; }
    }

    public sealed class SourceWallData
    {
        public Wall SourceWall { get; set; }
        public ElementId SourceElementId { get; set; }
        public string SourceUniqueId { get; set; }
        public Line LocationLine { get; set; }
        public XYZ StartPoint { get; set; }
        public XYZ Direction { get; set; }
        public ElementId BaseLevelId { get; set; }
        public double BaseElevationInternal { get; set; }
        public double HeightInternal { get; set; }
        public double LengthInternal { get; set; }
        public double CoreWidthInternal { get; set; }
        public string WallTypeName { get; set; }
        public string WallMark { get; set; }
        public string LevelName { get; set; }
        public IList<WallOpeningData> Openings { get; set; }
        public IList<double> TConnectionOffsetsInternal { get; set; }
    }

    public sealed class GeneratedFrameData
    {
        public GeneratedFrameData()
        {
            MullionIds = new List<ElementId>();
            Warnings = new List<string>();
        }

        public ElementId SourceWallId { get; set; }
        public ElementId CalculationWallId { get; set; }
        public IList<ElementId> MullionIds { get; private set; }
        public IList<string> Warnings { get; private set; }
    }

    public sealed class FrameGenerationResult
    {
        public FrameGenerationResult()
        {
            GeneratedFrames = new List<GeneratedFrameData>();
            Warnings = new List<string>();
            Errors = new List<string>();
        }

        public int RequestedWallCount { get; set; }
        public int SkippedWallCount { get; set; }
        public int DoorCount { get; set; }
        public int OpeningCount { get; set; }
        public IList<GeneratedFrameData> GeneratedFrames { get; private set; }
        public IList<string> Warnings { get; private set; }
        public IList<string> Errors { get; private set; }

        public int CreatedFrameCount
        {
            get { return GeneratedFrames.Count; }
        }
    }

    public sealed class FrameModuleSettings
    {
        public FrameModuleSettings()
        {
            StudSpacingMm = FrameModuleConstants.DefaultStudSpacingMm;
            CurtainWallTypeName = FrameModuleConstants.DefaultCurtainWallTypeName;
            ProcessDoors = true;
            CwPurchaseLengthMm = FrameModuleConstants.DefaultPurchaseLengthMm;
            UwPurchaseLengthMm = FrameModuleConstants.DefaultPurchaseLengthMm;
            CutLossMm = 0.0;
            MinimumReusableOffcutMm = FrameModuleConstants.DefaultMinimumReusableOffcutMm;
            PerformCutting = true;
            ExportCsv = true;
        }

        public double StudSpacingMm { get; set; }
        public string CurtainWallTypeName { get; set; }
        public bool ProcessDoors { get; set; }
        public double CwPurchaseLengthMm { get; set; }
        public double UwPurchaseLengthMm { get; set; }
        public double CutLossMm { get; set; }
        public double MinimumReusableOffcutMm { get; set; }
        public bool PerformCutting { get; set; }
        public bool ExportCsv { get; set; }
        public string ExportDirectory { get; set; }
    }
}
