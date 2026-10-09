using Autodesk.Revit.DB;

namespace SAB.InteriorElevations.Services.Marks
{
    public static class CornerMarkConstants
    {
        public const string RoomNumberParameterName = "Номер помещения";

        public const string CornerNumberParameterName = "Номер угла";

        public const string ProjectSpecificCornerMarkFamilyName = "SAB_Марка угла_Развертки";

        public const string PlanCornerMarkFamilyName = "SAB_Марка угла_План";

        public const string PlanCornerVisibilityUpperLeftParameterName = "S1";

        public const string PlanCornerVisibilityUpperRightParameterName = "S2";

        public const string PlanCornerVisibilityLowerLeftParameterName = "S3";

        public const string PlanCornerVisibilityLowerRightParameterName = "S4";

        public const string LeftCornerMarkTypeName = "Left";

        public const string RightCornerMarkTypeName = "Right";

        public static bool IsAnnotationSymbol(FamilySymbol symbol)
        {
            if (symbol == null || symbol.Category == null)
            {
                return false;
            }

            return symbol.Category.Id.IntegerValue == (int)BuiltInCategory.OST_GenericAnnotation;
        }

        public static bool IsAnnotationInstance(FamilyInstance familyInstance)
        {
            if (familyInstance == null)
            {
                return false;
            }

            if (familyInstance.Category != null &&
                familyInstance.Category.Id.IntegerValue == (int)BuiltInCategory.OST_GenericAnnotation)
            {
                return true;
            }

            return IsAnnotationSymbol(familyInstance.Symbol);
        }

        public static string GetAnnotationCategoryNameForMessage()
        {
            return "Аннотационные обозначения";
        }
    }
}
