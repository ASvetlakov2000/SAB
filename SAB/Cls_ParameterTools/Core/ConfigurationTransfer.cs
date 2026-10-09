using System;
using System.Collections.Generic;
using System.Linq;

namespace SAB.ParameterTools.Core
{
    public sealed class ParameterConfiguration
    {
        public string Format { get; set; } = "SAB.ParameterTools";
        public int Version { get; set; } = 1;
        public string SourceModelId { get; set; }
        public string ExportedAtUtc { get; set; }
        public Profile Profile { get; set; }
    }
    public sealed class ConfigurationImport
    {
        public Profile Profile { get; set; }
        public List<string> Notes { get; } = new List<string>();
    }
    public static class ConfigurationTransfer
    {
        public static ConfigurationImport Prepare(ParameterConfiguration file, string modelId,
            IList<ParameterRef> targets, IList<ParameterRef> roomParameters, IList<ParameterRef> elementParameters,
            IList<LevelMapping> levels, IList<int> categories)
        {
            if (file == null || file.Format != "SAB.ParameterTools" || file.Version != 1 || file.Profile == null || file.Profile.SchemaVersion != 1)
                throw new ArgumentException("Файл не является поддерживаемой конфигурацией параметров SAB.");
            var p = file.Profile;
            if (p.Rules == null || p.Levels == null || p.CategoryIds == null || p.Corpora == null || p.ArchivedAdvancedRules == null || p.CheckCategoryIds == null
                || p.Rules.Any(r => r == null || r.Mappings == null || r.ManualValues == null || r.Mappings.Any(m => m == null) || r.Conditions == null || r.CategoryIds == null)
                || p.Levels.Any(l => l == null) || p.Rules.Any(r => string.IsNullOrWhiteSpace(r.Id)) || p.Rules.GroupBy(r => r.Id).Any(g => g.Count() > 1))
                throw new ArgumentException("Структура конфигурации повреждена. Текущие настройки не изменены.");
            var result = new ConfigurationImport { Profile = p };
            bool sameModel = !string.IsNullOrWhiteSpace(modelId) && modelId == file.SourceModelId;
            Func<ParameterRef, IList<ParameterRef>, ParameterRef> rebind = (parameter, catalog) => {
                if (parameter == null) return null;
                var matches = catalog.Where(c => !string.IsNullOrWhiteSpace(parameter.SharedGuid)
                    ? string.Equals(c.SharedGuid, parameter.SharedGuid, StringComparison.OrdinalIgnoreCase)
                    : (parameter.Id < 0 || sameModel) && c.Id == parameter.Id && c.Name == parameter.Name).ToList();
                if (matches.Count == 1) return matches[0];
                result.Notes.Add("Выберите заново: " + parameter.Name + " (" + parameter.Identity + "). "
                    + (string.IsNullOrWhiteSpace(parameter.SharedGuid) && parameter.Id > 0 && !sameModel
                        ? "Локальный ID другой модели не переносится." : "Параметр не найден однозначно."));
                return null;
            };
            foreach (var rule in p.Rules)
            {
                RuleEngine.NormalizeRule(p, rule);
                rule.Target = rebind(rule.Target, targets);
                rule.RoomParameter = rebind(rule.RoomParameter, roomParameters);
                rule.ElementParameter = rebind(rule.ElementParameter, elementParameters);
                rule.MappingParameter = rebind(rule.MappingParameter, rule.MappingInput == MappingInput.RoomParameter ? roomParameters : elementParameters);
            }
            p.RoomCorpusParameter = rebind(p.RoomCorpusParameter, roomParameters);
            var imported = p.Levels;
            p.Levels = levels.Select(l => new LevelMapping { LevelUniqueId = l.LevelUniqueId, LevelName = l.LevelName }).ToList();
            var assigned = new HashSet<string>();
            foreach (var row in imported.Where(l => !string.IsNullOrWhiteSpace(l.Value)))
            {
                var matches = p.Levels.Where(l => sameModel ? l.LevelUniqueId == row.LevelUniqueId : l.LevelName == row.LevelName).ToList();
                if (matches.Count == 1 && assigned.Add(matches[0].LevelUniqueId)) matches[0].Value = row.Value;
                else result.Notes.Add("Не перенесено значение уровня «" + row.LevelName + "» → «" + row.Value + "»: выберите соответствие в текущей модели.");
            }
            var unavailable = p.CategoryIds.Except(categories).ToList();
            if (unavailable.Count > 0) result.Notes.Add("Часть категорий отсутствует в текущей модели: " + string.Join(", ", unavailable));
            p.CategoryIds = p.CategoryIds.Intersect(categories).ToList();
            RuleEngine.ArchiveAdvancedOptions(p);
            return result;
        }
    }
}
