namespace SAB.CreateViewsAndSheets.Models
{
    public class PlacementSettings
    {
        public PlacementSettings()
        {
            CoordinateUnits = "мм";
            ViewCenterXmm = 150.0;
            ViewCenterYmm = 200.0;
            ViewTitleXmm = 120.0;
            ViewTitleYmm = 130.0;
            TitleLineLengthMm = 80.0;
            UsePointSelectionForViewCenter = false;
            UsePointSelectionForViewTitle = false;
            SaveSettings = true;
        }

        public string CoordinateUnits { get; set; }

        public double ViewCenterXmm { get; set; }

        public double ViewCenterYmm { get; set; }

        public double ViewTitleXmm { get; set; }

        public double ViewTitleYmm { get; set; }

        public double TitleLineLengthMm { get; set; }

        public bool UsePointSelectionForViewCenter { get; set; }

        public bool UsePointSelectionForViewTitle { get; set; }

        public bool SaveSettings { get; set; }
    }
}
