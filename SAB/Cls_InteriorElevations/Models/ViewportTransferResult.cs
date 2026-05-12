using Autodesk.Revit.DB;

namespace SAB.InteriorElevations.Models
{
    public class ViewportTransferResult
    {
        public int SelectedViewportCount { get; set; }

        public int SelectedSheetMarkCount { get; set; }

        public int MovedViewportCount { get; set; }

        public int CopiedViewportCount { get; set; }

        public int MovedSheetMarkCount { get; set; }

        public int FailedViewportCount { get; set; }

        public int FailedSheetMarkCount { get; set; }

        public int SelectedCount { get; set; }

        public int MovedCount { get; set; }

        public int FailedCount { get; set; }

        public ViewSheet SourceSheet { get; set; }

        public ViewSheet TargetSheet { get; set; }
    }
}
