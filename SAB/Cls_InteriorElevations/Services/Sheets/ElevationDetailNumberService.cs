using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;

namespace SAB.InteriorElevations.Services.Sheets
{
    public sealed class ElevationDetailNumberService
    {
        // A common suffix identifies one ordered group, including views on continuation sheets.
        public IDictionary<int, string> ReserveGroupNumbers(Document document, IEnumerable<int> indices,
            string prefix, string suffix)
        {
            var numbers = new List<string>();
            foreach (Element element in new FilteredElementCollector(document).OfClass(typeof(Viewport)))
            {
                numbers.Add(Read(element as Viewport));
            }

            return ResolveGroupNumbers(numbers, indices, prefix, suffix);
        }

        public static IDictionary<int, string> ResolveGroupNumbers(IEnumerable<string> numbers,
            IEnumerable<int> indices, string prefix, string suffix)
        {
            var used = new HashSet<string>(numbers, StringComparer.OrdinalIgnoreCase);
            var sequence = new List<int>(indices);
            prefix = prefix ?? string.Empty;
            suffix = suffix ?? string.Empty;
            for (int group = 1; ; group = checked(group + 1))
            {
                string candidateSuffix = suffix + (group == 1 ? string.Empty : "-" + group.ToString(CultureInfo.InvariantCulture));
                // Reserve the entire namespace, even if an earlier group has only one surviving view.
                var pattern = new Regex("^" + Regex.Escape(prefix) + "[0-9]+" + Regex.Escape(candidateSuffix) + "$",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                bool occupied = false;
                foreach (string number in used)
                    if (number != null && pattern.IsMatch(number)) { occupied = true; break; }
                if (occupied) continue;

                var result = new Dictionary<int, string>();
                foreach (int index in sequence) result.Add(index, BuildNumber(index, prefix, candidateSuffix));
                return result;
            }
        }

        public static string BuildNumber(int sequenceIndex, string prefix, string suffix)
        {
            if (sequenceIndex <= 0) throw new ArgumentOutOfRangeException("sequenceIndex");
            return (prefix ?? string.Empty) + sequenceIndex.ToString(CultureInfo.InvariantCulture) + (suffix ?? string.Empty);
        }

        public static string Read(Viewport viewport)
        {
            Parameter parameter = viewport != null
                ? viewport.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER) : null;
            return parameter != null ? parameter.AsString() : null;
        }

        public static void Apply(Viewport viewport, string number)
        {
            if (string.IsNullOrWhiteSpace(number)) return;
            Parameter parameter = viewport != null
                ? viewport.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER) : null;
            if (parameter == null || parameter.IsReadOnly || parameter.StorageType != StorageType.String)
            {
                throw new InvalidOperationException("Параметр «Номер вида» недоступен для записи.");
            }

            if (!string.Equals(parameter.AsString(), number, StringComparison.Ordinal) && !parameter.Set(number))
            {
                throw new InvalidOperationException("Не удалось назначить номер вида «" + number + "».");
            }
            if (!string.Equals(parameter.AsString(), number, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Revit не подтвердил номер вида «" + number + "».");
            }
        }
    }
}
