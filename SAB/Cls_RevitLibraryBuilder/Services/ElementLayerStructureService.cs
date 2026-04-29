using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace RevitLibraryBuilder.Services
{
    /// <summary>
    /// Сервис формирования структуры слоев системных типов.
    /// </summary>
    public class ElementLayerStructureService
    {
        /// <summary>
        /// Возвращает текст структуры в многострочном виде:
        /// 1. Материал - Толщина
        /// 2. ...
        /// </summary>
        public string GetLayerStructureText(ElementType elementType, Document document)
        {
            if (elementType == null || document == null)
            {
                return string.Empty;
            }

            CompoundStructure compoundStructure;

            if (!TryGetCompoundStructure(elementType, out compoundStructure) || compoundStructure == null)
            {
                return string.Empty;
            }

            IList<CompoundStructureLayer> layers = compoundStructure.GetLayers();

            if (layers == null || layers.Count == 0)
            {
                return string.Empty;
            }

            List<string> lines = new List<string>();

            for (int i = 0; i < layers.Count; i++)
            {
                CompoundStructureLayer layer = layers[i];

                if (layer == null)
                {
                    continue;
                }

                string materialName = ResolveMaterialName(document, layer.MaterialId);
                string thicknessText = FormatLayerThickness(layer.Width);

                lines.Add((i + 1).ToString(CultureInfo.InvariantCulture) + ". " + materialName + " - " + thicknessText);
            }

            return string.Join(Environment.NewLine, lines);
        }

        // Блок определения категорий, у которых есть структура слоев.
        private static bool TryGetCompoundStructure(ElementType elementType, out CompoundStructure compoundStructure)
        {
            compoundStructure = null;

            WallType wallType = elementType as WallType;

            if (wallType != null)
            {
                compoundStructure = wallType.GetCompoundStructure();
                return true;
            }

            FloorType floorType = elementType as FloorType;

            if (floorType != null)
            {
                compoundStructure = floorType.GetCompoundStructure();
                return true;
            }

            CeilingType ceilingType = elementType as CeilingType;

            if (ceilingType != null)
            {
                compoundStructure = ceilingType.GetCompoundStructure();
                return true;
            }

            RoofType roofType = elementType as RoofType;

            if (roofType != null)
            {
                compoundStructure = roofType.GetCompoundStructure();
                return true;
            }

            return false;
        }

        private static string ResolveMaterialName(Document document, ElementId materialId)
        {
            if (materialId == null || materialId == ElementId.InvalidElementId)
            {
                return "Нет материала";
            }

            Material material = document.GetElement(materialId) as Material;

            if (material == null || string.IsNullOrWhiteSpace(material.Name))
            {
                return "Нет материала";
            }

            return material.Name;
        }

        private static string FormatLayerThickness(double internalWidth)
        {
            if (internalWidth <= 0)
            {
                return "0";
            }

            double mm = UnitUtils.ConvertFromInternalUnits(internalWidth, UnitTypeId.Millimeters);
            double rounded = Math.Round(mm, 2, MidpointRounding.AwayFromZero);

            if (Math.Abs(rounded - Math.Round(rounded)) < 0.0001)
            {
                return Math.Round(rounded).ToString(CultureInfo.InvariantCulture);
            }

            return rounded.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
