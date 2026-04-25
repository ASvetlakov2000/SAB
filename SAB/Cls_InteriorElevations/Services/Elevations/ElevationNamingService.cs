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

        public string GenerateUniqueElevationViewName(RoomData roomData, int startPointNumber, int endPointNumber)
        {
            string roomNumber = roomData != null ? roomData.RoomNumber : "Без номера";
            string roomName = roomData != null ? roomData.RoomName : "Без имени";

            string baseName =
                "ELV_r" + roomNumber +
                "_" + roomName +
                "_Elev_" + startPointNumber + "-" + endPointNumber;

            baseName = RevitNameUtils.SanitizeName(baseName, "ELV_rБез номера_Без имени_Elev_1-2");
            return GetUniqueName(baseName, _usedViewNames, "_", 2);
        }

        public string GenerateUniqueSheetName(RoomData roomData)
        {
            string roomNumber = roomData != null ? roomData.RoomNumber : "Без номера";
            string roomName = roomData != null ? roomData.RoomName : "Без имени";

            string baseName = "Развертки стен помещения №" + roomNumber + " " + roomName;
            baseName = RevitNameUtils.SanitizeName(baseName, "Развертки стен помещения №Без номера Без имени");

            return GetUniqueName(baseName, _usedSheetNames, "_", 2);
        }

        public string GenerateUniqueSheetNumber(RoomData roomData)
        {
            string roomNumber = roomData != null ? roomData.RoomNumber : "000";
            string baseNumber = RevitNameUtils.SanitizeName("ELV-" + roomNumber, "ELV-000");

            return GetUniqueName(baseNumber, _usedSheetNumbers, "-", 2);
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
