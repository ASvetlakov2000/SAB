using Autodesk.Revit.DB;

namespace SAB.ParameterTools
{
    internal static class Ids
    {
        internal static long Value(ElementId id)
        {
#if REVIT2024
            return id.Value;
#else
            return id.IntegerValue;
#endif
        }
        internal static int Category(ElementId id) { return checked((int)Value(id)); }
        internal static ElementId Create(long id)
        {
#if REVIT2024
            return new ElementId(id);
#else
            return new ElementId(checked((int)id));
#endif
        }
    }
}
