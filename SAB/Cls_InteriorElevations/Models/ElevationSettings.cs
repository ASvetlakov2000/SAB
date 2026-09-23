using Autodesk.Revit.DB;

namespace SAB.InteriorElevations.Models
{
    public class ElevationSettings
    {
        public ElementId ViewTemplateId { get; set; }

        public ElementId ElevationViewFamilyTypeId { get; set; }

        public int ViewScale { get; set; }

        public double TopOffsetMm { get; set; }

        public double BottomOffsetMm { get; set; }

        public double LeftOffsetMm { get; set; }

        public double RightOffsetMm { get; set; }

        public double ViewDepthMm { get; set; }

        public double MarkerOffsetMm { get; set; }

        public string ElevationNamePart1 { get; set; }

        public string ElevationNamePart2 { get; set; }

        public string ElevationNamePart3 { get; set; }

        public string ElevationTitlePart1 { get; set; }

        public string ElevationTitlePart2 { get; set; }

        public string ElevationTitlePart3 { get; set; }

        public bool CreateSheet { get; set; }

        public bool UseExistingSheet { get; set; }

        public ElementId ExistingSheetId { get; set; }

        // План-схема, уже размещенная на выбранном существующем листе.
        // В этом режиме новый вид плана не создается: в выбранный вид добавляются
        // линии и марки углов очередного помещения.
        public ElementId ExistingRoomPlanViewId { get; set; }

        public bool OpenCreatedSheet { get; set; }

        public bool MultipleRoomsOnSheet { get; set; }

        public bool PickRoomFromLink { get; set; }

        public bool EnableRoomObjectCategory { get; set; }

        public ElementId TitleBlockTypeId { get; set; }

        public ElementId ViewportTypeId { get; set; }

        public ElementId PlanCornerMarkTypeId { get; set; }

        public ElementId SheetCornerMarkTypeId { get; set; }

        public bool CornerMarksOnlyCornerNumber { get; set; }

        public bool SheetCornerMarksBelowView { get; set; }

        public int? SheetFormatAValue { get; set; }

        public SheetLayoutSettings SheetLayoutSettings { get; set; }

        public string SheetNamePart1 { get; set; }

        public string SheetNamePart2 { get; set; }

        public string SheetNamePart3 { get; set; }

        // Блок настроек создания план-схемы помещения после построения разверток.
        public bool CreateRoomPlanScheme { get; set; }

        public bool PlaceRoomPlanSchemeOnSheet { get; set; }

        public string RoomPlanNamePart1 { get; set; }

        public string RoomPlanNamePart2 { get; set; }

        public string RoomPlanNamePart3 { get; set; }

        public ElementId RoomPlanViewTemplateId { get; set; }

        public ElementId RoomPlanRoomTagTypeId { get; set; }

        public int RoomPlanViewScale { get; set; }

        public double RoomPlanCropOffsetMm { get; set; }
    }
}
