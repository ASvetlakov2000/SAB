using Autodesk.Revit.DB;

namespace SAB.GklFrame.Services
{
    public interface IFrameParameterWriter
    {
        void WriteLength(Mullion mullion, double lengthInternal);
    }

    public sealed class FrameParameterWriter : IFrameParameterWriter
    {
        public void WriteLength(Mullion mullion, double lengthInternal)
        {
            if (mullion == null)
            {
                return;
            }

            Parameter parameter = mullion.LookupParameter("SAB_ProfileLength");
            if (parameter != null && !parameter.IsReadOnly && parameter.StorageType == StorageType.Double)
            {
                parameter.Set(lengthInternal);
            }
        }
    }
}
