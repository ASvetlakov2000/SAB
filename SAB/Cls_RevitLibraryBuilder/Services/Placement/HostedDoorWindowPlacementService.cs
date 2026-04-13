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
        private const double WallThicknessMm = 300;
        private const double WallLengthMm = 50000;
        private const double WallHeightMm = 3000;
        private const double StartOffsetMm = 1000;
        private const double StepOffsetMm = 2000;
        private const double WindowInsertionHeightMm = 1000;

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

            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return 0;
            }

            bool isDoorCategory = IsDoorCategory(categoryName);
            bool isWindowCategory = IsWindowCategory(categoryName);

            if (!isDoorCategory && !isWindowCategory)
            {
                return 0;
            }

            Level level = ResolveHostLevel();

            if (level == null)
            {
                throw new InvalidOperationException("No valid level found for hosted placement.");
            }

            using (Transaction transaction = new Transaction(_document, "Hosted Door/Window Placement"))
            {
                transaction.Start();

                // Block responsible for creating or reusing wall type "Стена условная_Основа"
                WallType hostWallType = GetOrCreateHostWallType();

                if (hostWallType == null)
                {
                    transaction.RollBack();
                    throw new InvalidOperationException("Host wall type could not be created.");
                }

                // Block responsible for creating the host wall instance
                Wall hostWall = CreateHostWallInstance(hostWallType, level);

                if (hostWall == null)
                {
                    transaction.RollBack();
                    throw new InvalidOperationException("Host wall instance could not be created.");
                }

                // Block responsible for hosted placement of Doors/Windows into the generated wall
                int placedCount = PlaceHostedInstances(rows, hostWall, level, isWindowCategory);

                transaction.Commit();
                return placedCount;
            }
        }

        private WallType GetOrCreateHostWallType()
        {
            FilteredElementCollector wallTypeCollector = new FilteredElementCollector(_document);
            wallTypeCollector.OfClass(typeof(WallType));

            foreach (Element element in wallTypeCollector)
            {
                WallType existingType = element as WallType;

                if (existingType == null)
                {
                    continue;
                }

                if (string.Equals(existingType.Name, HostWallTypeName, StringComparison.Ordinal))
                {
                    return existingType;
                }
            }

            WallType baseType = GetBaseBasicWallType();

            if (baseType == null)
            {
                return null;
            }

            ElementType duplicatedType = baseType.Duplicate(HostWallTypeName) as ElementType;
            WallType hostWallType = duplicatedType as WallType;

            if (hostWallType == null)
            {
                return null;
            }

            CompoundStructure compoundStructure = hostWallType.GetCompoundStructure();

            if (compoundStructure == null)
            {
                return null;
            }

            ElementId materialId = ResolveDefaultMaterialId();
            double thickness = UnitUtils.ConvertToInternalUnits(WallThicknessMm, UnitTypeId.Millimeters);
            IList<CompoundStructureLayer> layers = new List<CompoundStructureLayer>();

            layers.Add(new CompoundStructureLayer(thickness, MaterialFunctionAssignment.Structure, materialId));
            compoundStructure.SetLayers(layers);
            hostWallType.SetCompoundStructure(compoundStructure);

            return hostWallType;
        }

        private WallType GetBaseBasicWallType()
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

        private Level ResolveHostLevel()
        {
            List<Level> levels = GetAllLevelsOrderedByElevation();

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

            for (int i = 0; i < levels.Count; i++)
            {
                Level level = levels[i];

                if (level != null)
                {
                    return level;
                }
            }

            return null;
        }

        private List<Level> GetAllLevelsOrderedByElevation()
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
                if (left == null && right == null)
                {
                    return 0;
                }

                if (left == null)
                {
                    return 1;
                }

                if (right == null)
                {
                    return -1;
                }

                return left.Elevation.CompareTo(right.Elevation);
            });

            return levels;
        }

        private Wall CreateHostWallInstance(WallType wallType, Level level)
        {
            if (wallType == null || level == null)
            {
                return null;
            }

            double wallLength = UnitUtils.ConvertToInternalUnits(WallLengthMm, UnitTypeId.Millimeters);
            double wallHeight = UnitUtils.ConvertToInternalUnits(WallHeightMm, UnitTypeId.Millimeters);
            XYZ startPoint = new XYZ(0, 0, level.Elevation);
            XYZ endPoint = new XYZ(wallLength, 0, level.Elevation);
            Line wallLine = Line.CreateBound(startPoint, endPoint);

            Wall wall = Wall.Create(_document, wallLine, wallType.Id, level.Id, wallHeight, 0, false, false);
            return wall;
        }

        private int PlaceHostedInstances(List<ElementTypeCsvModel> rows, Wall hostWall, Level level, bool isWindowCategory)
        {
            int placedCount = 0;
            double wallLength = UnitUtils.ConvertToInternalUnits(WallLengthMm, UnitTypeId.Millimeters);
            double startOffset = UnitUtils.ConvertToInternalUnits(StartOffsetMm, UnitTypeId.Millimeters);
            double stepOffset = UnitUtils.ConvertToInternalUnits(StepOffsetMm, UnitTypeId.Millimeters);
            double insertionHeight = 0;

            if (isWindowCategory)
            {
                insertionHeight = UnitUtils.ConvertToInternalUnits(WindowInsertionHeightMm, UnitTypeId.Millimeters);
            }

            double currentOffset = startOffset;

            for (int i = 0; i < rows.Count; i++)
            {
                ElementTypeCsvModel row = rows[i];

                if (row == null || !row.Include)
                {
                    continue;
                }

                FamilySymbol symbol = FindFamilySymbol(row);

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

                XYZ insertionPoint = new XYZ(currentOffset, 0, level.Elevation + insertionHeight);

                try
                {
                    _document.Create.NewFamilyInstance(
                        insertionPoint,
                        symbol,
                        hostWall,
                        level,
                        StructuralType.NonStructural);

                    placedCount++;
                    currentOffset += stepOffset;
                }
                catch
                {
                    // Skip invalid symbol placement and continue remaining rows.
                }
            }

            return placedCount;
        }

        private FamilySymbol FindFamilySymbol(ElementTypeCsvModel row)
        {
            FilteredElementCollector collector = new FilteredElementCollector(_document);
            collector.OfClass(typeof(FamilySymbol));

            FamilySymbol fallbackByType = null;

            foreach (Element element in collector)
            {
                FamilySymbol symbol = element as FamilySymbol;

                if (symbol == null)
                {
                    continue;
                }

                if (!string.Equals(symbol.Name, row.TypeName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(row.Family))
                {
                    return symbol;
                }

                if (symbol.Family != null &&
                    string.Equals(symbol.Family.Name, row.Family.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return symbol;
                }

                if (fallbackByType == null)
                {
                    fallbackByType = symbol;
                }
            }

            return fallbackByType;
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
