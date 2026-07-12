using System;
using System.IO;
using SAB.BimDashboard.Models;

namespace SAB.BimDashboard.Services
{
    /// <summary>
    /// Central profile detection for CSV files used by the viewer and its preparation window.
    /// </summary>
    public static class DashboardProfileResolver
    {
        public static DashboardProfileType DetectFromFilePath(string filePath)
        {
            DashboardProfileType profile;

            if (TryDetectFromFilePath(filePath, out profile))
            {
                return profile;
            }

            throw new InvalidOperationException(
                "Имя файла не соответствует поддерживаемым шаблонам.\n" +
                "Ожидается, что имя содержит:\n" +
                "- \"Системные семейства\"\n" +
                "- \"Загружаемые семейства\"\n" +
                "- \"Линии\"\n" +
                "- \"Штриховки\"");
        }

        public static bool TryDetectFromFilePath(string filePath, out DashboardProfileType profile)
        {
            profile = DashboardProfileType.SystemFamilies;
            string fileNameWithoutExtension;

            try
            {
                fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath) ?? string.Empty;
            }
            catch
            {
                return false;
            }

            if (fileNameWithoutExtension.IndexOf("Системные семейства", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                profile = DashboardProfileType.SystemFamilies;
                return true;
            }

            if (fileNameWithoutExtension.IndexOf("Загружаемые семейства", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                profile = DashboardProfileType.LoadableFamilies;
                return true;
            }

            if (fileNameWithoutExtension.IndexOf("Линии", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileNameWithoutExtension.IndexOf("LineStyles", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                profile = DashboardProfileType.Lines;
                return true;
            }

            if (fileNameWithoutExtension.IndexOf("Штриховки", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fileNameWithoutExtension.IndexOf("FillPatterns", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                profile = DashboardProfileType.FillPatterns;
                return true;
            }

            return false;
        }

        public static string GetDisplayName(DashboardProfileType profile)
        {
            switch (profile)
            {
                case DashboardProfileType.SystemFamilies:
                    return "Системные типы";
                case DashboardProfileType.LoadableFamilies:
                    return "Загружаемые семейства";
                case DashboardProfileType.Lines:
                    return "Стили линий";
                case DashboardProfileType.FillPatterns:
                    return "Штриховки";
                default:
                    return profile.ToString();
            }
        }
    }
}
