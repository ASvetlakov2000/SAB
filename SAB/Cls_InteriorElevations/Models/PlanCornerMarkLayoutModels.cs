using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace SAB.InteriorElevations.Models
{
    /// <summary>
    /// Положение наконечника встроенной выноски относительно начала координат семейства.
    /// Начало координат будущего семейства находится в центре текста марки.
    /// </summary>
    public enum PlanCornerMarkOrientation
    {
        TipUpperRight = 0,
        TipLowerRight = 1,
        TipUpperLeft = 2,
        TipLowerLeft = 3
    }

    /// <summary>
    /// Геометрия встроенной выноски в миллиметрах на листе.
    /// При расчете размеры умножаются на масштаб плана и переводятся в футы модели.
    /// </summary>
    public class PlanCornerMarkLayoutSettings
    {
        public PlanCornerMarkLayoutSettings()
        {
            HorizontalShoulderPaperMm = 7.5;
            DiagonalProjectionPaperMm = 7.5;
            BodyHalfWidthPaperMm = 2.5;
            BodyHalfHeightPaperMm = 2.5;
            MinimumOriginClearancePaperMm = 0.5;
            EndpointToleranceModelMm = 2.0;
            RequireWholeLeaderInsideRoom = true;
            RequireWholeBodyInsideRoom = true;
        }

        /// <summary>
        /// Горизонтальный участок от центра текста до точки излома.
        /// </summary>
        public double HorizontalShoulderPaperMm { get; set; }

        /// <summary>
        /// Горизонтальная и вертикальная проекции диагонального участка.
        /// Равные проекции дают угол 45 градусов.
        /// </summary>
        public double DiagonalProjectionPaperMm { get; set; }

        /// <summary>
        /// Половина ширины рамки марки относительно начала координат семейства.
        /// </summary>
        public double BodyHalfWidthPaperMm { get; set; }

        /// <summary>
        /// Половина высоты рамки марки относительно начала координат семейства.
        /// </summary>
        public double BodyHalfHeightPaperMm { get; set; }

        /// <summary>
        /// Минимальное расстояние от начала координат семейства до границы помещения.
        /// </summary>
        public double MinimumOriginClearancePaperMm { get; set; }

        /// <summary>
        /// Допуск объединения концов линий замкнутого контура в миллиметрах модели.
        /// </summary>
        public double EndpointToleranceModelMm { get; set; }

        /// <summary>
        /// Если включено, оба участка встроенной выноски также должны оставаться внутри контура.
        /// </summary>
        public bool RequireWholeLeaderInsideRoom { get; set; }

        /// <summary>
        /// Если включено, вся прямоугольная рамка текста должна находиться внутри помещения.
        /// </summary>
        public bool RequireWholeBodyInsideRoom { get; set; }
    }

    public class PlanCornerMarkLayoutItem
    {
        public int CornerNumber { get; set; }

        public XYZ CornerPoint { get; set; }

        public XYZ FamilyOriginPoint { get; set; }

        public XYZ LeaderElbowPoint { get; set; }

        public XYZ LeaderTipPoint { get; set; }

        public PlanCornerMarkOrientation Orientation { get; set; }

        public double OriginClearanceFeet { get; set; }

        /// <summary>
        /// Резервное размещение используется, когда номинальная геометрия семейства
        /// физически не помещается в контур. Марка при этом не пропускается.
        /// </summary>
        public bool IsFallback { get; set; }

        public string FallbackReason { get; set; }

        public double TipDeviationFeet { get; set; }
    }

    public class PlanCornerMarkLayoutFailure
    {
        public int CornerNumber { get; set; }

        public XYZ CornerPoint { get; set; }

        public string Reason { get; set; }
    }

    public class PlanCornerMarkLayoutResult
    {
        public PlanCornerMarkLayoutResult()
        {
            Placements = new List<PlanCornerMarkLayoutItem>();
            Failures = new List<PlanCornerMarkLayoutFailure>();
        }

        public List<PlanCornerMarkLayoutItem> Placements { get; private set; }

        public List<PlanCornerMarkLayoutFailure> Failures { get; private set; }

        public int ClosedContourCount { get; set; }

        public bool IsComplete
        {
            get { return Placements.Count > 0 && Failures.Count == 0; }
        }
    }
}
