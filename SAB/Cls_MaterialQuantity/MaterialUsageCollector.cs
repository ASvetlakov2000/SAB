using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace SAB.MaterialQuantity
{
    internal static class MaterialUsageCollector
    {
        internal static readonly BuiltInCategory[] HostCategories =
        {
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_Floors,
            BuiltInCategory.OST_Ceilings,
            BuiltInCategory.OST_Roofs
        };

        internal static HashSet<long> CollectUsedMaterialIds(Document project)
        {
            var result = new HashSet<long>();
            ICollection<Element> hosts = new FilteredElementCollector(project)
                .WherePasses(new ElementMulticategoryFilter(HostCategories.ToList()))
                .WhereElementIsNotElementType()
                .ToElements();

            foreach (Element host in hosts)
            {
                var hostType = project.GetElement(host.GetTypeId()) as HostObjAttributes;
                CompoundStructure compound = hostType == null ? null : hostType.GetCompoundStructure();
                if (compound == null) continue;

                foreach (CompoundStructureLayer layer in compound.GetLayers())
                {
                    if (layer.MaterialId != ElementId.InvalidElementId)
                        result.Add(RevitElementId.Key(layer.MaterialId));
                }
            }

            return result;
        }
    }
}
