namespace SAB.InteriorElevations.Models
{
    public class ElevationCropAdjustmentResult
    {
        public int SelectedCount { get; set; }

        public int UpdatedCount { get; set; }

        public int FailedCount { get; set; }

        public int SelectedMarkCount { get; set; }

        public int UpdatedMarkCount { get; set; }

        public int FailedMarkCount { get; set; }

        public int UpdatedGridCount { get; set; }

        public int FailedGridCount { get; set; }
    }
}
