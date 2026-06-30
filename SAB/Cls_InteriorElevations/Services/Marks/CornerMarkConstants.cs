using Autodesk.Revit.DB;

namespace SAB.InteriorElevations.Services.Marks
{
    public static class CornerMarkConstants
    {
        public const string RoomNumberParameterName = "Номер помещения";

        public const string CornerNumberParameterName = "Номер угла";

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
