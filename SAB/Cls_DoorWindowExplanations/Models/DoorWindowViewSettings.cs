using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SAB.DoorWindowExplanations.Models
{
    public enum DoorWindowBoundsSourceMode
    {
        Automatic = 0,
        ManualFrontContour = 1
    }

    public enum DoorWindowElevationSide
    {
        Front = 0,
        Back = 1
    }

    public enum DoorWindowWorkflowMode
    {
        FullExplication = 0,
        ImageOnly = 1
    }

    public enum DoorWindowSingleViewKind
    {
        Front = 0,
        Top = 1,
        Section = 2
    }

    public enum CurtainWallFrontSideMode
    {
        RevitExterior = 0,
        RevitInterior = 1,
        AwayFromInteriorPoint = 2
    }

    public enum DoorWindowHorizontalDimensionSide
    {
        Bottom = 0,
        Top = 1
    }

    public enum DoorWindowVerticalDimensionSide
    {
        Left = 0,
        Right = 1
    }

    public enum DoorWindowViewTitleAnchor
    {
        BottomLeft = 0,
        BottomCenter = 1,
        BottomRight = 2,
        TopCenter = 3
    }

    public class DoorWindowViewSettings
    {
        public DoorWindowViewSettings()
        {
            WorkflowMode = DoorWindowWorkflowMode.FullExplication;
            SingleViewKind = DoorWindowSingleViewKind.Front;
            CurtainWallFrontSideMode = CurtainWallFrontSideMode.AwayFromInteriorPoint;
            BoundsSourceMode = DoorWindowBoundsSourceMode.Automatic;
            ElevationSide = DoorWindowElevationSide.Front;
            TopViewNameFormula = "{Категория}_{Позиция}_Сверху";
            FrontViewNameFormula = "{Категория}_{Позиция}_{Сторона}";
            SectionViewNameFormula = "{Категория}_{Позиция}_Разрез";
            TopViewScale = 20;
            FrontViewScale = 20;
            SectionViewScale = 20;
            TopProjectionDepthMm = 1500.0;
            FrontProjectionDepthMm = 500.0;
            TopCutHeightPercent = 50.0;
            WidthCropOffsetMm = 100.0;
            PlanDepthCropOffsetMm = 100.0;
            FrontVerticalCropOffsetMm = 100.0;
            SectionCropOffsetMm = 100.0;
            ManualPlanDepthMm = 400.0;
            SectionProjectionDepthMm = 500.0;
            SectionMarkerExtensionMm = 50.0;
            SheetNumber = "ЭД-1";
            SheetName = "Экспликации дверей, окон и витражей";
            ViewHorizontalStepMm = 120.0;
            ElementVerticalStepMm = 100.0;
            ViewTitleAnchor = DoorWindowViewTitleAnchor.BottomLeft;
            ViewTitleOffsetXmm = 0.0;
            ViewTitleOffsetYmm = -5.0;
            ViewportTypeIdValue = -1;
            IsolateSelectedElement = true;
            CreateFrontDimensions = true;
            DetailedDimensionOffsetPaperMm = 8.0;
            OverallDimensionOffsetPaperMm = 15.0;
            SideDetailedDimensionOffsetPaperMm = 8.0;
            SideOverallDimensionOffsetPaperMm = 15.0;
            HorizontalDimensionSide = DoorWindowHorizontalDimensionSide.Bottom;
            VerticalDimensionSide = DoorWindowVerticalDimensionSide.Left;
            DimensionTypeIdValue = -1;
            DimensionTextHeightMm = 0.0;
            CreateElementImages = true;
            CurtainWallInstanceImageParameterName = "SAB_IMG_Эземпляр";
            DoorWindowTypeImageParameterName = "SAB_IMG_Тип";
            ImagePixelSize = 2000;
            ImageHeightPaperMm = 80.0;
            SaveSettings = true;
        }

        public DoorWindowWorkflowMode WorkflowMode { get; set; }

        public DoorWindowSingleViewKind SingleViewKind { get; set; }

        public CurtainWallFrontSideMode CurtainWallFrontSideMode { get; set; }

        public DoorWindowBoundsSourceMode BoundsSourceMode { get; set; }

        public DoorWindowElevationSide ElevationSide { get; set; }

        public string PositionParameterName { get; set; }

        public string TopViewNameFormula { get; set; }

        public string FrontViewNameFormula { get; set; }

        public string SectionViewNameFormula { get; set; }

        public string TopViewTemplateName { get; set; }

        public string FrontViewTemplateName { get; set; }

        public string SectionViewTemplateName { get; set; }

        public int TopViewScale { get; set; }

        public int FrontViewScale { get; set; }

        public int SectionViewScale { get; set; }

        public double TopProjectionDepthMm { get; set; }

        public double FrontProjectionDepthMm { get; set; }

        public double TopCutHeightPercent { get; set; }

        public double WidthCropOffsetMm { get; set; }

        public double PlanDepthCropOffsetMm { get; set; }

        public double FrontVerticalCropOffsetMm { get; set; }

        public double SectionCropOffsetMm { get; set; }

        public double ManualPlanDepthMm { get; set; }

        public double SectionProjectionDepthMm { get; set; }

        public double SectionMarkerExtensionMm { get; set; }

        public string TitleBlockTypeName { get; set; }

        public string ViewportTypeName { get; set; }

        public int ViewportTypeIdValue { get; set; }

        public string SheetNumber { get; set; }

        public string SheetName { get; set; }

        public double ViewHorizontalStepMm { get; set; }

        public double ElementVerticalStepMm { get; set; }

        public DoorWindowViewTitleAnchor ViewTitleAnchor { get; set; }

        public double ViewTitleOffsetXmm { get; set; }

        public double ViewTitleOffsetYmm { get; set; }

        public bool IsolateSelectedElement { get; set; }

        public bool CreateFrontDimensions { get; set; }

        public string DimensionTypeName { get; set; }

        public int DimensionTypeIdValue { get; set; }

        public double DimensionTextHeightMm { get; set; }

        public double DetailedDimensionOffsetPaperMm { get; set; }

        public double OverallDimensionOffsetPaperMm { get; set; }

        public double SideDetailedDimensionOffsetPaperMm { get; set; }

        public double SideOverallDimensionOffsetPaperMm { get; set; }

        public DoorWindowHorizontalDimensionSide HorizontalDimensionSide { get; set; }

        public DoorWindowVerticalDimensionSide VerticalDimensionSide { get; set; }

        public bool CreateElementImages { get; set; }

        public string CurtainWallInstanceImageParameterName { get; set; }

        public string DoorWindowTypeImageParameterName { get; set; }

        public int ImagePixelSize { get; set; }

        public double ImageHeightPaperMm { get; set; }

        public bool SaveSettings { get; set; }

        public DoorWindowViewSettings Clone()
        {
            return (DoorWindowViewSettings)MemberwiseClone();
        }
    }

    public class DoorWindowSelectionData
    {
        public Document HostDocument { get; set; }

        public Document SourceDocument { get; set; }

        public Element Element { get; set; }

        public ElementType ElementType { get; set; }

        public RevitLinkInstance LinkInstance { get; set; }

        public Transform SourceToHostTransform { get; set; }

        public XYZ Origin { get; set; }

        public XYZ WidthDirection { get; set; }

        public XYZ FacingDirection { get; set; }

        public bool ReverseFrontSide { get; set; }

        public string CategoryName { get; set; }

        public string FamilyName { get; set; }

        public string TypeName { get; set; }

        public string LinkName { get; set; }

        public bool IsLinked
        {
            get { return LinkInstance != null; }
        }

        public bool IsCurtainWall
        {
            get { return Element is Wall && ((Wall)Element).CurtainGrid != null; }
        }

        public bool IsWindow
        {
            get
            {
                return Element != null && Element.Category != null &&
                       Element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Windows;
            }
        }

        public bool IsDoor
        {
            get
            {
                return Element != null && Element.Category != null &&
                       Element.Category.Id.IntegerValue == (int)BuiltInCategory.OST_Doors;
            }
        }

        public string DisplayName
        {
            get
            {
                string source = IsLinked ? "Связь: " + (LinkName ?? string.Empty) : "Рабочая модель";
                return (CategoryName ?? string.Empty) + " · " +
                       (FamilyName ?? string.Empty) + " · " +
                       (TypeName ?? string.Empty) + "\n" + source;
            }
        }

        public string SourceName
        {
            get { return IsLinked ? "Связь: " + (LinkName ?? string.Empty) : "Рабочая модель"; }
        }

        public string ElementIdText
        {
            get
            {
                int elementId = Element != null && Element.Id != null ? Element.Id.IntegerValue : -1;
                if (!IsLinked)
                {
                    return elementId.ToString();
                }

                int linkId = LinkInstance != null && LinkInstance.Id != null ? LinkInstance.Id.IntegerValue : -1;
                return linkId + ":" + elementId;
            }
        }

        public string SelectionKey
        {
            get { return (IsLinked ? "L:" : "H:") + ElementIdText; }
        }
    }

    public class DoorWindowViewTemplateItem
    {
        public DoorWindowViewTemplateItem(ElementId id, string name)
        {
            Id = id;
            Name = name ?? string.Empty;
        }

        public ElementId Id { get; private set; }

        public string Name { get; private set; }

        public override string ToString()
        {
            return Name;
        }
    }

    public class DoorWindowNamedElementItem
    {
        public DoorWindowNamedElementItem(ElementId id, string name)
            : this(id, name, name)
        {
        }

        public DoorWindowNamedElementItem(ElementId id, string name, string displayName)
        {
            Id = id;
            Name = name ?? string.Empty;
            DisplayName = displayName ?? Name;
        }

        public ElementId Id { get; private set; }

        public string Name { get; private set; }

        public string DisplayName { get; private set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public class DoorWindowOrientedBounds
    {
        public XYZ Origin { get; set; }

        public XYZ WidthDirection { get; set; }

        public XYZ FacingDirection { get; set; }

        public double MinWidth { get; set; }

        public double MaxWidth { get; set; }

        public double MinDepth { get; set; }

        public double MaxDepth { get; set; }

        public double MinHeight { get; set; }

        public double MaxHeight { get; set; }
    }

    public class DoorWindowViewCreationResult
    {
        public XYZ ModelAlignmentPoint { get; set; }

        public ViewSection TopView { get; set; }

        public ViewSection FrontView { get; set; }

        public ViewSection SectionView { get; set; }

        public IList<ViewSection> GetViews()
        {
            List<ViewSection> views = new List<ViewSection>();
            if (TopView != null)
            {
                views.Add(TopView);
            }

            if (FrontView != null)
            {
                views.Add(FrontView);
            }

            if (SectionView != null)
            {
                views.Add(SectionView);
            }

            return views;
        }

        public ViewSection GetSingleView(DoorWindowSingleViewKind viewKind)
        {
            if (viewKind == DoorWindowSingleViewKind.Top)
            {
                return TopView;
            }

            if (viewKind == DoorWindowSingleViewKind.Section)
            {
                return SectionView;
            }

            return FrontView;
        }
    }

    public class DoorWindowBatchCreationResult
    {
        public DoorWindowBatchCreationResult()
        {
            ViewGroups = new List<DoorWindowViewCreationResult>();
            Warnings = new List<string>();
            ExportedImagePaths = new List<string>();
        }

        public IList<DoorWindowViewCreationResult> ViewGroups { get; private set; }

        public ViewSheet Sheet { get; set; }

        public int DimensionsCreated { get; set; }

        public int ImagesAssigned { get; set; }

        public IList<string> Warnings { get; private set; }

        public IList<string> ExportedImagePaths { get; private set; }

        public string ImageOutputFolder { get; set; }
    }
}
