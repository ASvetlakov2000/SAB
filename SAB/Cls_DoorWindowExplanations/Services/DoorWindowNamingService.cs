using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using SAB.DoorWindowExplanations.Models;

namespace SAB.DoorWindowExplanations.Services
{
    public class DoorWindowNamingService
    {
        private static readonly char[] InvalidViewNameCharacters = { '\\', ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~' };

        public IList<string> BuildUniqueNames(
            Document document,
            DoorWindowSelectionData selection,
            DoorWindowViewSettings settings)
        {
            return BuildUniqueNames(document, selection, settings, null);
        }

        public IList<string> BuildUniqueNames(
            Document document,
            DoorWindowSelectionData selection,
            DoorWindowViewSettings settings,
            ISet<string> reservedNames)
        {
            DoorWindowParameterService parameterService = new DoorWindowParameterService();
            string position = parameterService.GetParameterValue(selection, settings.PositionParameterName);
            string category = selection != null ? selection.CategoryName : string.Empty;
            string elevationSide = settings.ElevationSide == DoorWindowElevationSide.Back
                ? "Сзади"
                : "Спереди";

            HashSet<string> usedNames = new HashSet<string>(
                new FilteredElementCollector(document)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(view => !view.IsTemplate)
                    .Select(view => view.Name),
                StringComparer.CurrentCultureIgnoreCase);

            if (reservedNames != null)
            {
                usedNames.UnionWith(reservedNames);
            }

            List<string> result = new List<string>();
            AddUniqueName(result, usedNames, Evaluate(settings.TopViewNameFormula, category, position, string.Empty, "Сверху"));
            AddUniqueName(result, usedNames, Evaluate(settings.FrontViewNameFormula, category, position, elevationSide, elevationSide));
            AddUniqueName(result, usedNames, Evaluate(settings.SectionViewNameFormula, category, position, string.Empty, "Разрез"));
            if (reservedNames != null)
            {
                for (int i = 0; i < result.Count; i++)
                {
                    reservedNames.Add(result[i]);
                }
            }

            return result;
        }

        private string Evaluate(
            string formula,
            string category,
            string position,
            string elevationSide,
            string fallbackSuffix)
        {
            string value = formula ?? string.Empty;
            value = ReplaceToken(value, "{Категория}", category ?? string.Empty);
            value = ReplaceToken(value, "{Позиция}", position ?? string.Empty);
            value = ReplaceToken(value, "{Сторона}", elevationSide ?? string.Empty);
            value = value.Trim();

            for (int i = 0; i < InvalidViewNameCharacters.Length; i++)
            {
                value = value.Replace(InvalidViewNameCharacters[i], '_');
            }

            while (value.Contains("__"))
            {
                value = value.Replace("__", "_");
            }

            value = value.Trim(' ', '_');
            return string.IsNullOrWhiteSpace(value) ? "Экспликация_" + fallbackSuffix : value;
        }

        private string ReplaceToken(string value, string token, string replacement)
        {
            int index = value.IndexOf(token, StringComparison.CurrentCultureIgnoreCase);
            while (index >= 0)
            {
                value = value.Substring(0, index) + replacement + value.Substring(index + token.Length);
                index = value.IndexOf(token, index + replacement.Length, StringComparison.CurrentCultureIgnoreCase);
            }

            return value;
        }

        private void AddUniqueName(ICollection<string> result, ISet<string> usedNames, string baseName)
        {
            string candidate = baseName;
            int suffix = 2;
            while (usedNames.Contains(candidate))
            {
                candidate = baseName + " (" + suffix + ")";
                suffix++;
            }

            usedNames.Add(candidate);
            result.Add(candidate);
        }
    }
}
