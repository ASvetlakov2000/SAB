using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using RevitLibraryBuilder.Models;

namespace RevitLibraryBuilder.Services.Placement
{
    public class HostedDoorWindowPlacementService
    {
        private const string HostWallTypeName = "Стена условная_Основа";

        // Настраиваемая толщина условной стены (мм)
        private const double HostWallThicknessMm = 300.0;
        // Настраиваемая длина условной стены (мм)
        private const double HostWallLengthMm = 50000.0;
        // Настраиваемая высота условной стены (мм)
        private const double HostWallHeightMm = 3000.0;
        // Настраиваемый отступ первого размещения от начала стены (мм)
        private const double StartOffsetMm = 1000.0;
        // Настраиваемый шаг между размещаемыми экземплярами (мм)
        private const double PlacementStepMm = 2000.0;
        // Настраиваемая высота вставки окон относительно уровня (мм)
        private const double WindowSillHeightMm = 1000.0;

        private readonly Document _document;

        public HostedDoorWindowPlacementService(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
        }

        public int PlaceHosted(List<ElementTypeCsvModel> rows, string categoryName)
        {
            if (rows == null || rows.Count == 0)
            {
                return 0;
            }

            bool isDoorCategory = IsDoorCategory(categoryName);
            bool isWindowCategory = IsWindowCategory(categoryName);

            if (!isDoorCategory && !isWindowCategory)
            {
                return 0;
            }

            Level level = ResolvePlacementLevel();

            if (level == null)
            {
                throw new InvalidOperationException("Не найден подходящий уровень для размещения дверей/окон.");
            }

            using (Transaction transaction = new Transaction(_document, "Hosted Doors/Windows Placement"))
            {
                transaction.Start();

                // Block responsible for creating or reusing wall type "Стена условная_Основа"
                WallType hostWallType = GetOrCreateHostWallType();

                if (hostWallType == null)
                {
                    transaction.RollBack();
                    throw new InvalidOperationException("Не удалось получить или создать тип стены 'Стена условная_Основа'.");
                }

                // Block responsible for creating the host wall instance
                Wall hostWall = CreateHostWall(hostWallType, level);

                if (hostWall == null)
                {
                    transaction.RollBack();
                    throw new InvalidOperationException("Не удалось создать экземпляр host-стены.");
                }

                // Block responsible for hosted placement of Doors/Windows into the generated wall
                int placedCount = PlaceHostedInstances(rows, hostWall, level, isWindowCategory);

                transaction.Commit();
                return placedCount;
            }
        }

        private WallType GetOrCreateHostWallType()
        {
            WallType existing = FindWallTypeByName(HostWallTypeName);

            if (existing != null)
            {
                return existing;
            }

            WallType baseWallType = GetFirstBasicWallType();

            if (baseWallType == null)
            {
                return null;
            }

            WallType newWallType = baseWallType.Duplicate(HostWallTypeName) as WallType;

            if (newWallType == null)
            {
                return null;
            }

            CompoundStructure structure = newWallType.GetCompoundStructure();

            if (structure == null)
            {
                return newWallType;
            }

            ElementId materialId = ResolveDefaultMaterialId();
            double thickness = UnitUtils.ConvertToInternalUnits(HostWallThicknessMm, UnitTypeId.Millimeters);
            IList<CompoundStructureLayer> layers = new List<CompoundStructureLayer>();
            layers.Add(new CompoundStructureLayer(thickness, MaterialFunctionAssignment.Structure, materialId));
            structure.SetLayers(layers);
            newWallType.SetCompoundStructure(structure);

            return newWallType;
        }

        private Wall CreateHostWall(WallType wallType, Level level)
        {
            if (wallType == null || level == null)
            {
                return null;
            }

            double length = UnitUtils.ConvertToInternalUnits(HostWallLengthMm, UnitTypeId.Millimeters);
            double height = UnitUtils.ConvertToInternalUnits(HostWallHeightMm, UnitTypeId.Millimeters);
            XYZ start = new XYZ(0, 0, level.Elevation);
            XYZ end = new XYZ(length, 0, level.Elevation);
            Line line = Line.CreateBound(start, end);
            return Wall.Create(_document, line, wallType.Id, level.Id, height, 0, false, false);
        }

        private int PlaceHostedInstances(List<ElementTypeCsvModel> rows, Wall hostWall, Level level, bool isWindowCategory)
        {
            int placedCount = 0;
            double currentOffset = UnitUtils.ConvertToInternalUnits(StartOffsetMm, UnitTypeId.Millimeters);
            double step = UnitUtils.ConvertToInternalUnits(PlacementStepMm, UnitTypeId.Millimeters);
            double wallLength = UnitUtils.ConvertToInternalUnits(HostWallLengthMm, UnitTypeId.Millimeters);
            double windowHeight = UnitUtils.ConvertToInternalUnits(WindowSillHeightMm, UnitTypeId.Millimeters);

            for (int i = 0; i < rows.Count; i++)
            {
                ElementTypeCsvModel row = rows[i];

                if (row == null || !row.Include)
                {
                    continue;
                }

                FamilySymbol symbol = FindFamilySymbol(row.Family, row.TypeName);

                if (symbol == null)
                {
                    continue;
                }

                if (!symbol.IsActive)
                {
                    symbol.Activate();
                    _document.Regenerate();
                }

                if (currentOffset >= wallLength)
                {
                    break;
                }

                double z = level.Elevation;

                if (isWindowCategory)
                {
                    z += windowHeight;
                }

                XYZ point = new XYZ(currentOffset, 0, z);

                try
                {
                    _document.Create.NewFamilyInstance(
                        point,
                        symbol,
                        hostWall,
                        level,
                        StructuralType.NonStructural);

                    placedCount++;
                    currentOffset += step;
                }
                catch
                {
                    // Пропускаем проблемный экземпляр, продолжаем остальные строки.
                }
            }

            return placedCount;
        }

        private WallType FindWallTypeByName(string wallTypeName)
        {
            if (string.IsNullOrWhiteSpace(wallTypeName))
            {
                return null;
            }

            FilteredElementCollector collector = new FilteredElementCollector(_document);
            collector.OfClass(typeof(WallType));

            foreach (Element element in collector)
            {
                WallType wallType = element as WallType;

                if (wallType == null)
                {
                    continue;
                }

                if (string.Equals(wallType.Name, wallTypeName, StringComparison.Ordinal))
                {
                    return wallType;
                }
            }

            return null;
        }

        private WallType GetFirstBasicWallType()
        {
            FilteredElementCollector collector = new FilteredElementCollector(_document);
            collector.OfClass(typeof(WallType));

            foreach (Element element in collector)
            {
                WallType wallType = element as WallType;

                if (wallType == null)
                {
                    continue;
                }

                if (wallType.Kind == WallKind.Basic)
                {
                    return wallType;
                }
            }

            return null;
        }

        private ElementId ResolveDefaultMaterialId()
        {
            FilteredElementCollector collector = new FilteredElementCollector(_document);
            collector.OfClass(typeof(Material));

            foreach (Element element in collector)
            {
                Material material = element as Material;

                if (material != null)
                {
                    return material.Id;
                }
            }

            return ElementId.InvalidElementId;
        }

        private Level ResolvePlacementLevel()
        {
            List<Level> levels = GetLevelsSortedByElevation();

            for (int i = 0; i < levels.Count; i++)
            {
                Level level = levels[i];

                if (level == null || string.IsNullOrWhiteSpace(level.Name))
                {
                    continue;
                }

                string name = level.Name.Trim().ToUpperInvariant();

                if (name == "ПЕРВЫЙ ЭТАЖ" || name == "1 ЭТАЖ")
                {
                    return level;
                }
            }

            if (levels.Count > 0)
            {
                return levels[0];
            }

            return null;
        }

        private List<Level> GetLevelsSortedByElevation()
        {
            List<Level> levels = new List<Level>();
            FilteredElementCollector collector = new FilteredElementCollector(_document);
            collector.OfClass(typeof(Level));

            foreach (Element element in collector)
            {
                Level level = element as Level;

                if (level != null)
                {
                    levels.Add(level);
                }
            }

            levels.Sort(delegate (Level left, Level right)
            {
                if (left == null && right == null) return 0;
                if (left == null) return 1;
                if (right == null) return -1;
                return left.Elevation.CompareTo(right.Elevation);
            });

            return levels;
        }

        private FamilySymbol FindFamilySymbol(string familyName, string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            FilteredElementCollector collector = new FilteredElementCollector(_document);
            collector.OfClass(typeof(FamilySymbol));

            FamilySymbol fallback = null;

            foreach (Element element in collector)
            {
                FamilySymbol symbol = element as FamilySymbol;

                if (symbol == null)
                {
                    continue;
                }

                if (!string.Equals(symbol.Name, typeName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(familyName))
                {
                    return symbol;
                }

                if (symbol.Family != null &&
                    string.Equals(symbol.Family.Name, familyName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return symbol;
                }

                if (fallback == null)
                {
                    fallback = symbol;
                }
            }

            return fallback;
        }

        public static bool IsDoorOrWindowCategory(string categoryName)
        {
            return IsDoorCategory(categoryName) || IsWindowCategory(categoryName);
        }

        private static bool IsDoorCategory(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return false;
            }

            string normalized = categoryName.Trim().ToUpperInvariant();
            return normalized == "DOORS" || normalized == "ДВЕРИ";
        }

        private static bool IsWindowCategory(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return false;
            }

            string normalized = categoryName.Trim().ToUpperInvariant();
            return normalized == "WINDOWS" || normalized == "ОКНА";
        }
    }
}
