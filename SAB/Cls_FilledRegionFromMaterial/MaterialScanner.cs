using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace SAB.FilledRegionFromMaterial
{
    internal static class MaterialScanner
    {
        public static MaterialScanResult Scan(
            Document document,
            bool includeUnusedMaterials,
            bool includePaintedMaterials)
        {
            if (includeUnusedMaterials)
            {
                IList<Material> allMaterials = new FilteredElementCollector(document)
                    .OfClass(typeof(Material))
                    .Cast<Material>()
                    .OrderBy(material => material.Name, System.StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                return new MaterialScanResult(allMaterials, 0, 0);
            }

            var materialIds = new HashSet<int>();
            int scannedElementCount = 0;
            int scanErrorCount = 0;

            foreach (Element element in new FilteredElementCollector(document)
                         .WhereElementIsNotElementType()
                         .ToElements())
            {
                scannedElementCount++;

                try
                {
                    AddMaterialIds(materialIds, element.GetMaterialIds(false));
                    if (includePaintedMaterials)
                    {
                        AddMaterialIds(materialIds, element.GetMaterialIds(true));
                    }
                }
                catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                {
                    scanErrorCount++;
                }
                catch (Autodesk.Revit.Exceptions.ArgumentException)
                {
                    scanErrorCount++;
                }
            }

            List<Material> materials = materialIds
                .Select(id => document.GetElement(new ElementId(id)) as Material)
                .Where(material => material != null)
                .OrderBy(material => material.Name, System.StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            return new MaterialScanResult(materials, scannedElementCount, scanErrorCount);
        }

        private static void AddMaterialIds(ISet<int> target, IEnumerable<ElementId> source)
        {
            foreach (ElementId id in source)
            {
                if (id != null && id != ElementId.InvalidElementId)
                {
                    target.Add(id.IntegerValue);
                }
            }
        }
    }

    internal sealed class MaterialScanResult
    {
        public MaterialScanResult(IList<Material> materials, int scannedElementCount, int scanErrorCount)
        {
            Materials = materials;
            ScannedElementCount = scannedElementCount;
            ScanErrorCount = scanErrorCount;
        }

        public IList<Material> Materials { get; private set; }
        public int ScannedElementCount { get; private set; }
        public int ScanErrorCount { get; private set; }
    }
}
