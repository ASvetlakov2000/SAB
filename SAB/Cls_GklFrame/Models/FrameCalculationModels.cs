using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SAB.GklFrame.Models
{
    public sealed class ProfileMetadata
    {
        public string ProfileFamily { get; set; }
        public string ProfileType { get; set; }
        public string ProfileSize { get; set; }
        public double NominalWidthMm { get; set; }
        public double NominalFlangeHeightMm { get; set; }
        public double NominalThicknessMm { get; set; }
    }

    public sealed class PurchaseProfileOption
    {
        public PurchaseProfileOption()
        {
            AvailableLengthsMm = new List<double>();
        }

        public string ProfileType { get; set; }
        public IList<double> AvailableLengthsMm { get; private set; }
    }

    public sealed class FrameMemberData
    {
        public ElementId ElementId { get; set; }
        public string UniqueId { get; set; }
        public string SourceWallUniqueId { get; set; }
        public string SourceOpeningUniqueId { get; set; }
        public string SourceWallType { get; set; }
        public string SourceWallMark { get; set; }
        public string SourceLevel { get; set; }
        public FrameMemberPurpose Purpose { get; set; }
        public string ProfileFamily { get; set; }
        public string ProfileType { get; set; }
        public string ProfileSize { get; set; }
        public double NominalWidthMm { get; set; }
        public double NominalFlangeHeightMm { get; set; }
        public double NominalThicknessMm { get; set; }
        public double ActualLengthInternal { get; set; }
        public double ActualLengthMm { get; set; }
        public double PurchaseLengthMm { get; set; }
    }

    public sealed class CutPiece
    {
        public ElementId SourceElementId { get; set; }
        public int PieceNumber { get; set; }
        public double LengthMm { get; set; }
    }

    public sealed class StockBarCut
    {
        public StockBarCut()
        {
            Pieces = new List<CutPiece>();
        }

        public int BarNumber { get; set; }
        public double StockLengthMm { get; set; }
        public IList<CutPiece> Pieces { get; private set; }
        public double UsedLengthMm { get; set; }
        public double CutLossMm { get; set; }
        public double RemainingLengthMm { get; set; }
        public double ReusableRemainderMm { get; set; }
        public double WasteMm { get; set; }
    }

    public sealed class CuttingPlan
    {
        public CuttingPlan()
        {
            Bars = new List<StockBarCut>();
            CannotBeCutFromSelectedStockLength = new List<CutPiece>();
        }

        public string ProfileFamily { get; set; }
        public string ProfileType { get; set; }
        public string ProfileSize { get; set; }
        public double StockLengthMm { get; set; }
        public IList<StockBarCut> Bars { get; private set; }
        public IList<CutPiece> CannotBeCutFromSelectedStockLength { get; private set; }
        public double TotalRequiredLengthMm { get; set; }
        public double TotalPurchasedLengthMm { get; set; }
        public double TotalUsedLengthMm { get; set; }
        public double TotalReusableRemainderMm { get; set; }
        public double TotalWasteMm { get; set; }
        public double UtilizationRatio { get; set; }
    }

    public sealed class ProfileSummary
    {
        public string ProfileFamily { get; set; }
        public string ProfileType { get; set; }
        public string ProfileSize { get; set; }
        public int MemberCount { get; set; }
        public double TotalActualLengthMm { get; set; }
        public double PurchaseLengthMm { get; set; }
        public int StockBarCount { get; set; }
        public double TotalPurchasedLengthMm { get; set; }
        public double TotalWasteMm { get; set; }
        public double TotalReusableRemainderMm { get; set; }
        public double UtilizationRatio { get; set; }
    }

    public sealed class FrameCalculationResult
    {
        public FrameCalculationResult()
        {
            Members = new List<FrameMemberData>();
            ProfileSummaries = new List<ProfileSummary>();
            CuttingPlans = new List<CuttingPlan>();
            Warnings = new List<string>();
            Errors = new List<string>();
        }

        public IList<FrameMemberData> Members { get; private set; }
        public IList<ProfileSummary> ProfileSummaries { get; private set; }
        public IList<CuttingPlan> CuttingPlans { get; private set; }
        public IList<string> Warnings { get; private set; }
        public IList<string> Errors { get; private set; }
    }
}
