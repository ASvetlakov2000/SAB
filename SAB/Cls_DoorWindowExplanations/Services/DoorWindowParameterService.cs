using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.Revit.DB;
using SAB.DoorWindowExplanations.Models;

namespace SAB.DoorWindowExplanations.Services
{
    public class DoorWindowParameterService
    {
        public IList<string> GetAvailableParameterNames(DoorWindowSelectionData selection)
        {
            SortedSet<string> names = new SortedSet<string>(StringComparer.CurrentCultureIgnoreCase);
            if (selection == null)
            {
                return new List<string>();
            }

            AddParameterNames(selection.Element, names);
            AddParameterNames(selection.ElementType, names);
            return new List<string>(names);
        }

        public string GetParameterValue(DoorWindowSelectionData selection, string parameterName)
        {
            if (selection == null || string.IsNullOrWhiteSpace(parameterName))
            {
                return string.Empty;
            }

            string value = GetParameterValue(selection.Element, parameterName, selection.SourceDocument);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return GetParameterValue(selection.ElementType, parameterName, selection.SourceDocument);
        }

        public string FindPreferredPositionParameter(IList<string> parameterNames, string savedName)
        {
            if (parameterNames == null || parameterNames.Count == 0)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(savedName))
            {
                for (int i = 0; i < parameterNames.Count; i++)
                {
                    if (string.Equals(parameterNames[i], savedName, StringComparison.CurrentCultureIgnoreCase))
                    {
                        return parameterNames[i];
                    }
                }
            }

            string[] preferredNames = { "Позиция", "Марка", "Mark", "Позиционное обозначение" };
            for (int p = 0; p < preferredNames.Length; p++)
            {
                for (int i = 0; i < parameterNames.Count; i++)
                {
                    if (string.Equals(parameterNames[i], preferredNames[p], StringComparison.CurrentCultureIgnoreCase))
                    {
                        return parameterNames[i];
                    }
                }
            }

            return parameterNames[0];
        }

        private void AddParameterNames(Element element, ISet<string> names)
        {
            if (element == null)
            {
                return;
            }

            foreach (Parameter parameter in element.Parameters)
            {
                string name = parameter != null && parameter.Definition != null
                    ? parameter.Definition.Name
                    : string.Empty;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name);
                }
            }
        }

        private string GetParameterValue(Element element, string parameterName, Document document)
        {
            if (element == null)
            {
                return string.Empty;
            }

            Parameter parameter = element.LookupParameter(parameterName);
            if (parameter == null || !parameter.HasValue)
            {
                return string.Empty;
            }

            string displayValue = parameter.AsValueString();
            if (!string.IsNullOrWhiteSpace(displayValue))
            {
                return displayValue.Trim();
            }

            switch (parameter.StorageType)
            {
                case StorageType.String:
                    return (parameter.AsString() ?? string.Empty).Trim();
                case StorageType.Integer:
                    return parameter.AsInteger().ToString(CultureInfo.InvariantCulture);
                case StorageType.Double:
                    return parameter.AsDouble().ToString("0.###", CultureInfo.InvariantCulture);
                case StorageType.ElementId:
                    ElementId id = parameter.AsElementId();
                    Element referenced = document != null && id != null ? document.GetElement(id) : null;
                    return referenced != null ? referenced.Name : (id != null ? id.IntegerValue.ToString(CultureInfo.InvariantCulture) : string.Empty);
                default:
                    return string.Empty;
            }
        }
    }
}
