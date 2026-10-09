using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
using SAB.ParameterTools.Core;
using Profile = SAB.ParameterTools.Core.Profile;

namespace SAB.ParameterTools
{
    /// <summary>Native category filters: an element matches while any required parameter is empty.</summary>
    internal static class ViewFilterService
    {
        private static readonly Guid OwnerGuid = new Guid("5d36deac-047e-459b-9fb7-75ec424f6694");

        private sealed class Plan
        {
            internal ElementId CategoryId;
            internal string Name;
            internal ElementFilter Filter;
        }

        private static Schema OwnerSchema()
        {
            var schema = Schema.Lookup(OwnerGuid);
            if (schema != null) return schema;
            var builder = new SchemaBuilder(OwnerGuid);
            builder.SetSchemaName("SABParameterViewFilterV1");
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField("Version", typeof(int));
            return builder.Finish();
        }

        private static bool Owned(Element element)
        {
            var schema = Schema.Lookup(OwnerGuid);
            return schema != null && element is ParameterFilterElement && element.GetEntity(schema).IsValid();
        }

        private static void ValidateView(View view)
        {
            if (!view.AreGraphicsOverridesAllowed())
                throw new InvalidOperationException("Текущий вид не поддерживает фильтры графики.");
            var template = view.Document.GetElement(view.ViewTemplateId) as View;
            var filtersParameter = new ElementId(BuiltInParameter.VIS_GRAPHICS_FILTERS);
            if (!view.IsTemporaryViewPropertiesModeEnabled() && template != null && template.GetTemplateParameterIds().Contains(filtersParameter)
                && !template.GetNonControlledTemplateParameterIds().Contains(filtersParameter))
                throw new InvalidOperationException("Фильтры текущего вида управляются шаблоном «" + template.Name
                    + "». Отключите управление фильтрами в шаблоне либо включите временные свойства вида.");
        }

        private static string SafeName(string value)
        {
            const string forbidden = "\\:{}[]|;<>?`~";
            return new string((value ?? "").Select(c => forbidden.IndexOf(c) >= 0 || char.IsControl(c) ? '-' : c).ToArray());
        }

        private static string Name(Category category, IList<ParameterRef> parameters)
        {
            string identity = Ids.Value(category.Id) + ":" + string.Join(";", parameters.Select(p => p.SharedGuid ?? p.Id.ToString()));
            string hash;
            using (var sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "").Substring(0, 12);
            string label = "SAB_Param_" + SafeName(category.Name) + "_Пусто-" + string.Join("-ИЛИ-", parameters.Select(p => SafeName(p.Name)));
            if (label.Length > 220) label = label.Substring(0, 220);
            return label + "_" + hash;
        }

        private static ElementId ParameterId(Document doc, ParameterRef reference)
        {
            if (!string.IsNullOrWhiteSpace(reference.SharedGuid))
            {
                var shared = SharedParameterElement.Lookup(doc, new Guid(reference.SharedGuid));
                if (shared != null) return shared.Id;
            }
            return Ids.Create(reference.Id);
        }

        private static ElementFilter EmptyFilter(Document doc, ElementId id)
        {
            var empty = new ElementParameterFilter(ParameterFilterRuleFactory.CreateHasNoValueParameterRule(id));
            var parameter = doc.GetElement(id) as ParameterElement;
            // Empty strings may still have HasValue=true. Zero is a valid numeric value.
            if (parameter != null && parameter.GetDefinition().GetDataType() == SpecTypeId.String.Text)
                return new LogicalOrFilter(empty, new ElementParameterFilter(
#if REVIT2022
                    ParameterFilterRuleFactory.CreateEqualsRule(id, "", false)));
#else
                    ParameterFilterRuleFactory.CreateEqualsRule(id, "")));
#endif
            return empty;
        }

        internal static void Apply(UIDocument ui, Profile profile, IList<int> categoryIds)
        {
            var doc = ui.Document;
            var view = ui.ActiveView;
            ValidateView(view);
            var plans = new List<Plan>();
            foreach (int categoryId in categoryIds.Distinct())
            {
                var category = Category.GetCategory(doc, Ids.Create(categoryId));
                if (category == null) continue;
                var parameters = profile.Rules.Where(r => r.Enabled && r.Required && r.Target != null
                    && (r.CategoryIds.Count == 0 || r.CategoryIds.Contains(categoryId)))
                    .Select(r => r.Target).GroupBy(p => p.SharedGuid ?? p.Id.ToString())
                    .Select(g => g.First()).OrderBy(p => p.SharedGuid ?? p.Id.ToString(), StringComparer.Ordinal).ToList();
                if (parameters.Count == 0) continue;
                var common = ParameterFilterUtilities.GetFilterableParametersInCommon(doc, new[] { category.Id });
                var predicates = new List<ElementFilter>();
                foreach (var parameter in parameters)
                {
                    var id = ParameterId(doc, parameter);
                    if (!common.Contains(id))
                        throw new InvalidOperationException("Параметр «" + parameter.Name + "» недоступен для фильтра категории «"
                            + category.Name + "». Добавьте привязку параметра к категории и повторите проверку.");
                    predicates.Add(EmptyFilter(doc, id));
                }
                plans.Add(new Plan { CategoryId = category.Id, Name = Name(category, parameters),
                    Filter = predicates.Count == 1 ? predicates[0] : new LogicalOrFilter(predicates) });
            }
            if (plans.Count == 0) throw new InvalidOperationException("Для выбранных категорий нет включённых обязательных параметров.");
            var fill = new FilteredElementCollector(doc).OfClass(typeof(FillPatternElement)).Cast<FillPatternElement>()
                .FirstOrDefault(f => f.GetFillPattern().IsSolidFill && f.GetFillPattern().Target == FillPatternTarget.Drafting);
            if (fill == null) throw new InvalidOperationException("В проекте не найден сплошной образец заливки.");
            var existing = new FilteredElementCollector(doc).OfClass(typeof(ParameterFilterElement)).Cast<ParameterFilterElement>().ToList();
            using (var transaction = new Transaction(doc, "SAB: фильтры заполненности параметров"))
            {
                transaction.Start();
                RemoveOwned(view);
                var graphics = new OverrideGraphicSettings().SetProjectionLineColor(new Color(235, 45, 45))
                    .SetCutLineColor(new Color(235, 45, 45)).SetHalftone(false)
                    .SetSurfaceForegroundPatternId(fill.Id).SetSurfaceForegroundPatternColor(new Color(245, 195, 195))
                    .SetSurfaceForegroundPatternVisible(true).SetCutForegroundPatternId(fill.Id)
                    .SetCutForegroundPatternColor(new Color(245, 195, 195)).SetCutForegroundPatternVisible(true);
                foreach (var plan in plans)
                {
                    var filter = existing.FirstOrDefault(f => f.Name == plan.Name);
                    if (filter != null && !Owned(filter))
                        throw new InvalidOperationException("Имя фильтра «" + plan.Name + "» занято пользовательским фильтром. Переименуйте его.");
                    if (filter == null)
                    {
                        filter = ParameterFilterElement.Create(doc, plan.Name, new[] { plan.CategoryId }, plan.Filter);
                        var entity = new Entity(OwnerSchema()); entity.Set("Version", 1); filter.SetEntity(entity);
                    }
                    else filter.SetElementFilter(plan.Filter);
                    view.AddFilter(filter.Id);
                    view.SetFilterVisibility(filter.Id, true);
                    view.SetIsFilterEnabled(filter.Id, true);
                    view.SetFilterOverrides(filter.Id, graphics);
                }
                if (transaction.Commit() != TransactionStatus.Committed)
                    throw new InvalidOperationException("Revit отменил создание фильтров проверки.");
            }
            ui.RefreshActiveView();
        }

        private static int RemoveOwned(View view)
        {
            var ids = view.GetFilters().Where(id => Owned(view.Document.GetElement(id))).ToList();
            foreach (var id in ids) view.RemoveFilter(id);
            return ids.Count;
        }

        internal static int Clear(UIDocument ui)
        {
            var view = ui.ActiveView;
            if (!view.AreGraphicsOverridesAllowed() || !view.GetFilters().Any(id => Owned(ui.Document.GetElement(id)))) return 0;
            ValidateView(view);
            using (var transaction = new Transaction(ui.Document, "SAB: снять фильтры проверки с вида"))
            {
                transaction.Start();
                int count = RemoveOwned(view);
                if (transaction.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Не удалось снять фильтры проверки.");
                return count;
            }
        }
    }
}
