using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace RevitLibraryBuilder.Services
{
    public class TypeCollectorService
    {
        public List<ElementType> CollectAllTypes(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);

            return collector
                .OfClass(typeof(ElementType))
                .Cast<ElementType>()
                .Where(t => t.Category != null)
                .ToList();
        }
    }
}