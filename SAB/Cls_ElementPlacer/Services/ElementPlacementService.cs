using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitLibraryBuilder.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitLibraryBuilder.Services
{
    public class ElementPlacementService
    {
        private readonly Document _doc;
        private const double Interval = 5.0; // интервал между элементами

        public ElementPlacementService(Document doc)
        {
            _doc = doc;
        }

        public void PlaceElements(List<ElementTypeCsvModel> elements, Level targetLevel)
        {
            if (elements == null || elements.Count == 0)
                throw new ArgumentException("Список элементов пуст.");

            using (Transaction trans = new Transaction(_doc, "Place Elements"))
            {
                trans.Start();

                var includedElements = elements.Where(e => e.Include).ToList();
                int placedCount = 0;
                List<string> skippedElements = new List<string>();

                XYZ currentPoint = new XYZ(0, 0, 0);

                foreach (var elemCsv in includedElements)
                {
                    try
                    {
                        FamilySymbol familySymbol = FindFamilySymbol(elemCsv.Family, elemCsv.TypeName);

                        if (familySymbol == null)
                        {
                            skippedElements.Add($"{elemCsv.TypeName} (символ не найден)");
                            continue;
                        }

                        if (!familySymbol.IsActive)
                            familySymbol.Activate();

                        _doc.Create.NewFamilyInstance(
                            currentPoint,
                            familySymbol,
                            targetLevel,
                            Autodesk.Revit.DB.Structure.StructuralType.NonStructural
                        );

                        placedCount++;
                        currentPoint = new XYZ(currentPoint.X + Interval, currentPoint.Y, currentPoint.Z);
                    }
                    catch (Exception ex)
                    {
                        skippedElements.Add($"{elemCsv.TypeName} (ошибка: {ex.Message})");
                    }
                }

                trans.Commit();

                string logMessage = $"Элементов размещено: {placedCount}";
                if (skippedElements.Count > 0)
                    logMessage += $"\nПропущено: {skippedElements.Count}\n{string.Join("\n", skippedElements)}";

                TaskDialog.Show("Результаты расстановки", logMessage);
            }
        }

        private FamilySymbol FindFamilySymbol(string familyName, string typeName)
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(fs =>
                    fs.Family.Name.Equals(familyName, StringComparison.InvariantCultureIgnoreCase) &&
                    fs.Name.Equals(typeName, StringComparison.InvariantCultureIgnoreCase));
        }
    }
}