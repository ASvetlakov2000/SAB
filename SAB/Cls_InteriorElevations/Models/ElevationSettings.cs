namespace InteriorElevations.Models
{
    public class ElevationSettings
    {
        public double CropOffsetTop { get; set; } = 2500;       // мм сверху от уровня
        public double CropOffsetBottom { get; set; } = 0;       // мм от уровня
        public double CropOffsetSide { get; set; } = 100;       // мм по бокам линии
        public double CropOffsetLine { get; set; } = 150;       // мм смещение от линии
        public int DefaultViewScale { get; set; } = 100;        // Масштаб вида
        public string ViewNameFormat { get; set; } = "Развертка пом {0}_{1}-{2}";
    }
}