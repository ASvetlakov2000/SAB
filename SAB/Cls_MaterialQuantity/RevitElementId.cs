using Autodesk.Revit.DB;

namespace SAB.MaterialQuantity
{
    internal static class RevitElementId
    {
        internal static long Key(ElementId id)
        {
#if REVIT2024
            return id.Value;
#else
            return id.IntegerValue;
#endif
        }
    }
}
