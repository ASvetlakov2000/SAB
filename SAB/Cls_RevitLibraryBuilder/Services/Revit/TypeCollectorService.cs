using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace RevitLibraryBuilder.Services.Revit
{
    public class TypeCollectorService
    {
        public List<ElementType> CollectAllTypes(Document doc)
        {
            List<ElementType> types = new List<ElementType>();

            if (doc == null)
            {
                return types;
            }

            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector.OfClass(typeof(ElementType));

            foreach (Element element in collector)
            {
                ElementType type = element as ElementType;

                if (type == null)
                {
                    continue;
                }

                if (type.Category == null)
                {
                    continue;
                }

                types.Add(type);
            }

            return types;
        }
    }
}
