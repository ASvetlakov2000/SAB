using System;
using System.Collections.Generic;
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

        public string GenerateUniqueElevationViewName(RoomData roomData, int index, ElevationSettings settings)
        {
            string roomNumber = roomData != null ? roomData.RoomNumber : "000";
            string roomName = roomData != null ? roomData.RoomName : "Room";
            string indexText = index.ToString("00");

            string prefix = settings != null ? settings.ViewNamePrefix : string.Empty;
            bool useRoomNumber = settings == null || settings.UseRoomNumberInViewName;
            bool useRoomName = settings == null || settings.UseRoomNameInViewName;

            string baseName = BuildViewBaseName(prefix, useRoomNumber ? roomNumber : string.Empty, useRoomName ? roomName : string.Empty, indexText);
            baseName = RevitNameUtils.SanitizeName(baseName, "Interior_Elevation_" + indexText);

            return GetUniqueName(baseName, _usedViewNames, "_", 2);
        }

        public string GenerateUniqueSheetName(RoomData roomData)
        {
            string roomNumber = roomData != null ? roomData.RoomNumber : "000";
            string roomName = roomData != null ? roomData.RoomName : "Room";

            string baseName = RevitNameUtils.BuildJoinedName("_", "Interior Elevations", roomNumber, roomName);
            baseName = RevitNameUtils.SanitizeName(baseName, "Interior Elevations");

            return GetUniqueName(baseName, _usedSheetNames, "_", 2);
        }

        public string GenerateUniqueSheetNumber(RoomData roomData)
        {
            string roomNumber = roomData != null ? roomData.RoomNumber : "000";
            string baseNumber = RevitNameUtils.SanitizeName("IE-" + roomNumber, "IE-000");

            return GetUniqueName(baseNumber, _usedSheetNumbers, "-", 2);
        }

        private string BuildViewBaseName(string prefix, string roomNumber, string roomName, string indexText)
        {
            List<string> parts = new List<string>();

            if (!string.IsNullOrWhiteSpace(prefix))
            {
                parts.Add(prefix.Trim());
            }

            if (!string.IsNullOrWhiteSpace(roomNumber))
            {
                parts.Add(roomNumber.Trim());
            }

            if (!string.IsNullOrWhiteSpace(roomName))
            {
                parts.Add(roomName.Trim());
            }

            parts.Add("Elevation");
            parts.Add(indexText);

            string[] array = parts.ToArray();
            return RevitNameUtils.BuildJoinedName("_", array);
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
