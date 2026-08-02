using System;
using System.Collections.Generic;
using System.Text;
using Autodesk.Revit.DB;
using SAB.InteriorElevations.Models;
using SAB.InteriorElevations.Utils;

namespace SAB.InteriorElevations.Services.Elevations
{
    public class ElevationNamingService
    {
        private readonly HashSet<string> _usedViewNames;
        private readonly HashSet<string> _usedSheetNames;
        private readonly HashSet<string> _usedSheetNumbers;

        public ElevationNamingService(Document document)
        {
            _usedViewNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _usedSheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _usedSheetNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            CollectExistingNames(document);
        }

        public string GenerateUniqueElevationViewName(
            RoomData roomData,
            int startPointNumber,
            int endPointNumber,
            ElevationSettings settings)
        {
            string part1 = settings != null ? settings.ElevationNamePart1 : string.Empty;
            string part2 = settings != null ? settings.ElevationNamePart2 : string.Empty;
            string part3 = settings != null ? settings.ElevationNamePart3 : string.Empty;
            string baseName = BuildFormulaName(
                part1,
                part2,
                part3,
                roomData,
                null,
                startPointNumber,
                endPointNumber);

            baseName = RevitNameUtils.SanitizeName(baseName, "ELV_rБез номера_Без имени_Elev_1-2");
            return GetUniqueName(baseName, _usedViewNames, "_", 2);
        }

        public string GenerateUniqueSheetName(IList<RoomData> roomDataList, ElevationSettings settings)
        {
            RoomData firstRoom = roomDataList != null && roomDataList.Count > 0 ? roomDataList[0] : null;
            string part1 = settings != null ? settings.SheetNamePart1 : string.Empty;
            string part2 = settings != null ? settings.SheetNamePart2 : string.Empty;
            string part3 = settings != null ? settings.SheetNamePart3 : string.Empty;
            string baseName = BuildFormulaName(part1, part2, part3, firstRoom, roomDataList, 0, 0);
            baseName = RevitNameUtils.SanitizeName(baseName, "Развертки стен пом. №Без номера Без имени");

            return GetUniqueName(baseName, _usedSheetNames, "_", 2);
        }

        public string GenerateUniqueSheetNumber(IList<RoomData> roomDataList)
        {
            StringBuilder numberBuilder = new StringBuilder("ELV");
            if (roomDataList != null)
            {
                for (int index = 0; index < roomDataList.Count; index++)
                {
                    RoomData roomData = roomDataList[index];
                    string roomNumber = roomData != null ? roomData.RoomNumber : "000";
                    numberBuilder.Append("-");
                    numberBuilder.Append(string.IsNullOrWhiteSpace(roomNumber) ? "000" : roomNumber.Trim());
                }
            }

            string baseNumber = RevitNameUtils.SanitizeName(numberBuilder.ToString(), "ELV-000");
            return GetUniqueName(baseNumber, _usedSheetNumbers, "-", 2);
        }

        private string BuildFormulaName(
            string part1,
            string part2,
            string part3,
            RoomData roomData,
            IList<RoomData> roomDataList,
            int startPointNumber,
            int endPointNumber)
        {
            StringBuilder builder = new StringBuilder();
            AppendResolvedPart(builder, part1, roomData, roomDataList, startPointNumber, endPointNumber);
            AppendResolvedPart(builder, part2, roomData, roomDataList, startPointNumber, endPointNumber);
            AppendResolvedPart(builder, part3, roomData, roomDataList, startPointNumber, endPointNumber);
            return builder.ToString().Trim();
        }

        private void AppendResolvedPart(
            StringBuilder builder,
            string template,
            RoomData roomData,
            IList<RoomData> roomDataList,
            int startPointNumber,
            int endPointNumber)
        {
            if (builder == null || string.IsNullOrEmpty(template))
            {
                return;
            }

            string roomNumber = roomData != null && !string.IsNullOrWhiteSpace(roomData.RoomNumber)
                ? roomData.RoomNumber
                : "Без номера";
            string roomName = roomData != null && !string.IsNullOrWhiteSpace(roomData.RoomName)
                ? roomData.RoomName
                : "Без имени";

            string value = template;
            if (string.Equals(value.Trim(), "Номер помещения", StringComparison.OrdinalIgnoreCase))
            {
                value = roomNumber;
            }
            else if (string.Equals(value.Trim(), "Имя помещения", StringComparison.OrdinalIgnoreCase))
            {
                value = roomName;
            }
            else if (string.Equals(value.Trim(), "Помещения", StringComparison.OrdinalIgnoreCase))
            {
                value = BuildRoomList(roomDataList, roomData);
            }
            else if (string.Equals(value.Trim(), "Начальный угол", StringComparison.OrdinalIgnoreCase))
            {
                value = startPointNumber.ToString();
            }
            else if (string.Equals(value.Trim(), "Конечный угол", StringComparison.OrdinalIgnoreCase))
            {
                value = endPointNumber.ToString();
            }
            else
            {
                value = value.Replace("{Номер помещения}", roomNumber);
                value = value.Replace("{Имя помещения}", roomName);
                value = value.Replace("{Начальный угол}", startPointNumber.ToString());
                value = value.Replace("{Конечный угол}", endPointNumber.ToString());
                value = value.Replace("{Помещения}", BuildRoomList(roomDataList, roomData));
            }

            builder.Append(value);
        }

        private string BuildRoomList(IList<RoomData> roomDataList, RoomData fallbackRoom)
        {
            IList<RoomData> rooms = roomDataList;
            if (rooms == null || rooms.Count == 0)
            {
                rooms = new List<RoomData> { fallbackRoom };
            }

            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < rooms.Count; index++)
            {
                RoomData room = rooms[index];
                string roomNumber = room != null && !string.IsNullOrWhiteSpace(room.RoomNumber)
                    ? room.RoomNumber.Trim()
                    : "Без номера";
                string roomName = room != null && !string.IsNullOrWhiteSpace(room.RoomName)
                    ? room.RoomName.Trim()
                    : "Без имени";

                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                builder.Append("№");
                builder.Append(roomNumber);
                builder.Append(" ");
                builder.Append(roomName);
            }

            return builder.ToString();
        }

        private string GetUniqueName(string baseValue, HashSet<string> nameStorage, string suffixSeparator, int suffixDigits)
        {
            if (!nameStorage.Contains(baseValue))
            {
                nameStorage.Add(baseValue);
                return baseValue;
            }

            int suffixIndex = 1;
            while (true)
            {
                string suffix = suffixIndex.ToString(new string('0', suffixDigits));
                string candidate = baseValue + suffixSeparator + suffix;

                if (!nameStorage.Contains(candidate))
                {
                    nameStorage.Add(candidate);
                    return candidate;
                }

                suffixIndex++;
            }
        }

        private void CollectExistingNames(Document document)
        {
            if (document == null)
            {
                return;
            }

            FilteredElementCollector viewCollector = new FilteredElementCollector(document).OfClass(typeof(View));
            foreach (Element element in viewCollector)
            {
                View view = element as View;
                if (view == null || view.IsTemplate)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(view.Name))
                {
                    _usedViewNames.Add(view.Name);
                }
            }

            FilteredElementCollector sheetCollector = new FilteredElementCollector(document).OfClass(typeof(ViewSheet));
            foreach (Element element in sheetCollector)
            {
                ViewSheet sheet = element as ViewSheet;
                if (sheet == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(sheet.Name))
                {
                    _usedSheetNames.Add(sheet.Name);
                }

                if (!string.IsNullOrWhiteSpace(sheet.SheetNumber))
                {
                    _usedSheetNumbers.Add(sheet.SheetNumber);
                }
            }
        }
    }
}
