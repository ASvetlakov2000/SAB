using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using SAB.ParameterTools.Core;
using Profile = SAB.ParameterTools.Core.Profile;

namespace SAB.ParameterTools
{
    internal sealed class RoomFilter : ISelectionFilter
    {
        public bool AllowElement(Element element)
        {
            var tag = element as RoomTag;
            return element is Room || tag != null && tag.Room != null && ReferenceEquals(tag.Room.Document, element.Document);
        }
        public bool AllowReference(Reference reference, XYZ point) { return false; }
    }
    internal sealed class Issue
    {
        public ElementId ElementId { get; set; }
        public string Element { get; set; }
        public string Parameter { get; set; }
        public string Actual { get; set; }
        public string Expected { get; set; }
        public string Reason { get; set; }
        public string Technical { get; set; }
    }
    internal static class Workflow
    {
        private sealed class PendingWrite
        {
            internal ElementId ElementId;
            internal string ElementLabel;
            internal Rule Rule;
            internal string Expected;
        }
        internal static Profile RequireProfile(Document doc)
        {
            var p = Storage.Load(doc);
            if (!p.Configured) throw new InvalidOperationException("Сначала выполните команду «Настройки параметров» и сохраните профиль.");
            var errors = RuleEngine.Validate(p).ToList();
            if (errors.Count > 0) throw new InvalidOperationException(string.Join("\n", errors));
            return p;
        }
        internal static List<Element> Targets(UIDocument ui, Profile p, bool selectionRequired, ICollection<ElementId> captured = null)
        {
            var selected = captured ?? (selectionRequired ? ui.Selection.GetElementIds() : new List<ElementId>());
            if (selectionRequired && selected.Count == 0) throw new InvalidOperationException("Сначала выделите элементы для заполнения.");
            IEnumerable<Element> candidates = selected.Count > 0 ? SelectionElements(ui.Document, selected, p.ExpandSelectedGroups)
                : new FilteredElementCollector(ui.Document, ui.ActiveView.Id).WhereElementIsNotElementType();
            var plan = ui.ActiveView as ViewPlan;
            Level planLevel = plan == null ? null : plan.GenLevel;
            var result = candidates.Where(e => e != null && !(e is ElementType) && e.Category != null
                && p.CategoryIds.Contains(Ids.Category(e.Category.Id))).ToList();
            // Do not relabel the visible underlay or the base floor of a multistory wall.
            if (selectionRequired && planLevel != null && p.LevelSource == LevelSource.ActivePlan && p.Rules.Any(r => r.Enabled && RuleEngine.UsesLevel(p, r)))
                result = result.Where(e => { var l = LevelService.ElementLevel(e); return l != null && l.Id == planLevel.Id; }).ToList();
            if (result.Count == 0) throw new InvalidOperationException("Нет подходящих элементов выбранных категорий и уровня. Проверьте область применения профиля.");
            if (selected.Count > 0 && result.Count != selected.Count)
                UI.Toast.Show(ui.Application.MainWindowHandle, "Выбрано объектов: " + selected.Count + ". Подходящих элементов внутри выделения: " + result.Count + ". Категории и уровень определяются настройками.");
            return result;
        }
        private static List<Element> SelectionElements(Document doc, IEnumerable<ElementId> selected, bool expandGroups)
        {
            var visited = new HashSet<ElementId>(); var result = new List<Element>();
            Action<ElementId> visit = null;
            visit = id => {
                if (!visited.Add(id)) return;
                var element = doc.GetElement(id); if (element == null) return;
                var group = element as Group;
                if (expandGroups && group != null) { foreach (var member in group.GetMemberIds()) visit(member); }
                else result.Add(element);
            };
            foreach (var id in selected) visit(id);
            return result;
        }
        private static string LevelId(UIDocument ui, Profile p, Element e, Provenance source, string chosenLevel = null)
        {
            var plan = ui.ActiveView as ViewPlan;
            var level = p.LevelSource == LevelSource.ActivePlan ? (plan == null ? null : plan.GenLevel) : LevelService.ElementLevel(e);
            return level == null ? chosenLevel ?? source.LevelUniqueId : level.UniqueId;
        }
        private static string ChooseLevel(UIDocument ui, Profile profile, IEnumerable<Element> targets, IEnumerable<Rule> rules, bool useStored = false)
        {
            if (!rules.Any(r => RuleEngine.UsesLevel(profile, r))) return null;
            var unresolved = targets.Where(e => (profile.LevelSource == LevelSource.ActivePlan ? (ui.ActiveView as ViewPlan)?.GenLevel == null : LevelService.ElementLevel(e) == null)
                && (!useStored || !StoredLevelValid(ui.Document, profile, Storage.Source(e).LevelUniqueId))).ToList();
            bool missing = unresolved.Count > 0;
            if (!missing) return null;
            var levels = profile.Levels.Where(l => !string.IsNullOrWhiteSpace(l.Value) && ui.Document.GetElement(l.LevelUniqueId) is Level).ToList();
            if (levels.Count == 0) throw new InvalidOperationException("Уровень вида или элемента не определён. Откройте «Настройки параметров» → «Матрица уровней», укажите номера этажей и сохраните.");
            var window = new UI.ModelSourceWindow("Выбрать уровень", "Уровень не определён. Выберите заполненный уровень из матрицы для этой операции.", levels.Select(l => new UI.ModelSourceChoice { Id = l.LevelUniqueId, Name = l.LevelName + " → этаж " + l.Value }).ToList());
            new System.Windows.Interop.WindowInteropHelper(window).Owner = ui.Application.MainWindowHandle;
            return window.ShowDialog() == true ? window.SelectedId : throw new System.OperationCanceledException();
        }
        private static bool StoredLevelValid(Document doc, Profile profile, string id)
        { return !string.IsNullOrWhiteSpace(id) && profile.Levels.Any(l => l.LevelUniqueId == id && !string.IsNullOrWhiteSpace(l.Value)) && doc.GetElement(id) is Level; }
        private static Resolution RoomValue(Room room, RoomField field, ParameterRef parameter)
        {
            if (room == null) return Resolution.Unknown("Не задано помещение-источник или оно удалено.");
            if (field == RoomField.Number) return Resolution.Known(room.Number);
            if (field == RoomField.Name) return Resolution.Known(room.get_Parameter(BuiltInParameter.ROOM_NAME).AsString());
            var p = Catalog.Read(Catalog.Parameter(room, parameter));
            if (!p.Exists || !p.HasValue) return Resolution.Unknown("Параметр источника помещения отсутствует или не заполнен.");
            return Resolution.Known(p.Numeric ? p.Number.ToString("G", System.Globalization.CultureInfo.InvariantCulture) : p.Text);
        }
        private static Resolution SourceValue(UIDocument ui, Profile profile, Rule rule, Element e, Provenance source, Room room, string chosenLevel, Func<ParameterRef, Resolution> read)
        {
            switch (RuleEngine.Source(profile, rule))
            {
                case RuleValueSource.Constant: return Resolution.Known(rule.Constant);
                case RuleValueSource.ManualChoice:
                    return rule.ManualValues.Contains(rule.LastManualValue) ? Resolution.Known(rule.LastManualValue) : Resolution.Unknown("Не выбрано значение перед записью.");
                case RuleValueSource.Level: return RuleEngine.Level(profile, LevelId(ui, profile, e, source, chosenLevel));
                case RuleValueSource.Room:
                    return rule.Source == RuleValueSource.ByGroup && rule.Group == ParameterGroup.Zone
                        ? RoomValue(room, RoomField.Parameter, profile.RoomCorpusParameter) : RoomValue(room, rule.RoomField, rule.RoomParameter);
                case RuleValueSource.ManualCorpus:
                    return profile.Corpora.Contains(source.ManualCorpus) ? Resolution.Known(source.ManualCorpus)
                            : Resolution.Unknown("У элемента не сохранён ручной выбор корпуса. Откройте «Настройки параметров» → «Источники» → «Корпуса для ручного выбора» → «Выбрать корпус».");
                case RuleValueSource.ElementParameter:
                    return read(rule.ElementParameter);
                case RuleValueSource.Mapping:
                    Resolution input;
                    switch (rule.MappingInput)
                    {
                        case MappingInput.ElementLevel: input = Resolution.Known(LevelService.ElementLevel(e)?.Name); break;
                        case MappingInput.ElementParameter: input = read(rule.MappingParameter); break;
                        case MappingInput.RoomNumber: input = RoomValue(room, RoomField.Number, null); break;
                        case MappingInput.RoomName: input = RoomValue(room, RoomField.Name, null); break;
                        case MappingInput.RoomParameter: input = RoomValue(room, RoomField.Parameter, rule.MappingParameter); break;
                        default: return Resolution.Unknown("Источник сопоставления не поддерживается.");
                    }
                    return RuleEngine.Map(rule, input);
                default: return Resolution.Unknown("Неизвестная группа правила.");
            }
        }
        private static Resolution Expected(UIDocument ui, Profile profile, Rule rule, Element e, Provenance source, Room room, string chosenLevel, Func<ParameterRef, Resolution> read)
        {
            return RuleEngine.Evaluate(rule, token => {
                if (token == "value") return SourceValue(ui, profile, rule, e, source, room, chosenLevel, read);
                if (token == "level") return RuleEngine.Level(profile, LevelId(ui, profile, e, source, chosenLevel));
                if (token == "room.number") return RoomValue(room, RoomField.Number, null);
                if (token == "room.name") return RoomValue(room, RoomField.Name, null);
                if (token.StartsWith("param:"))
                {
                    var parameters = e.GetParameters(token.Substring(6));
                    if (parameters.Count != 1) return Resolution.Unknown("Параметр подстановки «" + token.Substring(6) + "» отсутствует или его имя неоднозначно.");
                    var parameter = parameters[0]; return read(new ParameterRef { Name = parameter.Definition.Name, Id = Ids.Value(parameter.Id), SharedGuid = parameter.IsShared ? parameter.GUID.ToString() : null });
                }
                return Resolution.Unknown("Неизвестная подстановка: " + token);
            });
        }
        private static Dictionary<string, Resolution> CalculatedValues(UIDocument ui, Profile profile, List<Rule> rules, Element element, Provenance source, Room room, string chosenLevel)
        {
            return RuleEngine.Calculate(rules, parameter => {
                var value = Catalog.Read(Catalog.Parameter(element, parameter));
                if (!value.Exists || !value.HasValue) return Resolution.Unknown("Исходный параметр «" + parameter?.Name + "» отсутствует или не заполнен.");
                return Resolution.Known(value.Numeric ? value.Number.ToString("G", System.Globalization.CultureInfo.InvariantCulture) : value.Text);
            }, (rule, read) => Expected(ui, profile, rule, element, source, room, chosenLevel, read));
        }
        private static List<Rule> Matching(Element element, IEnumerable<Rule> rules, List<Issue> issues, bool checking = false)
        {
            var matched = new List<Rule>();
            foreach (var rule in rules)
            {
                var match = RuleEngine.Match(rule, Ids.Category(element.Category.Id), p => Catalog.Read(Catalog.Parameter(element, p)));
                if (!match.Success) { if (!checking || rule.Required) issues.Add(Problem(element, rule.Target.Name, match.Error)); }
                else if (match.Value == "true") matched.Add(rule);
            }
            if (matched.Any(r => matched.Any(other => other != r && RuleEngine.SameParameter(r.Target, other.Target))))
            { issues.Add(Problem(element, "Правила", "Несколько правил подходят элементу и записывают один параметр. Отключите дублирующее правило во вкладке «Правила».")); return new List<Rule>(); }
            return matched;
        }
        private static Room StoredRoom(Document doc, Provenance source)
        {
            if (string.IsNullOrEmpty(source.RoomUniqueId)) return null;
            try { return doc.GetElement(source.RoomUniqueId) as Room; }
            catch (Autodesk.Revit.Exceptions.ArgumentException) { return null; }
        }
        private static bool NeedsRoom(Profile p, IEnumerable<Rule> rules)
        {
            return rules.Any(r => RuleEngine.UsesRoom(p, r));
        }
        private static Issue Problem(Element e, string parameter, string reason, Check check = null)
        {
            return new Issue { ElementId = e.Id, Element = e.Category.Name + " · " + Ids.Value(e.Id),
                Parameter = parameter, Reason = reason, Actual = check == null ? null : check.Actual,
                Expected = check == null ? null : check.Expected };
        }
        private static Issue WriteProblem(Element element, Rule rule, Parameter parameter, string expected, Exception exception)
        {
            var issue = Problem(element, Catalog.Describe(rule.Target), exception.Message);
            issue.Expected = expected;
            issue.Technical = exception + "\nЭлемент: " + element.UniqueId + "\nГруппа: " + Ids.Value(element.GroupId);
            try
            {
                issue.Parameter = Catalog.Describe(rule.Target, parameter);
                var actual = Catalog.Read(parameter);
                issue.Actual = !actual.HasValue ? "(пусто)" : actual.Numeric ? actual.Number.ToString("G", System.Globalization.CultureInfo.InvariantCulture) : actual.Text;
            }
            catch (Exception readError) { issue.Technical += "\nНе удалось прочитать параметр после отказа: " + readError.Message; }
            if (element.Document.IsWorkshared)
            {
                try { issue.Technical += "\nВладелец: " + WorksharingUtils.GetWorksharingTooltipInfo(element.Document, element.Id).Owner; }
                catch (Exception infoError) { issue.Technical += "\nНе удалось прочитать владельца: " + infoError.Message; }
            }
            return issue;
        }
        internal static void Fill(UIApplication app, string corpus = null, ICollection<ElementId> captured = null)
        {
            var ui = app.ActiveUIDocument; var doc = ui.Document; var profile = RequireProfile(doc);
            var rules = profile.Rules.Where(r => r.Enabled && (corpus == null || RuleEngine.Source(profile, r) == RuleValueSource.ManualCorpus)).ToList();
            if (corpus != null && !rules.Any()) throw new InvalidOperationException("Не включено правило с источником «Корпус вручную». Выберите этот источник в настройках правила.");
            if (corpus != null && !profile.Corpora.Contains(corpus)) throw new InvalidOperationException("Выбранный корпус отсутствует в списке этой модели. Проверьте названия корпусов в настройках.");
            if (rules.Count == 0) throw new InvalidOperationException("Не включены правила для этой команды.");
            var targets = Targets(ui, profile, true, captured);
            int targetCount = targets.Count;
            var issues = new List<Issue>();
            var perElement = targets.ToDictionary(e => e.Id, e => Matching(e, rules, issues));
            var groupSkips = new GroupSkipSummary();
            foreach (var element in targets.Where(Catalog.IsGrouped))
                perElement[element.Id] = perElement[element.Id].Where(rule => {
                    var parameter = Catalog.Parameter(element, rule.Target);
                    if (parameter == null || Catalog.CanVaryInGroup(parameter)) return true;
                    groupSkips.Add(Catalog.Reference(parameter.Definition, Ids.Value(parameter.Id), parameter.IsShared ? parameter.GUID.ToString() : null));
                    return false;
                }).ToList();
            targets = targets.Where(e => perElement[e.Id].Count > 0 && !issues.Any(i => i.ElementId == e.Id)).ToList();
            if (targets.Count == 0) { issues.AddRange(GroupIssues(groupSkips)); if (issues.Count > 0) UI.ReportWindow.Show(ui, issues, "Правила не применены"); else UI.Toast.Show(app.MainWindowHandle, "Выделенные элементы не подходят под категории и условия правил."); return; }
            var relevantRules = targets.SelectMany(e => perElement[e.Id]).Distinct().ToList();
            bool manualSelection = relevantRules.Any(r => RuleEngine.Source(profile, r) == RuleValueSource.ManualCorpus || RuleEngine.Source(profile, r) == RuleValueSource.ManualChoice);
            if (corpus == null && relevantRules.Any(r => RuleEngine.Source(profile, r) == RuleValueSource.ManualCorpus))
            {
                var choice = new UI.CorpusChoiceWindow(profile.Corpora, profile.LastCorpus);
                new System.Windows.Interop.WindowInteropHelper(choice).Owner = app.MainWindowHandle;
                if (choice.ShowDialog() != true) return;
                corpus = choice.Corpus;
            }
            foreach (var rule in relevantRules.Where(r => RuleEngine.Source(profile, r) == RuleValueSource.ManualChoice))
            {
                var choice = new UI.ModelSourceWindow("Выбрать значение: " + rule.Target.Name,
                    "Значение применяется к подходящим элементам текущего выделения. Последний выбор запоминается в модели.",
                    rule.ManualValues.Select(v => new UI.ModelSourceChoice { Id = v, Name = v }).ToList(), rule.LastManualValue);
                new System.Windows.Interop.WindowInteropHelper(choice).Owner = app.MainWindowHandle;
                if (choice.ShowDialog() != true) return;
                rule.LastManualValue = choice.SelectedId;
            }
            string chosenLevel = ChooseLevel(ui, profile, targets, relevantRules);
            var roomResults = new Dictionary<ElementId, RoomResult>();
            bool needsRoom = NeedsRoom(profile, relevantRules);
            if (needsRoom && profile.RoomSourceMode == RoomSourceMode.ManualPick)
            {
                Room pickedRoom;
                var originalSelection = ui.Selection.GetElementIds().ToList();
                try
                {
                    var phase = UIDocumentContext.From(ui).Phase;
                    if (ui.ActiveView is View3D)
                    {
                        var rooms = new FilteredElementCollector(doc).OfClass(typeof(SpatialElement)).OfType<Room>().Where(r => r.Area > 0 && phase != null && r.get_Parameter(BuiltInParameter.ROOM_PHASE).AsElementId() == phase.Id)
                            .Select(r => new UI.ModelSourceChoice { Id = r.UniqueId, Name = r.Number + " · " + r.get_Parameter(BuiltInParameter.ROOM_NAME).AsString() + " · " + (doc.GetElement(r.LevelId)?.Name ?? "") }).OrderBy(r => r.Name).ToList();
                        if (rooms.Count == 0) throw new InvalidOperationException("В фазе 3D-вида нет размещённых помещений. Проверьте фазу вида и помещения модели.");
                        var picker = new UI.ModelSourceWindow("Выбрать помещение", "На 3D-виде выберите помещение-источник из списка модели.", rooms);
                        new System.Windows.Interop.WindowInteropHelper(picker).Owner = app.MainWindowHandle;
                        if (picker.ShowDialog() != true) throw new System.OperationCanceledException();
                        pickedRoom = (Room)doc.GetElement(picker.SelectedId);
                    }
                    else { var reference = ui.Selection.PickObject(ObjectType.Element, new RoomFilter(), "Выберите помещение-источник для выделенных элементов"); var picked = doc.GetElement(reference); pickedRoom = picked as Room ?? (picked as RoomTag)?.Room; }
                    if (phase == null || pickedRoom.get_Parameter(BuiltInParameter.ROOM_PHASE).AsElementId() != phase.Id)
                        throw new InvalidOperationException("Выбранное помещение относится к другой фазе вида.");
                }
                finally { ui.Selection.SetElementIds(originalSelection); }
                foreach (var target in targets) roomResults[target.Id] = new RoomResult { Room = pickedRoom };
            }
            else
            {
                using (var resolver = needsRoom ? new RoomResolver(UIDocumentContext.From(ui), profile.DoorRoomSide) : null)
                    foreach (var target in targets) roomResults[target.Id] = resolver == null || !NeedsRoom(profile, perElement[target.Id]) ? new RoomResult() : resolver.Resolve(target);
            }
            var applied = rules.ToDictionary(r => r.Id, r => new HashSet<string>());
            var pendingWrites = new List<PendingWrite>();
            int success = 0, written = 0;
            bool committed = false;
            var failures = new RollbackOnError();
            using (var transaction = new Transaction(doc, corpus == null ? "SAB: заполнить параметры" : "SAB: назначить " + corpus))
            {
                transaction.Start();
                var options = transaction.GetFailureHandlingOptions();
                options.SetFailuresPreprocessor(failures);
                options.SetClearAfterRollback(true); transaction.SetFailureHandlingOptions(options);
                try
                {
                    if (profile.AddMissingCategoryBindings)
                        foreach (var rule in relevantRules) BindingService.Ensure(doc, targets.Where(e => perElement[e.Id].Contains(rule)).ToList(), new List<Rule> { rule });
                    foreach (var element in targets)
                    {
                        var source = Catalog.IsGrouped(element) ? new Provenance() : Storage.Source(element);
                        var resolved = roomResults[element.Id];
                        var elementRules = perElement[element.Id];
                        if (resolved.Room != null) source.RoomUniqueId = resolved.Room.UniqueId;
                        if (corpus != null) source.ManualCorpus = corpus;
                        else if (rules.Any(r => RuleEngine.Source(profile, r) == RuleValueSource.ManualCorpus) && string.IsNullOrWhiteSpace(source.ManualCorpus))
                            source.ManualCorpus = ParameterToolsModule.Host.CurrentCorpus(doc);
                        Room room = resolved.Room;
                        if (elementRules.Any(r => RuleEngine.UsesLevel(profile, r))) source.LevelUniqueId = LevelId(ui, profile, element, source, chosenLevel);
                        var calculated = CalculatedValues(ui, profile, elementRules, element, source, room, chosenLevel);
                        var values = elementRules.Select(r => new { Rule = r, Expected = calculated[r.Id] }).ToList();
                        bool elementWritten = false;
                        foreach (var value in values)
                        {
                            var parameter = Catalog.Parameter(element, value.Rule.Target);
                            // Binding expansion may have introduced this parameter since the first check.
                            if (Catalog.IsGrouped(element) && parameter != null && !Catalog.CanVaryInGroup(parameter))
                            {
                                groupSkips.Add(Catalog.Reference(parameter.Definition, Ids.Value(parameter.Id), parameter.IsShared ? parameter.GUID.ToString() : null));
                                continue;
                            }
                            try
                            {
                                if (RuleEngine.UsesRoom(profile, value.Rule) && resolved.Error != null) throw new InvalidOperationException(resolved.Error);
                                if (!value.Expected.Success) throw new InvalidOperationException(value.Expected.Error);
                                Catalog.ValidateWrite(parameter, value.Expected.Value);
                            }
                            catch (Exception ex)
                            {
                                var issue = WriteProblem(element, value.Rule, parameter, value.Expected.Value, ex);
                                if (RuleEngine.UsesRoom(profile, value.Rule) && !string.IsNullOrWhiteSpace(resolved.Technical))
                                    issue.Technical += "\nОпределение помещения:\n" + resolved.Technical;
                                issues.Add(issue); continue;
                            }
                            using (var sub = new SubTransaction(doc))
                            {
                                sub.Start();
                                try
                                {
                                    Catalog.Write(parameter, value.Expected.Value);
                                    if (sub.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Запись этого параметра отменена Revit при завершении подоперации.");
                                    pendingWrites.Add(new PendingWrite { ElementId = element.Id, ElementLabel = element.Category.Name + " · " + Ids.Value(element.Id), Rule = value.Rule, Expected = value.Expected.Value });
                                    elementWritten = true; written++;
                                    applied[value.Rule.Id].Add(value.Expected.Value);
                                }
                                catch (Exception ex)
                                {
                                    if (sub.GetStatus() == TransactionStatus.Started) sub.RollBack();
                                    issues.Add(WriteProblem(element, value.Rule, parameter, value.Expected.Value, ex));
                                }
                            }
                        }
                        if (!elementWritten) continue;
                        success++;
                        // Per-member extensible storage does not vary across group instances.
                        // Filling and checking do not require it for grouped members.
                        if (!Catalog.IsGrouped(element))
                            using (var metadata = new SubTransaction(doc))
                            {
                                metadata.Start();
                                try { Storage.Source(element, source); if (metadata.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Revit отменил сохранение источников."); }
                                catch (Exception ex)
                                {
                                    if (metadata.GetStatus() == TransactionStatus.Started) metadata.RollBack();
                                    issues.Add(Problem(element, "Источники SAB", "Параметры обработаны, но служебные сведения об источниках не сохранены: " + ex.Message));
                                }
                            }
                    }
                    if (manualSelection)
                        using (var remember = new SubTransaction(doc))
                        {
                            remember.Start();
                            try {
                                if (corpus != null) profile.LastCorpus = corpus;
                                Storage.Save(doc, profile);
                                if (remember.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Revit отменил сохранение последнего выбора.");
                            } catch (Exception ex) {
                                if (remember.GetStatus() == TransactionStatus.Started) remember.RollBack();
                                issues.Add(new Issue { Element = "Настройки модели", Parameter = "Последний ручной выбор", Reason = "Последний выбор не запомнен: " + ex.Message, Technical = ex.ToString() });
                            }
                        }
                    committed = transaction.Commit() == TransactionStatus.Committed;
                }
                catch (Exception ex)
                {
                    if (transaction.GetStatus() == TransactionStatus.Started) transaction.RollBack();
                    issues.Add(new Issue { Element = "Операция", Parameter = "Запись параметров / привязки категорий", Reason = ex.Message, Technical = ex.ToString() });
                }
            }
            if (committed)
            {
                // The outer Commit regenerates the model and runs updaters. Only now
                // can final values be trusted; do not retain pre-commit Parameters.
                var verifiedElements = new HashSet<ElementId>();
                written = 0;
                foreach (var values in applied.Values) values.Clear();
                foreach (var pending in pendingWrites)
                {
                    Element element = null;
                    Parameter parameter = null;
                    try
                    {
                        element = doc.GetElement(pending.ElementId);
                        parameter = Catalog.Parameter(element, pending.Rule.Target);
                        var check = RuleEngine.Compare(Catalog.Read(parameter), Resolution.Known(pending.Expected));
                        if (check.Status != CheckStatus.Valid)
                            throw new InvalidOperationException("После завершения транзакции значение не совпадает с правилом. Сейчас: «" + check.Actual + "»; ожидалось: «" + pending.Expected + "». Запись была принята до Commit; итог мог измениться при обработке ограничений или обновляющих модулей Revit. Остальные подтверждённые записи сохранены.");
                        verifiedElements.Add(pending.ElementId);
                        written++;
                        applied[pending.Rule.Id].Add(pending.Expected);
                    }
                    catch (Exception ex)
                    {
                        var issue = element == null
                            ? new Issue { ElementId = pending.ElementId, Element = pending.ElementLabel, Parameter = Catalog.Describe(pending.Rule.Target), Expected = pending.Expected, Reason = "Не удалось проверить запись после Commit: " + ex.Message, Technical = ex.ToString() }
                            : WriteProblem(element, pending.Rule, parameter, pending.Expected, ex);
                        issue.Technical += "\nЭтап: проверка после Transaction.Commit = Committed. Параметр повторно получен по GUID/ID.";
                        issues.Add(issue);
                    }
                }
                success = verifiedElements.Count;
            }
            issues.AddRange(failures.Issues);
            issues.AddRange(GroupIssues(groupSkips));
            if (!committed)
            {
                success = 0; written = 0;
                foreach (var values in applied.Values) values.Clear();
                issues.Insert(0, new Issue { Element = "Операция отменена", Parameter = "Все параметры этой операции",
                    Reason = "Revit отменил транзакцию. Ни одно изменение этой операции не сохранено. Причины Revit и технические подробности приведены ниже; скопируйте отчёт для диагностики.",
                    Technical = "Источник уровня: " + profile.LevelSource + "\nВыбор помещения: " + profile.RoomSourceMode + "\nПравила операции:\n"
                        + string.Join("\n", relevantRules.Select(r => Catalog.Describe(r.Target) + " ← " + RuleEngine.Source(profile, r))) });
            }
            if (committed && corpus != null) ParameterToolsModule.Host.SetCorpus(doc, corpus);
            string summary = (committed ? "Обработано элементов: " : "Операция отменена. Обработано элементов: ") + success + " из " + targetCount + ". Записей параметров: " + written;
            if (groupSkips.Parameters.Count > 0) summary += "\nПропущено параметров в группах: " + groupSkips.Parameters.Count + ". Причины — в отчёте.";
            var writtenNames = rules.Where(r => applied[r.Id].Count > 0).Select(r => r.Target.Name).Distinct().ToList();
            if (writtenNames.Count > 0) summary += "\nЗаполненные параметры:\n" + string.Join("\n", writtenNames);
            UI.Toast.Show(app.MainWindowHandle, summary, 8);
            if (issues.Count > 0) UI.ReportWindow.Show(ui, issues, "Результат заполнения");
        }
        private static IEnumerable<Issue> GroupIssues(GroupSkipSummary summary)
        {
            return summary.Parameters.Select(parameter => new Issue { Element = "Группы · сводка", Parameter = parameter.Display,
                Reason = "Параметр не изменён в группах: значения не могут различаться по экземплярам групп. Остальные доступные параметры обрабатываются. Проверьте свойство «Значения могут различаться по экземплярам групп» в параметрах проекта; SAB не меняет его автоматически." });
        }
        internal static void Check(UIApplication app)
        {
            var ui = app.ActiveUIDocument; var profile = RequireProfile(ui.Document);
            var targets = Targets(ui, profile, false);
            var categories = targets.GroupBy(e => Ids.Category(e.Category.Id)).Select(g => new CategoryChoice {
                Id = g.Key, Name = g.First().Category.Name,
                Selected = profile.CheckCategoryIds.Count > 0 ? profile.CheckCategoryIds.Contains(g.Key) : g.Key == (int)BuiltInCategory.OST_Walls
            }).OrderBy(c => c.Name).ToList();
            var window = new UI.CheckCategoriesWindow(categories);
            new System.Windows.Interop.WindowInteropHelper(window).Owner = app.MainWindowHandle;
            if (window.ShowDialog() != true) return;
            targets = targets.Where(e => window.CategoryIds.Contains(Ids.Category(e.Category.Id))).ToList();
            // Remember only the check scope; filling keeps its own category selection.
            profile.CheckCategoryIds = window.CategoryIds;
            using (var settings = new Transaction(ui.Document, "SAB: категории проверки"))
            { settings.Start(); Storage.Save(ui.Document, profile); if (settings.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Не удалось сохранить категории проверки."); }
            var issues = new List<Issue>();
            var required = profile.Rules.Where(r => r.Enabled && r.Required).ToList();
            foreach (var element in targets)
                foreach (var rule in required.Where(r => r.CategoryIds.Count == 0 || r.CategoryIds.Contains(Ids.Category(element.Category.Id))))
                {
                    var parameter = Catalog.Parameter(element, rule.Target);
                    if (parameter == null) issues.Add(Problem(element, Catalog.Describe(rule.Target), "Параметр с выбранным GUID/ID отсутствует у экземпляра. Проверьте привязку к категории."));
                    else if (!parameter.HasValue || parameter.StorageType == StorageType.String && string.IsNullOrEmpty(parameter.AsString()))
                        issues.Add(Problem(element, Catalog.Describe(rule.Target, parameter), "Обязательный параметр не заполнен."));
                }
            ParameterToolsModule.Host.Highlight.Restore(ui.Document, ui.ActiveView.Id);
            HighlightService.ClearLegacy(ui.Document, ui.ActiveView);
            ViewFilterService.Apply(ui, profile, window.CategoryIds);
            UI.Toast.Show(app.MainWindowHandle, "Проверено: " + targets.Count + ". Проблемных элементов: " + issues.Select(i => i.ElementId).Distinct().Count());
            if (issues.Count > 0) UI.ReportWindow.Show(ui, issues, "Проверка параметров");
        }
        internal static void AssignCorpus(UIApplication app)
        {
            var profile = RequireProfile(app.ActiveUIDocument.Document);
            if (!profile.Rules.Any(r => r.Enabled && RuleEngine.Source(profile, r) == RuleValueSource.ManualCorpus))
                throw new InvalidOperationException("Во вкладке «Правила» выберите источник «Корпус вручную» хотя бы для одного правила.");
            if (app.ActiveUIDocument.Selection.GetElementIds().Count == 0)
                throw new InvalidOperationException("Сначала выделите элементы для назначения корпуса.");
            var window = new UI.CorpusChoiceWindow(profile.Corpora, profile.LastCorpus);
            new System.Windows.Interop.WindowInteropHelper(window).Owner = app.MainWindowHandle;
            if (window.ShowDialog() == true) Fill(app, window.Corpus);
        }

        internal static void Settings(UIApplication app)
        {
            var ui = app.ActiveUIDocument; var profile = Storage.Load(ui.Document);
            {
                var window = new UI.SettingsWindow(ui.Document, profile);
                new System.Windows.Interop.WindowInteropHelper(window).Owner = app.MainWindowHandle;
                window.ShowDialog();
                if (window.Action == UI.SettingsAction.Cancel) return;
                profile = window.Profile;
                profile.Configured = true;
                using (var t = new Transaction(ui.Document, "SAB: настройки параметров"))
                {
                    t.Start(); Storage.Save(ui.Document, profile);
                    if (t.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Настройки не сохранены.");
                }
                UI.Toast.Show(app.MainWindowHandle, "Настройки параметров сохранены");
                if (window.Action == UI.SettingsAction.AssignCorpus) AssignCorpus(app);
                return;
            }
        }
    }
    internal sealed class RollbackOnError : IFailuresPreprocessor
    {
        internal List<Issue> Issues { get; } = new List<Issue>();
        public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
        {
            bool error = false;
            foreach (var failure in accessor.GetFailureMessages())
            {
                var severity = failure.GetSeverity();
                error |= severity == FailureSeverity.Error || severity == FailureSeverity.DocumentCorruption;
                var ids = failure.GetFailingElementIds().ToList();
                var additional = failure.GetAdditionalElementIds();
                string technical = "FailureDefinitionId: " + failure.GetFailureDefinitionId().Guid + "\nУровень: " + severity
                    + "\nID элементов: " + string.Join(", ", ids.Select(Ids.Value)) + "\nСвязанные ID: " + string.Join(", ", additional.Select(Ids.Value));
                string reason = failure.GetDescriptionText();
                if (!Issues.Any(i => i.Reason == reason && i.Technical == technical))
                    Issues.Add(new Issue { Element = "Revit · " + severity, ElementId = ids.Count == 1 ? ids[0] : null,
                        Parameter = "Диагностика транзакции", Reason = reason, Technical = technical });
            }
            return error ? FailureProcessingResult.ProceedWithRollBack : FailureProcessingResult.Continue;
        }
    }
}
