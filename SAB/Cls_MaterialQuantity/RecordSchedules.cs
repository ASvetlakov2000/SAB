using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;

namespace SAB.MaterialQuantity
{
    internal static class RecordSchedules
    {
        private const string ReleaseName = "SAB — Материалы по слоям — Черновая ведомость";
        private const string AuditName = "SAB — Материалы по слоям — Контроль";
        private const string ProhibitedNameCharacters = "{}[]|;<>?`~\\:";

        internal static void Ensure(Document project, Action<int, int, string> progress = null)
        {
            EnsureSchedule(project, ReleaseName, true);
            if (progress != null)
                progress(1, 2, ReleaseName);
            EnsureSchedule(project, AuditName, false);
            if (progress != null)
                progress(2, 2, AuditName);
        }

        private static void EnsureSchedule(Document project, string name, bool release)
        {
            name = SanitizeViewName(name);
            ViewSchedule existing = new FilteredElementCollector(project)
                .OfClass(typeof(ViewSchedule)).Cast<ViewSchedule>()
                .FirstOrDefault(item => !item.IsTemplate && item.Name == name);
            if (existing != null)
            {
                UpdateAggregateHeadings(project, existing, release);
                EnsureMaterialFields(project, existing.Definition);
                return; // Preserve existing fields, filters, sorting and user formatting.
            }

            ViewSchedule schedule = ViewSchedule.CreateSchedule(
                project, new ElementId(BuiltInCategory.OST_GenericModel));
            schedule.Name = name;
            ScheduleDefinition definition = schedule.Definition;
            var available = definition.GetSchedulableFields()
                .GroupBy(field => field.GetName(project), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            ScheduleField marker = Add(definition, available, RecordFields.Marker, "Служебный признак");
            marker.IsHidden = true;
            definition.AddFilter(new ScheduleFilter(
                marker.FieldId, ScheduleFilterType.Equal, RecordFields.MarkerValue));

            if (release)
            {
                ScheduleField status = Add(definition, available, RecordFields.Status, "Статус");
                status.IsHidden = true;
                definition.AddFilter(new ScheduleFilter(status.FieldId, ScheduleFilterType.Equal, RecordFields.Ready));

                ScheduleField unit = Add(definition, available, RecordFields.Unit, "Ед. изм.");
                ScheduleField material = Add(definition, available, RecordFields.MaterialName, "Материал: Имя");
                Add(definition, available, RecordFields.MaterialDescription, "Материал: Описание");
                Add(definition, available, RecordFields.MaterialModel, "Материал: Модель");
                ScheduleField quantity = Add(definition, available, RecordFields.Quantity, "Количество");
                if (quantity.CanTotal()) quantity.DisplayType = ScheduleFieldDisplayType.Totals;

                definition.AddSortGroupField(new ScheduleSortGroupField(unit.FieldId));
                definition.AddSortGroupField(new ScheduleSortGroupField(material.FieldId));
                definition.IsItemized = false;
                // No grand total: metres, square metres and cubic metres must never be added together.
                definition.ShowGrandTotal = false;
            }
            else
            {
                ScheduleField status = Add(definition, available, RecordFields.Status, "Статус");
                Add(definition, available, RecordFields.SourceCategory, "Категория");
                Add(definition, available, RecordFields.SourceType, "Тип конструкции");
                Add(definition, available, RecordFields.LayerIndex, "Слой №");
                Add(definition, available, RecordFields.MaterialName, "Материал: Имя");
                Add(definition, available, RecordFields.MaterialDescription, "Материал: Описание");
                Add(definition, available, RecordFields.MaterialModel, "Материал: Модель");
                Add(definition, available, RecordFields.Thickness, "Толщина");
                Add(definition, available, RecordFields.MaterialVolume, "Объём Revit");
                Add(definition, available, RecordFields.Unit, "Ед. изм.");
                Add(definition, available, RecordFields.Quantity, "Количество");
                Add(definition, available, RecordFields.SourceId, "Источники");
                definition.AddSortGroupField(new ScheduleSortGroupField(status.FieldId));
                definition.IsItemized = true;
            }
        }

        private static void EnsureMaterialFields(Document project, ScheduleDefinition definition)
        {
            var available = definition.GetSchedulableFields()
                .GroupBy(field => field.GetName(project), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var present = new HashSet<string>(StringComparer.Ordinal);
            var order = definition.GetFieldOrder().ToList();
            int materialIndex = -1;
            for (int index = 0; index < order.Count; index++)
            {
                ScheduleField field = definition.GetField(order[index]);
                if (!field.HasSchedulableField) continue;
                string name = field.GetSchedulableField().GetName(project);
                present.Add(name);
                if (name == RecordFields.MaterialName || name == RecordFields.LegacyMaterialName)
                    materialIndex = index;
            }

            // The legacy name column remains valid because synchronization writes both name parameters.
            if (materialIndex < 0)
            {
                ScheduleField name = Add(definition, available, RecordFields.MaterialName, "Материал: Имя");
                order.Add(name.FieldId);
                materialIndex = order.Count - 1;
            }

            int insertionIndex = materialIndex + 1;
            if (!present.Contains(RecordFields.MaterialDescription))
            {
                ScheduleField description = Add(definition, available,
                    RecordFields.MaterialDescription, "Материал: Описание");
                order.Insert(insertionIndex++, description.FieldId);
            }
            if (!present.Contains(RecordFields.MaterialModel))
            {
                ScheduleField model = Add(definition, available,
                    RecordFields.MaterialModel, "Материал: Модель");
                order.Insert(insertionIndex, model.FieldId);
            }
            definition.SetFieldOrder(order);
        }

        private static void UpdateAggregateHeadings(
            Document project,
            ViewSchedule schedule,
            bool release)
        {
            if (release) return;

            ScheduleDefinition definition = schedule.Definition;
            foreach (ScheduleFieldId fieldId in definition.GetFieldOrder())
            {
                ScheduleField field = definition.GetField(fieldId);
                if (!field.HasSchedulableField) continue;

                string parameterName = field.GetSchedulableField().GetName(project);
                if (parameterName == RecordFields.SourceId)
                    field.ColumnHeading = "Источники";
                else if (parameterName == RecordFields.LayerIndex)
                    field.ColumnHeading = "Слои";
            }
        }

        private static string SanitizeViewName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Имя спецификации не может быть пустым.", nameof(name));

            var result = new StringBuilder(name.Length);
            foreach (char character in name.Trim())
            {
                if (char.IsControl(character))
                    result.Append(' ');
                else if (ProhibitedNameCharacters.IndexOf(character) >= 0)
                    result.Append('—');
                else
                    result.Append(character);
            }

            return Regex.Replace(result.ToString(), @"\s+", " ");
        }

        private static ScheduleField Add(
            ScheduleDefinition definition,
            IDictionary<string, SchedulableField> available,
            string name,
            string heading)
        {
            SchedulableField field;
            if (!available.TryGetValue(name, out field))
                throw new InvalidOperationException("Параметр " + name + " недоступен для спецификации.");
            ScheduleField added = definition.AddField(field);
            added.ColumnHeading = heading;
            return added;
        }
    }
}
