using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SAB.ParameterTools.Core
{
    public static class RuleEngine
    {
        public static void ArchiveAdvancedOptions(Profile profile)
        {
            foreach (var rule in profile.Rules)
            {
                if (rule.Conditions.Count > 0 || rule.CategoryIds.Count > 0 || rule.Expression != "{value}"
                    || rule.AnyCondition || !rule.CaseSensitive || rule.Tolerance != 1e-8)
                {
                    profile.ArchivedAdvancedRules.Add(new Rule { Id = rule.Id, Target = rule.Target,
                        Enabled = rule.Enabled, Required = rule.Required, Group = rule.Group, Source = rule.Source,
                        Constant = rule.Constant, EntityName = rule.EntityName, RoomField = rule.RoomField,
                        RoomParameter = rule.RoomParameter, ElementParameter = rule.ElementParameter,
                        Conditions = rule.Conditions, CategoryIds = rule.CategoryIds, Expression = rule.Expression,
                        AnyCondition = rule.AnyCondition, CaseSensitive = rule.CaseSensitive, Tolerance = rule.Tolerance });
                    rule.Conditions = new List<RuleCondition>(); rule.CategoryIds = new List<int>();
                    rule.Expression = "{value}"; rule.AnyCondition = false; rule.CaseSensitive = true; rule.Tolerance = 1e-8;
                }
            }
        }

        public static RuleValueSource Source(Profile profile, Rule rule)
        {
            if (rule.Source != RuleValueSource.ByGroup) return rule.Source;
            switch (rule.Group)
            {
                case ParameterGroup.Zone: return profile.ZoneSource == ZoneSource.Manual ? RuleValueSource.ManualCorpus : RuleValueSource.Room;
                case ParameterGroup.Level: return RuleValueSource.Level;
                case ParameterGroup.Location: return RuleValueSource.Constant;
                default: return RuleValueSource.Room;
            }
        }
        public static void NormalizeRule(Profile profile, Rule rule)
        {
            if (string.IsNullOrWhiteSpace(rule.EntityName)) rule.EntityName = Entity(rule.Group);
            if (rule.Source != RuleValueSource.ByGroup) return;
            if (rule.Group == ParameterGroup.Zone && profile.ZoneSource == ZoneSource.Room)
            { rule.RoomField = RoomField.Parameter; rule.RoomParameter = profile.RoomCorpusParameter; }
            rule.Source = Source(profile, rule);
        }
        public static string Entity(ParameterGroup group)
        { return group == ParameterGroup.Zone ? "Зона" : group == ParameterGroup.Level ? "Уровень" : group == ParameterGroup.Location ? "Местоположение" : "Помещение"; }
        public static bool SameParameter(ParameterRef a, ParameterRef b)
        { return a != null && b != null && (!string.IsNullOrEmpty(a.SharedGuid) && !string.IsNullOrEmpty(b.SharedGuid) ? string.Equals(a.SharedGuid, b.SharedGuid, StringComparison.OrdinalIgnoreCase) : a.Id == b.Id); }
        public static bool UsesRoom(Profile p, Rule r)
        { return Source(p, r) == RuleValueSource.Room || System.Text.RegularExpressions.Regex.IsMatch(r.Expression ?? "", @"\{room\.(number|name)\}"); }
        public static bool UsesLevel(Profile p, Rule r)
        { return Source(p, r) == RuleValueSource.Level || (r.Expression ?? "").Contains("{level}"); }
        public static bool TryNumber(string text, out double number)
        {
            return double.TryParse((text ?? "").Trim().Replace(',', '.'), NumberStyles.Float,
                CultureInfo.InvariantCulture, out number) && !double.IsNaN(number) && !double.IsInfinity(number);
        }
        public static Check Compare(ParameterValue actual, Resolution expected, double tolerance = 1e-8, bool caseSensitive = true)
        {
            var check = new Check { Expected = expected.Value, Actual = actual.Numeric
                ? actual.Number.ToString("G", CultureInfo.InvariantCulture) : actual.Text };
            if (!actual.Exists) { check.Status = CheckStatus.Unavailable; check.Reason = "Параметр отсутствует у экземпляра."; }
            else if (!actual.HasValue || (!actual.Numeric && string.IsNullOrWhiteSpace(actual.Text)))
            { check.Status = CheckStatus.Missing; check.Reason = "Обязательный параметр не заполнен."; }
            else if (!expected.Success)
            { check.Status = CheckStatus.Unresolved; check.Reason = expected.Error; }
            else if (actual.Numeric)
            {
                double n;
                if (double.IsNaN(actual.Number) || double.IsInfinity(actual.Number))
                { check.Status = CheckStatus.Mismatch; check.Reason = "Числовое значение некорректно."; }
                else if (!TryNumber(expected.Value, out n))
                { check.Status = CheckStatus.Unresolved; check.Reason = "Ожидаемое значение не является числом."; }
                else if (Math.Abs(actual.Number - n) > tolerance)
                { check.Status = CheckStatus.Mismatch; check.Reason = "Значение отличается от правила."; }
            }
            else if (!string.Equals((actual.Text ?? "").Trim(), expected.Value.Trim(), caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
            { check.Status = CheckStatus.Mismatch; check.Reason = "Значение отличается от правила."; }
            return check;
        }
        public static Resolution Level(Profile profile, string uniqueId)
        {
            var rows = profile.Levels.Where(x => x.LevelUniqueId == uniqueId).ToList();
            return rows.Count == 1 ? Resolution.Known(rows[0].Value)
                : Resolution.Unknown("Для уровня не задано однозначное соответствие номеру этажа.");
        }
        public static IEnumerable<string> Validate(Profile p)
        {
            if (p.SchemaVersion != 1) yield return "Версия профиля не поддерживается.";
            if (!Enum.IsDefined(typeof(ZoneSource), p.ZoneSource)) yield return "Источник зоны пока не поддерживается.";
            if (!Enum.IsDefined(typeof(LevelSource), p.LevelSource)) yield return "Источник уровня не поддерживается.";
            if (!Enum.IsDefined(typeof(RoomSourceMode), p.RoomSourceMode)) yield return "Способ определения помещения не поддерживается.";
            var rules = p.Rules.Where(r => r.Enabled).ToList();
            if (rules.Any(r => string.IsNullOrWhiteSpace(r.Id)) || rules.GroupBy(r => r.Id).Any(g => g.Count() > 1)) yield return "Идентификаторы правил повреждены или повторяются. Удалите и заново добавьте проблемные строки.";
            if (rules.Any(r => Source(p, r) == RuleValueSource.ManualCorpus) && (p.Corpora.Count == 0 || p.Corpora.Any(string.IsNullOrWhiteSpace) ||
                p.Corpora.Select(c => c.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != p.Corpora.Count))
                yield return "Задайте хотя бы одно название корпуса. Названия должны быть непустыми и различаться.";
            if (p.CategoryIds.Count == 0) yield return "Выберите категории элементов.";
            if (rules.Count == 0) yield return "Включите хотя бы одно правило.";
            foreach (var r in rules)
            {
                if (r.Tolerance < 0 || double.IsNaN(r.Tolerance) || double.IsInfinity(r.Tolerance)) yield return "Допуск должен быть конечным неотрицательным числом.";
                var expressionError = ExpressionError(r.Expression); if (expressionError != null) yield return expressionError;
                if (r.Conditions.Any(c => c.Parameter == null || !Enum.IsDefined(typeof(ConditionOperator), c.Operator) ||
                    c.Operator != ConditionOperator.Empty && c.Operator != ConditionOperator.Filled && string.IsNullOrWhiteSpace(c.Value)))
                    yield return "В условиях выберите параметр, сравнение и значение.";
                foreach (var condition in r.Conditions.Where(c => c.Operator >= ConditionOperator.Greater && c.Operator <= ConditionOperator.LessOrEqual)) { double number; if (!TryNumber(condition.Value, out number)) yield return "Для условия с числовым сравнением введите число."; }
                if (r.Target == null) yield return "Не выбран параметр получателя.";
                if (!Enum.IsDefined(typeof(ParameterGroup), r.Group)) yield return "Группа правила не поддерживается.";
                if (!Enum.IsDefined(typeof(RuleValueSource), r.Source)) yield return "Источник значения не поддерживается.";
                if (Source(p, r) == RuleValueSource.Constant && string.IsNullOrWhiteSpace(r.Constant)) yield return "Не задано значение для ручного заполнения.";
                if (Source(p, r) == RuleValueSource.Room && !Enum.IsDefined(typeof(RoomField), r.RoomField)) yield return "Поле помещения не поддерживается.";
                if (Source(p, r) == RuleValueSource.Room && r.RoomField == RoomField.Parameter && r.RoomParameter == null && !(r.Source == RuleValueSource.ByGroup && r.Group == ParameterGroup.Zone))
                    yield return "Не выбран параметр источника помещения.";
                if (Source(p, r) == RuleValueSource.ElementParameter && r.ElementParameter == null) yield return "Не выбран исходный параметр элемента.";
                if (Source(p, r) == RuleValueSource.ElementParameter && r.Target != null && r.ElementParameter != null &&
                    SameParameter(r.Target, r.ElementParameter))
                    yield return "Исходный и заполняемый параметры должны различаться.";
            }
            if (rules.Where(r => r.Target != null && r.Conditions.Count == 0 && r.CategoryIds.Count == 0).GroupBy(r => string.IsNullOrEmpty(r.Target.SharedGuid)
                ? "id:" + r.Target.Id : "guid:" + r.Target.SharedGuid).Any(g => g.Count() > 1))
                yield return "Один параметр получателя используется в нескольких включённых правилах.";
            if (rules.Any(r => r.Source == RuleValueSource.ByGroup && r.Group == ParameterGroup.Zone) && p.ZoneSource == ZoneSource.Room && p.RoomCorpusParameter == null)
                yield return "Не выбран параметр корпуса у помещения.";
            if (p.Levels.GroupBy(l => l.LevelUniqueId).Any(g => g.Count() > 1))
                yield return "Один уровень указан в матрице несколько раз.";
        }

        public static Resolution Match(Rule rule, int category, Func<ParameterRef, ParameterValue> read)
        {
            if (rule.CategoryIds.Count > 0 && !rule.CategoryIds.Contains(category)) return Resolution.Known("false");
            var results = rule.Conditions.Select(c => Condition(c, read(c.Parameter), rule.CaseSensitive, rule.Tolerance)).ToList();
            if (results.Count == 0) return Resolution.Known("true");
            if (rule.AnyCondition && results.Any(r => r.Success && r.Value == "true")) return Resolution.Known("true");
            if (!rule.AnyCondition && results.Any(r => r.Success && r.Value == "false")) return Resolution.Known("false");
            var unknown = results.FirstOrDefault(r => !r.Success); if (unknown != null) return unknown;
            return Resolution.Known(rule.AnyCondition ? "false" : "true");
        }
        public static Resolution Condition(RuleCondition condition, ParameterValue value, bool caseSensitive, double tolerance)
        {
            if (!value.Exists) return Resolution.Unknown("Параметр условия «" + condition.Parameter?.Name + "» отсутствует у элемента.");
            bool empty = !value.HasValue || !value.Numeric && string.IsNullOrWhiteSpace(value.Text);
            if (condition.Operator == ConditionOperator.Empty) return Resolution.Known(empty ? "true" : "false");
            if (condition.Operator == ConditionOperator.Filled) return Resolution.Known(empty ? "false" : "true");
            if (empty) return Resolution.Unknown("Параметр условия «" + condition.Parameter?.Name + "» не заполнен.");
            string text = value.Numeric ? value.Number.ToString("G", CultureInfo.InvariantCulture) : value.Text ?? "";
            string expected = condition.Value ?? ""; double a = 0, b = 0; bool numeric = TryNumber(text, out a) && TryNumber(expected, out b);
            var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            bool equal = value.Numeric && numeric ? Math.Abs(a - b) <= tolerance : string.Equals(text.Trim(), expected.Trim(), comparison);
            bool match;
            switch (condition.Operator)
            {
                case ConditionOperator.Equals: match = equal; break;
                case ConditionOperator.NotEquals: match = !equal; break;
                case ConditionOperator.Contains: match = text.IndexOf(expected, comparison) >= 0; break;
                case ConditionOperator.StartsWith: match = text.StartsWith(expected, comparison); break;
                default:
                    if (!numeric) return Resolution.Unknown("Для числового условия нужны числа в параметре и в правиле.");
                    switch (condition.Operator)
                    {
                        case ConditionOperator.Greater: match = a > b + tolerance; break;
                        case ConditionOperator.GreaterOrEqual: match = a >= b - tolerance; break;
                        case ConditionOperator.Less: match = a < b - tolerance; break;
                        case ConditionOperator.LessOrEqual: match = a <= b + tolerance; break;
                        default: return Resolution.Unknown("Неизвестное сравнение в условии.");
                    } break;
            }
            return Resolution.Known(match ? "true" : "false");
        }
        public static string ExpressionError(string expression)
        {
            if (string.IsNullOrWhiteSpace(expression)) return "Введите шаблон значения: {value} для исходного значения.";
            var rest = System.Text.RegularExpressions.Regex.Replace(expression, @"\{(value|level|room\.number|room\.name|param:[^{}]+)\}", "1");
            if (rest.Contains("{") || rest.Contains("}")) return "Неизвестная подстановка. Используйте {value}, {level}, {room.number}, {room.name} или {param:Имя параметра}.";
            if (expression.Length > 4096) return "Сократите формулу или шаблон до 4096 символов.";
            if (expression.TrimStart().StartsWith("=")) { try { new Arithmetic(rest.TrimStart().Substring(1)).Evaluate(); } catch (FormatException e) { return e.Message; } catch (InvalidOperationException e) { if (!expression.Contains("{")) return e.Message; } }
            return null;
        }
        public static Resolution Evaluate(Rule rule, Func<string, Resolution> token)
        {
            string error = ExpressionError(rule.Expression); if (error != null) return Resolution.Unknown(error);
            string failure = null;
            string result = System.Text.RegularExpressions.Regex.Replace(rule.Expression, @"\{([^{}]+)\}", m => {
                var resolved = token(m.Groups[1].Value); if (!resolved.Success) { failure = resolved.Error; return ""; } return resolved.Value;
            });
            if (failure != null) return Resolution.Unknown(failure);
            if (rule.Expression.TrimStart().StartsWith("="))
            {
                try { result = new Arithmetic(result.TrimStart().Substring(1)).Evaluate().ToString("G", CultureInfo.InvariantCulture); }
                catch (Exception e) when (e is FormatException || e is InvalidOperationException) { return Resolution.Unknown(e.Message); }
            }
            return Resolution.Known(result);
        }
        public static Dictionary<string, Resolution> Calculate(IList<Rule> rules, Func<ParameterRef, Resolution> read, Func<Rule, Func<ParameterRef, Resolution>, Resolution> evaluate)
        {
            var cache = new Dictionary<string, Resolution>(); var visiting = new HashSet<string>();
            Func<Rule, Resolution> resolve = null;
            resolve = rule => {
                Resolution result; if (cache.TryGetValue(rule.Id, out result)) return result;
                if (!visiting.Add(rule.Id)) return Resolution.Unknown("Циклическая зависимость правил для параметра «" + rule.Target?.Name + "». Измените источник или формулу.");
                result = evaluate(rule, parameter => { var provider = rules.FirstOrDefault(r => SameParameter(r.Target, parameter)); return provider == null ? read(parameter) : resolve(provider); });
                visiting.Remove(rule.Id); cache[rule.Id] = result; return result;
            };
            foreach (var rule in rules) resolve(rule); return cache;
        }
        public static List<Rule> ForCheck(IList<Rule> rules, Profile profile)
        {
            var needed = new HashSet<Rule>(rules.Where(r => r.Required)); bool changed;
            do {
                changed = false;
                foreach (var rule in needed.ToList())
                {
                    foreach (var other in rules.Where(r =>
                        Source(profile, rule) == RuleValueSource.ElementParameter && (rule.Expression ?? "").Contains("{value}") && SameParameter(r.Target, rule.ElementParameter) ||
                        !string.IsNullOrWhiteSpace(r.Target?.Name) && (rule.Expression ?? "").Contains("{param:" + r.Target.Name + "}"))) changed |= needed.Add(other);
                }
            } while (changed);
            return rules.Where(needed.Contains).ToList();
        }
        // A deliberately restricted parser: arithmetic only, without executable code or script evaluation.
        private sealed class Arithmetic
        {
            private readonly string _text; private int _position, _depth;
            internal Arithmetic(string text) { _text = text.Replace(',', '.'); }
            private void Space() { while (_position < _text.Length && char.IsWhiteSpace(_text[_position])) _position++; }
            private bool Take(char c) { Space(); if (_position >= _text.Length || _text[_position] != c) return false; _position++; return true; }
            internal double Evaluate() { double value = Sum(); Space(); if (_position != _text.Length) throw new FormatException("В формуле разрешены числа, подстановки, + − * / и круглые скобки."); if (double.IsNaN(value) || double.IsInfinity(value)) throw new InvalidOperationException("Результат формулы не является конечным числом."); return value; }
            private double Sum() { double value = Product(); while (true) { if (Take('+')) value += Product(); else if (Take('-')) value -= Product(); else return value; } }
            private double Product() { double value = Atom(); while (true) { if (Take('*')) value *= Atom(); else if (Take('/')) { double divisor = Atom(); if (divisor == 0) throw new InvalidOperationException("Деление на ноль в формуле."); value /= divisor; } else return value; } }
            private double Atom()
            {
                if (++_depth > 128) throw new FormatException("Слишком много вложенных скобок или знаков в формуле.");
                try { return AtomValue(); } finally { _depth--; }
            }
            private double AtomValue()
            {
                if (Take('+')) return Atom(); if (Take('-')) return -Atom(); if (Take('(')) { double value = Sum(); if (!Take(')')) throw new FormatException("Закройте круглую скобку в формуле."); return value; }
                Space(); int start = _position;
                while (_position < _text.Length && (char.IsDigit(_text[_position]) || _text[_position] == '.')) _position++;
                if (_position < _text.Length && (_text[_position] == 'e' || _text[_position] == 'E')) { _position++; if (_position < _text.Length && (_text[_position] == '+' || _text[_position] == '-')) _position++; while (_position < _text.Length && char.IsDigit(_text[_position])) _position++; }
                double number; if (!TryNumber(_text.Substring(start, _position - start), out number)) throw new FormatException("В формуле ожидается число или числовой параметр."); return number;
            }
        }
    }
}
