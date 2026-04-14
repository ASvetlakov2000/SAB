using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using SAB.BimDashboard.Models;

namespace SAB.BimDashboard.Services.Data
{
    /// <summary>
    /// Провайдер данных из текущей модели Revit.
    /// </summary>
    public class RevitDataProvider : IDataProvider
    {
        public bool CanHandle(DataSourceType sourceType)
        {
            return sourceType == DataSourceType.Revit;
        }

        public ProviderResult Load(DataProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.UiApplication == null)
            {
                throw new InvalidOperationException("UIApplication недоступен.");
            }

            UIDocument uiDocument = context.UiApplication.ActiveUIDocument;

            if (uiDocument == null)
            {
                throw new InvalidOperationException("Активный документ Revit не найден.");
            }

            Document document = uiDocument.Document;

            if (document == null)
            {
                throw new InvalidOperationException("Документ Revit недоступен.");
            }

            if (document.ActiveView == null)
            {
                throw new InvalidOperationException("ActiveView недоступен.");
            }

            ProviderResult result = new ProviderResult();
            result.ProjectName = string.IsNullOrWhiteSpace(document.Title) ? "Revit Project" : document.Title;

            // Блок базовых счетчиков по ключевым категориям MVP.
            List<CategorySetup> categorySetups = new List<CategorySetup>
            {
                new CategorySetup(BuiltInCategory.OST_Walls, "Walls"),
                new CategorySetup(BuiltInCategory.OST_Floors, "Floors"),
                new CategorySetup(BuiltInCategory.OST_Doors, "Doors"),
                new CategorySetup(BuiltInCategory.OST_Windows, "Windows"),
                new CategorySetup(BuiltInCategory.OST_Rooms, "Rooms")
            };

            for (int i = 0; i < categorySetups.Count; i++)
            {
                CategorySetup setup = categorySetups[i];
                int elementCount = CountInstancesByCategory(document, setup.Category);

                UnifiedRecord record = new UnifiedRecord
                {
                    Category = setup.DisplayName,
                    Name = setup.DisplayName,
                    Count = elementCount
                };

                FillStandardFields(record, "CategorySummary", "Revit", document.Title);
                result.Records.Add(record);
            }

            // Блок площадей помещений (ft² -> м²).
            AddRoomAreaRecords(document, result.Records, result.Warnings);

            // Блок длин стен (ft -> м).
            AddWallLengthRecords(document, result.Records, result.Warnings);

            if (result.Records.Count == 0)
            {
                throw new InvalidOperationException("В модели Revit не найдены данные для dashboard.");
            }

            return result;
        }

        private static int CountInstancesByCategory(Document document, BuiltInCategory builtInCategory)
        {
            FilteredElementCollector collector = new FilteredElementCollector(document)
                .OfCategory(builtInCategory)
                .WhereElementIsNotElementType();

            return collector.GetElementCount();
        }

        private static void AddRoomAreaRecords(Document document, List<UnifiedRecord> records, List<string> warnings)
        {
            int roomCountWithArea = 0;

            FilteredElementCollector roomCollector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType();

            IList<Element> rooms = roomCollector.ToElements();

            for (int i = 0; i < rooms.Count; i++)
            {
                Element roomElement = rooms[i];

                if (roomElement == null)
                {
                    continue;
                }

                Parameter areaParameter = roomElement.get_Parameter(BuiltInParameter.ROOM_AREA);

                if (areaParameter == null || areaParameter.StorageType != StorageType.Double)
                {
                    continue;
                }

                double areaInternal = areaParameter.AsDouble();

                if (areaInternal <= 0)
                {
                    continue;
                }

                double areaSquareMeters = UnitUtils.ConvertFromInternalUnits(areaInternal, UnitTypeId.SquareMeters);
                string roomName = string.IsNullOrWhiteSpace(roomElement.Name) ? "Room" : roomElement.Name;

                UnifiedRecord record = new UnifiedRecord
                {
                    Category = "Rooms",
                    Name = roomName,
                    Area = areaSquareMeters
                };

                FillStandardFields(record, "RoomArea", "Revit", document.Title);
                records.Add(record);
                roomCountWithArea++;
            }

            if (roomCountWithArea == 0)
            {
                warnings.Add("В модели не найдено помещений с ненулевой площадью.");
            }
        }

        private static void AddWallLengthRecords(Document document, List<UnifiedRecord> records, List<string> warnings)
        {
            int wallCountWithLength = 0;

            FilteredElementCollector wallCollector = new FilteredElementCollector(document)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType();

            IList<Element> walls = wallCollector.ToElements();

            for (int i = 0; i < walls.Count; i++)
            {
                Element wallElement = walls[i];

                if (wallElement == null)
                {
                    continue;
                }

                Parameter lengthParameter = wallElement.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH);

                if (lengthParameter == null || lengthParameter.StorageType != StorageType.Double)
                {
                    continue;
                }

                double lengthInternal = lengthParameter.AsDouble();

                if (lengthInternal <= 0)
                {
                    continue;
                }

                double lengthMeters = UnitUtils.ConvertFromInternalUnits(lengthInternal, UnitTypeId.Meters);
                string wallName = string.IsNullOrWhiteSpace(wallElement.Name) ? "Wall" : wallElement.Name;

                UnifiedRecord record = new UnifiedRecord
                {
                    Category = "Walls",
                    Name = wallName,
                    Length = lengthMeters
                };

                FillStandardFields(record, "WallLength", "Revit", document.Title);
                records.Add(record);
                wallCountWithLength++;
            }

            if (wallCountWithLength == 0)
            {
                warnings.Add("В модели не найдено стен с измеряемой длиной.");
            }
        }

        private static void FillStandardFields(UnifiedRecord record, string recordType, string sourceType, string sourceFile)
        {
            if (record == null)
            {
                return;
            }

            record.Fields["RecordType"] = recordType;
            record.Fields["SourceType"] = sourceType;
            record.Fields["SourceFile"] = sourceFile ?? string.Empty;
            record.Fields["Category"] = record.Category ?? string.Empty;
            record.Fields["Name"] = record.Name ?? string.Empty;
            record.Fields["Count"] = record.Count.HasValue ? record.Count.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
            record.Fields["Area"] = record.Area.HasValue ? record.Area.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
            record.Fields["Length"] = record.Length.HasValue ? record.Length.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
            record.Fields["Value"] = record.Value.HasValue ? record.Value.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;
        }

        private class CategorySetup
        {
            public CategorySetup(BuiltInCategory category, string displayName)
            {
                Category = category;
                DisplayName = displayName;
            }

            public BuiltInCategory Category { get; private set; }

            public string DisplayName { get; private set; }
        }
    }
}
