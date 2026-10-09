using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using SAB.ParameterTools.Core;

namespace SAB.ParameterTools
{
    internal static class BindingService
    {
        private sealed class PreviousValue
        {
            internal ElementId ElementId;
            internal StorageType Type;
            internal string Text;
            internal double Number;
            internal int Integer;
            internal ElementId Reference;
        }
        // Caller owns the transaction: binding expansion and parameter writes undo together.
        internal static void Ensure(Document doc, IList<Element> elements, IList<Rule> rules)
        {
            foreach (var rule in rules)
            {
                var missing = elements.Where(e => Catalog.Parameter(e, rule.Target) == null).ToList();
                if (missing.Count == 0) continue;
                InternalDefinition definition = null;
                var existing = new Dictionary<int, Category>();
                var iterator = doc.ParameterBindings.ForwardIterator();
                while (iterator.MoveNext())
                {
                    var candidate = iterator.Key as InternalDefinition;
                    if (candidate == null) continue;
                    var shared = doc.GetElement(candidate.Id) as SharedParameterElement;
                    bool match = string.IsNullOrEmpty(rule.Target.SharedGuid) ? Ids.Value(candidate.Id) == rule.Target.Id
                        : shared != null && string.Equals(shared.GuidValue.ToString(), rule.Target.SharedGuid, StringComparison.OrdinalIgnoreCase);
                    if (!match) continue;
                    var binding = iterator.Current as InstanceBinding;
                    if (binding == null) throw new InvalidOperationException("Параметр «" + rule.Target.Name + "» привязан к типу. Автоматически менять тип привязки нельзя.");
                    definition = candidate;
                    foreach (Category category in binding.Categories) existing[Ids.Category(category.Id)] = category;
                }
                if (definition == null) throw new InvalidOperationException("Не найдена привязка параметра «" + rule.Target.Name + "» в этом проекте. Выберите существующий параметр проекта в настройках.");
                var required = missing.Select(e => e.Category).GroupBy(c => Ids.Category(c.Id)).Select(g => g.First()).ToList();
                foreach (var category in required)
                {
                    if (!category.AllowsBoundParameters) throw new InvalidOperationException("Категория «" + category.Name + "» не допускает параметры проекта.");
                    if (existing.ContainsKey(Ids.Category(category.Id))) throw new InvalidOperationException("Параметр «" + rule.Target.Name + "» уже привязан к категории «" + category.Name + "», но отсутствует у элемента. Автоматическая привязка не устранит эту проблему.");
                }
                // ReInsert may rebuild a binding. Preserve data on all previously bound instances.
                var oldValues = Snapshot(doc, rule.Target, existing.Keys.ToList());
                var set = doc.Application.Create.NewCategorySet();
                foreach (var category in existing.Values.Concat(required).GroupBy(c => Ids.Category(c.Id)).Select(g => g.First())) set.Insert(category);
                var expanded = doc.Application.Create.NewInstanceBinding(set);
                bool variedAcrossGroups = definition.VariesAcrossGroups;
                // Keep the established group-binding overload for the 2022 build.
#if REVIT2022
                bool rebound = doc.ParameterBindings.ReInsert(definition, expanded, definition.ParameterGroup);
#else
                bool rebound = doc.ParameterBindings.ReInsert(definition, expanded, definition.GetGroupTypeId());
#endif
                if (!rebound)
                    throw new InvalidOperationException("Revit не смог расширить привязку параметра «" + rule.Target.Name + "».");
                var reboundDefinition = (doc.GetElement(definition.Id) as ParameterElement)?.GetDefinition();
                if (reboundDefinition == null) throw new InvalidOperationException("После расширения привязки не найдено определение параметра «" + rule.Target.Name + "».");
                if (variedAcrossGroups && !reboundDefinition.VariesAcrossGroups) reboundDefinition.SetAllowVaryBetweenGroups(doc, true);
                doc.Regenerate();
                foreach (var previous in oldValues)
                {
                    var element = doc.GetElement(previous.ElementId);
                    var parameter = Catalog.Parameter(element, rule.Target);
                    if (parameter == null || parameter.StorageType != previous.Type)
                        throw new InvalidOperationException("Не удалось сохранить данные параметра «" + rule.Target.Name + "». Операция отменена.");
                    bool equal = parameter.HasValue && (previous.Type == StorageType.String ? parameter.AsString() == previous.Text
                        : previous.Type == StorageType.Double ? parameter.AsDouble() == previous.Number
                        : previous.Type == StorageType.Integer ? parameter.AsInteger() == previous.Integer
                        : parameter.AsElementId() == previous.Reference);
                    if (equal) continue;
                    if (parameter.IsReadOnly) throw new InvalidOperationException("Не удалось восстановить существующее значение параметра «" + rule.Target.Name + "».");
                    if (previous.Type == StorageType.String) parameter.Set(previous.Text ?? "");
                    else if (previous.Type == StorageType.Double) parameter.Set(previous.Number);
                    else if (previous.Type == StorageType.Integer) parameter.Set(previous.Integer);
                    else if (previous.Type == StorageType.ElementId) parameter.Set(previous.Reference);
                    bool restored = parameter.HasValue && (previous.Type == StorageType.String ? (parameter.AsString() ?? "") == (previous.Text ?? "")
                        : previous.Type == StorageType.Double ? parameter.AsDouble() == previous.Number
                        : previous.Type == StorageType.Integer ? parameter.AsInteger() == previous.Integer
                        : parameter.AsElementId() == previous.Reference);
                    if (!restored) throw new InvalidOperationException("Revit не восстановил существующее значение параметра «" + rule.Target.Name + "». Операция отменена.");
                }
                if (missing.Any(e => Catalog.Parameter(e, rule.Target) == null))
                    throw new InvalidOperationException("После расширения привязки параметр «" + rule.Target.Name + "» не появился у элемента.");
            }
        }
        private static List<PreviousValue> Snapshot(Document doc, ParameterRef reference, IList<int> categoryIds)
        {
            var list = new List<PreviousValue>();
            if (categoryIds.Count == 0) return list;
            var filter = new ElementMulticategoryFilter(categoryIds.Select(id => Ids.Create(id)).ToList());
            foreach (var e in new FilteredElementCollector(doc).WherePasses(filter).WhereElementIsNotElementType())
            {
                var p = Catalog.Parameter(e, reference);
                if (p == null || !p.HasValue || p.StorageType == StorageType.None) continue;
                var saved = new PreviousValue { ElementId = e.Id, Type = p.StorageType };
                if (p.StorageType == StorageType.String) saved.Text = p.AsString();
                else if (p.StorageType == StorageType.Double) saved.Number = p.AsDouble();
                else if (p.StorageType == StorageType.Integer) saved.Integer = p.AsInteger();
                else if (p.StorageType == StorageType.ElementId) saved.Reference = p.AsElementId();
                list.Add(saved);
            }
            return list;
        }
    }
}
