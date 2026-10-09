namespace SAB.MaterialQuantity
{
    internal sealed class MaterialQuantityProgressInfo
    {
        internal int OverallPercentage { get; set; }

        internal bool IsIndeterminate { get; set; }

        internal int CurrentItem { get; set; }

        internal int TotalItems { get; set; }

        internal string Stage { get; set; }

        internal string Details { get; set; }

        internal int Created { get; set; }

        internal int Updated { get; set; }

        internal int NeedsReview { get; set; }

        internal int Obsolete { get; set; }
    }
}
