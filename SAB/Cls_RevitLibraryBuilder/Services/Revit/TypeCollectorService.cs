using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace RevitLibraryBuilder.Services.Revit
{
    public class TypeCollectorService
    {
        public List<ElementType> CollectAllTypes(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ElementType))
                .Cast<ElementType>()
                .Where(x => x.Category != null)
                .ToList();
        }
    }
}