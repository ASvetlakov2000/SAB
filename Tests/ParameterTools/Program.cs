using System;
using System.Linq;
using SAB.ParameterTools.Core;

internal static class Program
{
    private static int _checks;
    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        _checks++; Console.WriteLine("PASS: " + name);
    }
    private static ParameterValue Text(string value) { return new ParameterValue { Exists = true, HasValue = true, Text = value }; }
    private static void Main()
    {
        var zero = new ParameterValue { Exists = true, HasValue = true, Numeric = true, Number = 0 };
        Assert(RuleEngine.Compare(zero, Resolution.Known("0")).Status == CheckStatus.Valid, "Above-ground zero is a filled value");
        Assert(RuleEngine.Compare(zero, Resolution.Known("0,00")).Status == CheckStatus.Valid, "Currency display comma preserves numeric equality");
        Assert(RuleEngine.Compare(new ParameterValue { Exists = true, Numeric = true, Number = 0 }, Resolution.Known("0")).Status == CheckStatus.Missing,
            "Unset numeric parameter differs from a stored zero");
        Assert(RuleEngine.Compare(new ParameterValue(), Resolution.Known("1")).Status == CheckStatus.Unavailable, "Missing category parameter is reported");
        Assert(RuleEngine.Compare(Text("  "), Resolution.Known("Room")).Status == CheckStatus.Missing, "Whitespace is empty");
        Assert(RuleEngine.Compare(Text("G2.1.M1.3"), Resolution.Known("G2.1.M1.3")).Status == CheckStatus.Valid, "Room number remains literal text");
        Assert(RuleEngine.Compare(Text("Old room name"), Resolution.Known("New room name")).Status == CheckStatus.Mismatch, "Renamed source room invalidates stale values");
        Assert(RuleEngine.Compare(Text("Корпус 1"), Resolution.Known("Корпус 2")).Status == CheckStatus.Mismatch, "Room corpus mismatch is an error");
        Assert(RuleEngine.Compare(Text("Filled"), Resolution.Unknown("Deleted room")).Status == CheckStatus.Unresolved, "Deleted source cannot pass with filled values");
        Assert(RuleEngine.Compare(zero, Resolution.Known("text")).Status == CheckStatus.Unresolved, "Incompatible numeric source cannot pass");
        double n;
        Assert(RuleEngine.TryNumber("-1,5", out n) && n == -1.5, "Russian decimal input is parsed");
        Assert(!RuleEngine.TryNumber("NaN", out n) && !RuleEngine.TryNumber("Infinity", out n), "Nonfinite numeric input is rejected");
        Assert(RuleEngine.Compare(new ParameterValue { Exists = true, HasValue = true, Numeric = true, Number = double.NaN }, Resolution.Known("0")).Status == CheckStatus.Mismatch,
            "Invalid stored number cannot pass numeric comparison");
        var profile = new Profile { Configured = true, ZoneSource = ZoneSource.Room, RoomCorpusParameter = new ParameterRef { Id = 20, Name = "Corpus" } };
        Assert(profile.RoomSourceMode == RoomSourceMode.ManualPick, "Manual room picking is the default workflow");
        profile.CategoryIds.Add(-2000011);
        profile.Rules.Add(new Rule { Group = ParameterGroup.Zone, Target = new ParameterRef { Id = 10, Name = "Corpus" } });
        Assert(!RuleEngine.Validate(profile).Any(), "Valid room-source profile");
        profile.Levels.Add(new LevelMapping { LevelUniqueId = "level-a", Value = "1" });
        Assert(RuleEngine.Level(profile, "level-a").Value == "1", "Floor mapping uses level identity");
        Assert(!RuleEngine.Level(profile, "other-level").Success, "Unmapped floor is unresolved");
        profile.Levels.Add(new LevelMapping { LevelUniqueId = "level-a", Value = "2" });
        Assert(!RuleEngine.Level(profile, "level-a").Success && RuleEngine.Validate(profile).Any(), "Duplicate floor mapping is rejected");
        profile.Levels.RemoveAt(1);
        profile.Rules.Add(new Rule { Group = ParameterGroup.Room, Target = new ParameterRef { Id = 10, Name = "Duplicate" } });
        Assert(RuleEngine.Validate(profile).Any(x => x.Contains("нескольких")), "Conflicting writes to one parameter are rejected");
        profile.Rules[1].Enabled = false;
        Assert(!RuleEngine.Validate(profile).Any(), "Disabled rule does not create a conflict");
        profile.RoomCorpusParameter = null;
        Assert(RuleEngine.Validate(profile).Any(x => x.Contains("корпуса")), "Room corpus source must be configured");
        profile.RoomCorpusParameter = new ParameterRef { Id = 20, Name = "Corpus" };
        profile.Corpora = new System.Collections.Generic.List<string> { "Секция А" };
        profile.Rules[0].Source = RuleValueSource.ManualCorpus;
        Assert(!RuleEngine.Validate(profile).Any(), "One manual corpus is supported");
        profile.Corpora.AddRange(new[] { "Секция Б", "Секция В", "Секция Г", "Секция Д" });
        Assert(!RuleEngine.Validate(profile).Any(), "More than three corpora are supported");
        profile.Corpora.Add(" секция а ");
        Assert(RuleEngine.Validate(profile).Any(), "Duplicate corpus names ignore casing and surrounding spaces");
        profile.Corpora.Clear();
        Assert(RuleEngine.Validate(profile).Any(), "Empty corpus list is rejected");
        profile.Rules[0].Source = RuleValueSource.ByGroup;
        RuleEngine.NormalizeRule(profile, profile.Rules[0]);
        Assert(profile.Rules[0].Source == RuleValueSource.Room && profile.Rules[0].RoomParameter.Id == 20, "Legacy room corpus migrates without changing its source");
        Assert(!RuleEngine.Validate(profile).Any(), "Hidden unused manual corpus list does not block saving");
        var advanced = new Rule { EntityName = "Несущие стены", Target = new ParameterRef { Id = 30 }, Source = RuleValueSource.Constant, Constant = "2", Expression = "=({value} + 3) * 2" };
        Assert(RuleEngine.Evaluate(advanced, _ => Resolution.Known("2")).Value == "10", "Arithmetic respects parentheses and priority");
        advanced.Expression = "={value} / 0"; Assert(!RuleEngine.Evaluate(advanced, _ => Resolution.Known("2")).Success, "Division by zero is reported without a write");
        advanced.Expression = "={value} +"; Assert(RuleEngine.ExpressionError(advanced.Expression) != null, "Incomplete arithmetic blocks saving");
        advanced.Expression = "К{value}: {room.number}";
        Assert(RuleEngine.Evaluate(advanced, t => Resolution.Known(t == "value" ? "2" : "G1.03")).Value == "К2: G1.03", "Text templates combine independent sources");
        Assert(RuleEngine.UsesRoom(profile, advanced), "Room token requests room even for a constant source");
        Assert(!RuleEngine.Evaluate(advanced, t => t == "value" ? Resolution.Known("2") : Resolution.Unknown("Deleted room")).Success, "Missing template input prevents partial results");
        advanced.Expression = "{unknown}"; Assert(RuleEngine.ExpressionError(advanced.Expression) != null, "Unknown tokens are rejected");
        advanced.Expression = "={value} * 2e-3"; Assert(RuleEngine.Evaluate(advanced, _ => Resolution.Known("1,5")).Value == "0.003", "Arithmetic supports Russian decimal input and scientific notation");
        advanced.Expression = "{value}"; advanced.CategoryIds.Add(-2000011);
        advanced.Conditions.Add(new RuleCondition { Parameter = new ParameterRef { Id = 40, Name = "Несущая" }, Operator = ConditionOperator.Equals, Value = "1" });
        Assert(RuleEngine.Match(advanced, -2000011, _ => new ParameterValue { Exists = true, HasValue = true, Numeric = true, Number = 1 }).Value == "true", "Rule filters category and numeric value");
        Assert(RuleEngine.Match(advanced, -2000032, _ => Text("1")).Value == "false", "Different category is skipped");
        Assert(!RuleEngine.Match(advanced, -2000011, _ => new ParameterValue()).Success, "Missing condition parameter cannot silently pass");
        advanced.Conditions.Add(new RuleCondition { Parameter = new ParameterRef { Id = 41 }, Operator = ConditionOperator.Contains, Value = "кирпич" });
        advanced.CaseSensitive = false;
        advanced.AnyCondition = true;
        Assert(RuleEngine.Match(advanced, -2000011, p => p.Id == 40 ? new ParameterValue() : Text("КИРПИЧНАЯ")).Value == "true", "OR uses a valid matching branch and ignores casing by default");
        advanced.AnyCondition = false;
        Assert(!RuleEngine.Match(advanced, -2000011, p => p.Id == 40 ? new ParameterValue() : Text("КИРПИЧНАЯ")).Success, "AND retains unresolved conditions");
        advanced.Conditions[0].Operator = ConditionOperator.Filled;
        Assert(RuleEngine.Condition(advanced.Conditions[0], zero, false, 1e-8).Value == "true", "Stored zero counts as filled in a condition");
        Assert(RuleEngine.Compare(Text("КОРПУС"), Resolution.Known("корпус"), 1e-8, false).Status == CheckStatus.Valid, "Rule can ignore text casing during check");
        Assert(RuleEngine.Compare(new ParameterValue { Exists = true, HasValue = true, Numeric = true, Number = 10.05 }, Resolution.Known("10.06"), .1).Status == CheckStatus.Valid, "Configurable numeric tolerance is shared by checking");
        string json = System.Text.Json.JsonSerializer.Serialize(advanced); var restored = System.Text.Json.JsonSerializer.Deserialize<Rule>(json);
        Assert(restored.Conditions.Count == 2 && restored.CategoryIds[0] == -2000011 && restored.EntityName == advanced.EntityName, "Advanced rules retain scope and conditions in JSON");
        var provider = new Rule { Target = new ParameterRef { Id = 80, Name = "A" }, Source = RuleValueSource.Constant, Constant = "2", Required = false };
        var dependent = new Rule { Target = new ParameterRef { Id = 81, Name = "B" }, Source = RuleValueSource.ElementParameter, ElementParameter = provider.Target, Expression = "={value} * 3" };
        var chain = new System.Collections.Generic.List<Rule> { dependent, provider };
        var calculated = RuleEngine.Calculate(chain, _ => Resolution.Known("999"), (r, read) => RuleEngine.Evaluate(r, _ => r.Source == RuleValueSource.Constant ? Resolution.Known(r.Constant) : read(r.ElementParameter)));
        Assert(calculated[dependent.Id].Value == "6", "Dependent rules use calculated values regardless of row order");
        Assert(RuleEngine.ForCheck(chain, profile).Count == 2, "Check retains an unchecked provider needed by a checked dependent");
        dependent.Required = false; Assert(RuleEngine.ForCheck(chain, profile).Count == 0, "Unchecked independent rules do not request sources during check");
        provider.Source = RuleValueSource.ElementParameter; provider.ElementParameter = dependent.Target;
        calculated = RuleEngine.Calculate(chain, _ => Resolution.Known("999"), (r, read) => RuleEngine.Evaluate(r, _ => read(r.ElementParameter)));
        Assert(calculated.Values.All(r => !r.Success && r.Error.Contains("Циклическая")), "Cyclic dependencies fail both rules without writing");
        var duplicateA = new ParameterRef { Id = 1, Name = "Этаж", SharedGuid = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", DataType = "Текст", VariesAcrossGroups = true };
        var duplicateB = new ParameterRef { Id = 2, Name = "Этаж", SharedGuid = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", DataType = "Целое число", VariesAcrossGroups = false };
        Assert(duplicateA.Display.Contains(duplicateA.SharedGuid) && duplicateA.Display.Contains("Текст") && duplicateA.Display.Contains("В группах: изменяется"), "Parameter labels expose GUID, data type and group behavior");
        Assert(duplicateB.Display != duplicateA.Display && !RuleEngine.SameParameter(duplicateA, duplicateB), "Same-name parameters remain distinct by GUID");
        var groupSummary = new GroupSkipSummary();
        for (int i = 0; i < 10000; i++) groupSummary.Add(duplicateA);
        groupSummary.Add(new ParameterRef { Id = 99, Name = "Этаж", SharedGuid = duplicateA.SharedGuid.ToUpperInvariant() });
        groupSummary.Add(duplicateB);
        groupSummary.Add(new ParameterRef { Id = 3, Name = "Этаж", VariesAcrossGroups = false });
        groupSummary.Add(null);
        Assert(groupSummary.Parameters.Count == 3, "Group skip summary deduplicates thousands of members while retaining different GUIDs and nonshared parameters");
        Assert(new ParameterRef { Id = 42, Name = "Local" }.Display.Contains("ID: 42 · без GUID"), "Nonshared parameters show ID and absence of GUID");
        var legacyProfile = System.Text.Json.JsonSerializer.Deserialize<Profile>("{\"SchemaVersion\":1,\"Configured\":true}");
        Assert(legacyProfile.ExpandSelectedGroups && new Profile().RoomSourceMode == RoomSourceMode.ManualPick, "Legacy profiles enable group expansion and preserve manual room default");
        var metadataRoundtrip = System.Text.Json.JsonSerializer.Deserialize<ParameterRef>(System.Text.Json.JsonSerializer.Serialize(duplicateB));
        Assert(metadataRoundtrip.SharedGuid == duplicateB.SharedGuid && metadataRoundtrip.VariesAcrossGroups == false && metadataRoundtrip.DataType == "Целое число", "Parameter identity and metadata survive profile serialization");
        Assert(new Rule().CaseSensitive, "Legacy strict text comparison is preserved by default");
        Assert(RuleEngine.ExpressionError("=10 / 0") != null, "Literal division by zero blocks saving");
        Assert(RuleEngine.ExpressionError("=" + new string('(', 150) + "1" + new string(')', 150)) != null, "Excessive formula nesting is reported");
        var archivedProfile = new Profile { Rules = new System.Collections.Generic.List<Rule> { restored } };
        restored.Expression = "={value} + 2";
        var originalConditions = restored.Conditions;
        RuleEngine.ArchiveAdvancedOptions(archivedProfile);
        Assert(restored.Conditions.Count == 0 && restored.CategoryIds.Count == 0 && restored.Expression == "{value}", "Live workflow does not apply archived formulas or conditions");
        Assert(archivedProfile.ArchivedAdvancedRules.Count == 1 && archivedProfile.ArchivedAdvancedRules[0].Conditions == originalConditions
            && archivedProfile.ArchivedAdvancedRules[0].Expression == "={value} + 2", "Migration retains the original advanced settings for recovery");
        RuleEngine.ArchiveAdvancedOptions(archivedProfile);
        Assert(archivedProfile.ArchivedAdvancedRules.Count == 1, "Repeated profile loading does not duplicate the archive");
        var archiveRoundtrip = System.Text.Json.JsonSerializer.Deserialize<Profile>(System.Text.Json.JsonSerializer.Serialize(archivedProfile));
        Assert(archiveRoundtrip.ArchivedAdvancedRules[0].Conditions.Count == 2 && archiveRoundtrip.Rules[0].Conditions.Count == 0, "Archive and live rules remain separate after JSON roundtrip");
        Console.WriteLine("All " + _checks + " checks passed.");
    }
}
