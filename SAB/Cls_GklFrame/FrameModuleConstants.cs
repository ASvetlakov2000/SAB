using System;

namespace SAB.GklFrame
{
    internal static class FrameModuleConstants
    {
        public const string ModuleTitle = "Каркас ГКЛ";
        public const string RibbonPanelName = "Каркас";
        public const string GenerateCommandText = "Создать\nкаркас";
        public const string CalculateCommandText = "Пересчитать\nкаркас";
        public const string DefaultCurtainWallTypeName = "SAB_GKL_FRAME_CALC";
        public const double DefaultStudSpacingMm = 600.0;
        public const double DefaultPurchaseLengthMm = 3000.0;
        public const double DefaultMinimumReusableOffcutMm = 500.0;
        public const double GeometryToleranceMm = 5.0;

        // Идентификатор схемы нельзя менять после выпуска: по нему существующие модели
        // находят производные стены и импосты после повторного открытия документа.
        public static readonly Guid MetadataSchemaGuid =
            new Guid("3F7C6BB7-64E6-4A21-A701-9164F04068C5");
    }
}
