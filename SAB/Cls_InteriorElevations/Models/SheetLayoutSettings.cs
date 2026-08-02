namespace SAB.InteriorElevations.Models
{
    public enum ViewTitleAnchor
    {
        BottomLeft = 0,
        BottomCenter = 1,
        BottomRight = 2,
        TopCenter = 3
    }

    public class SheetLayoutSettings
    {
        public int ColumnsCount { get; set; }

        public double StartXmm { get; set; }

        public double StartYmm { get; set; }

        public double StepXmm { get; set; }

        public double StepYmm { get; set; }

        public ViewTitleAnchor ViewTitleAnchor { get; set; }

        public double ViewTitleOffsetXmm { get; set; }

        public double ViewTitleOffsetYmm { get; set; }
    }
}
