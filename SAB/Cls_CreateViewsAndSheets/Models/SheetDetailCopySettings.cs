namespace SAB.CreateViewsAndSheets.Models
{
    public class SheetDetailCopySettings
    {
        public SheetDetailCopySettings()
        {
            CopySheetWithDetailing = false;
            CopySchedules = true;
            CopyLegends = true;
            CopyDraftingViews = true;
            CopyDetailLines = true;
            CopyFilledRegions = true;
            CopyTextNotes = true;
            CopyGenericAnnotations = true;
            CopyImages = true;
        }

        public bool CopySheetWithDetailing { get; set; }

        public bool CopySchedules { get; set; }

        public bool CopyLegends { get; set; }

        public bool CopyDraftingViews { get; set; }

        public bool CopyDetailLines { get; set; }

        public bool CopyFilledRegions { get; set; }

        public bool CopyTextNotes { get; set; }

        public bool CopyGenericAnnotations { get; set; }

        public bool CopyImages { get; set; }
    }
}
