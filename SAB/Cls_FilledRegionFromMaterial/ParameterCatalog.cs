using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Autodesk.Revit.DB;

namespace SAB.FilledRegionFromMaterial
{
    public static class ParameterTargetScope
    {
        public const string Type = "Type";
        public const string Instance = "Instance";
    }

    public sealed class ParameterOption
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Scope { get; set; }

        public string SelectionKey
        {
            get { return string.IsNullOrEmpty(Scope) ? Key : Scope + "|" + Key; }
        }

        public string DisplayName
        {
            get
            {
                if (Scope == ParameterTargetScope.Type)
                {
                    return Name + "  [тип]";
                }

                if (Scope == ParameterTargetScope.Instance)
                {
                    return Name + "  [экземпляр]";
                }

                return Name;
            }
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    public sealed class ParameterSnapshot
    {
        public IList<ParameterOption> MaterialParameters { get; set; }
        public IList<ParameterOption> TargetParameters { get; set; }
        public int TotalMaterials { get; set; }
        public int UsedMaterials { get; set; }
        public int FilledRegionTypes { get; set; }
        public int FilledRegionInstances { get; set; }

        public static ParameterSnapshot Create(Document document)
        {
            IList<Material> allMaterials = new FilteredElementCollector(document)
                .OfClass(typeof(Material))
                .Cast<Material>()
                .ToList();
            MaterialScanResult usedScan = MaterialScanner.Scan(document, false, true);
            IList<FilledRegionType> types = new FilteredElementCollector(document)
                .OfClass(typeof(FilledRegionType))
                .Cast<FilledRegionType>()
                .ToList();
            IList<FilledRegion> instances = new FilteredElementCollector(document)
                .OfClass(typeof(FilledRegion))
                .Cast<FilledRegion>()
                .ToList();

            List<ParameterOption> materialParameters = CollectElementParameters(
                allMaterials.Cast<Element>(), null, false);
            List<ParameterOption> targetParameters = CollectElementParameters(
                types.Cast<Element>(), ParameterTargetScope.Type, true);
            targetParameters.AddRange(CollectElementParameters(
                instances.Cast<Element>(), ParameterTargetScope.Instance, true));
            AddBoundDetailItemParameters(document, targetParameters);

            return new ParameterSnapshot
            {
                MaterialParameters = DeduplicateAndSort(materialParameters, false),
                TargetParameters = DeduplicateAndSort(targetParameters, true),
                TotalMaterials = allMaterials.Count,
                UsedMaterials = usedScan.Materials.Count,
                FilledRegionTypes = types.Count,
                FilledRegionInstances = instances.Count
            };
        }

        private static List<ParameterOption> CollectElementParameters(
            IEnumerable<Element> elements,
            string scope,
            bool requireWritable)
        {
            var result = new List<ParameterOption>();

            foreach (Element element in elements)
            {
                foreach (Parameter parameter in element.Parameters)
                {
                    if (parameter == null || parameter.Definition == null || parameter.StorageType == StorageType.None ||
                        (requireWritable && parameter.IsReadOnly))
                    {
                        continue;
                    }

                    result.Add(new ParameterOption
                    {
                        Key = ParameterIdentity.FromParameter(parameter),
                        Name = parameter.Definition.Name,
                        Scope = scope
                    });
                }
            }

            return result;
        }

        private static void AddBoundDetailItemParameters(Document document, ICollection<ParameterOption> target)
        {
            Category detailItems = Category.GetCategory(document, BuiltInCategory.OST_DetailComponents);
            if (detailItems == null)
            {
                return;
            }

            DefinitionBindingMapIterator iterator = document.ParameterBindings.ForwardIterator();
            iterator.Reset();

            while (iterator.MoveNext())
            {
                Definition definition = iterator.Key as Definition;
                ElementBinding binding = iterator.Current as ElementBinding;
                if (definition == null || binding == null || !binding.Categories.Contains(detailItems))
                {
                    continue;
                }

                string scope = binding is InstanceBinding
                    ? ParameterTargetScope.Instance
                    : ParameterTargetScope.Type;
                string key = ParameterIdentity.FromDefinition(definition);

                target.Add(new ParameterOption
                {
                    Key = key,
                    Name = definition.Name,
                    Scope = scope
                });
            }
        }

        private static IList<ParameterOption> DeduplicateAndSort(
            IEnumerable<ParameterOption> source,
            bool includeScope)
        {
            return source
                .Where(option => !string.IsNullOrWhiteSpace(option.Key))
                .GroupBy(
                    option => includeScope ? option.SelectionKey : option.Key,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(option => option.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(option => option.Scope, StringComparer.Ordinal)
                .ToList();
        }
    }

    internal static class ParameterIdentity
    {
        private const string BuiltInPrefix = "bip:";
        private const string SharedPrefix = "guid:";
        private const string NamePrefix = "name:";

        public static string FromBuiltIn(BuiltInParameter parameter)
        {
            return BuiltInPrefix + ((int)parameter).ToString(CultureInfo.InvariantCulture);
        }

        public static string FromParameter(Parameter parameter)
        {
            if (parameter.IsShared)
            {
                try
                {
                    return SharedPrefix + parameter.GUID.ToString("D");
                }
                catch (InvalidOperationException)
                {
                    // Fall back to the parameter id or name.
                }
            }

            if (parameter.Id != null && parameter.Id.IntegerValue < 0)
            {
                return BuiltInPrefix + parameter.Id.IntegerValue.ToString(CultureInfo.InvariantCulture);
            }

            return NamePrefix + parameter.Definition.Name;
        }

        public static string FromDefinition(Definition definition)
        {
            ExternalDefinition external = definition as ExternalDefinition;
            if (external != null)
            {
                return SharedPrefix + external.GUID.ToString("D");
            }

            return NamePrefix + definition.Name;
        }

        public static Parameter Find(Element element, string key)
        {
            if (element == null || string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            if (key.StartsWith(BuiltInPrefix, StringComparison.Ordinal))
            {
                int value;
                return int.TryParse(key.Substring(BuiltInPrefix.Length), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out value)
                    ? element.get_Parameter((BuiltInParameter)value)
                    : null;
            }

            if (key.StartsWith(SharedPrefix, StringComparison.Ordinal))
            {
                Guid guid;
                return Guid.TryParse(key.Substring(SharedPrefix.Length), out guid)
                    ? element.get_Parameter(guid)
                    : null;
            }

            if (key.StartsWith(NamePrefix, StringComparison.Ordinal))
            {
                return element.LookupParameter(key.Substring(NamePrefix.Length));
            }

            return element.LookupParameter(key);
        }

        public static string GetTargetScope(string targetSelectionKey)
        {
            int separator = string.IsNullOrEmpty(targetSelectionKey)
                ? -1
                : targetSelectionKey.IndexOf('|');
            return separator < 0 ? ParameterTargetScope.Type : targetSelectionKey.Substring(0, separator);
        }

        public static string GetTargetKey(string targetSelectionKey)
        {
            int separator = string.IsNullOrEmpty(targetSelectionKey)
                ? -1
                : targetSelectionKey.IndexOf('|');
            return separator < 0 ? targetSelectionKey : targetSelectionKey.Substring(separator + 1);
        }
    }

    internal static class ParameterValueCopier
    {
        public static bool TryCopy(
            Element sourceElement,
            string sourceKey,
            Element targetElement,
            string targetKey,
            out string error)
        {
            error = null;
            Parameter source = ParameterIdentity.Find(sourceElement, sourceKey);
            Parameter target = ParameterIdentity.Find(targetElement, targetKey);

            if (source == null)
            {
                error = "исходный параметр не найден";
                return false;
            }

            if (target == null)
            {
                error = "целевой параметр не найден";
                return false;
            }

            if (target.IsReadOnly)
            {
                error = "целевой параметр доступен только для чтения";
                return false;
            }

            if (target.StorageType == StorageType.String)
            {
                string text = source.StorageType == StorageType.String
                    ? source.AsString() ?? string.Empty
                    : source.AsValueString() ?? string.Empty;
                target.Set(text);
                return true;
            }

            if (source.StorageType != target.StorageType)
            {
                error = "несовместимые типы данных";
                return false;
            }

            switch (target.StorageType)
            {
                case StorageType.Integer:
                    target.Set(source.AsInteger());
                    return true;
                case StorageType.Double:
                    target.Set(source.AsDouble());
                    return true;
                case StorageType.ElementId:
                    target.Set(source.AsElementId());
                    return true;
                default:
                    error = "тип данных не поддерживается";
                    return false;
            }
        }
    }
}
