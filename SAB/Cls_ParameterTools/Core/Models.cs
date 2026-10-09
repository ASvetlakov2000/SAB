using System;
using System.Collections.Generic;

namespace SAB.ParameterTools.Core
{
    public enum ParameterGroup { Zone, Level, Location, Room }
    public enum ZoneSource { Manual = 0, Room = 2 }
    public enum RoomField { Number, Name, Parameter }
    public enum LevelSource { ActivePlan, Element }
    public enum RoomSourceMode { ManualPick, Automatic }
    public enum RuleValueSource { ByGroup, Room, Level, Constant, ManualCorpus, ElementParameter }
    public enum CheckStatus { Valid, Missing, Mismatch, Unresolved, Unavailable }
    public enum ConditionOperator { Equals, NotEquals, Contains, StartsWith, Greater, GreaterOrEqual, Less, LessOrEqual, Empty, Filled }
    public sealed class RuleCondition
    {
        public ParameterRef Parameter { get; set; }
        public ConditionOperator Operator { get; set; }
        public string Value { get; set; }
    }

    public sealed class ParameterRef
    {
        public string Name { get; set; }
        public string SharedGuid { get; set; }
        public long Id { get; set; }
        public string DataType { get; set; }
        public bool? VariesAcrossGroups { get; set; }
        public string Identity { get { return string.IsNullOrWhiteSpace(SharedGuid) ? "ID: " + Id + " · без GUID" : "GUID: " + SharedGuid; } }
        public string GroupBehavior { get { return VariesAcrossGroups == true ? "В группах: изменяется" : VariesAcrossGroups == false ? "В группах: не изменяется" : "В группах: не определено"; } }
        public string Details { get { return (DataType ?? "Тип данных не определён") + " · " + GroupBehavior + "\n" + Identity; } }
        public string Display { get { return Name + "\n" + Details; } }
        public override string ToString() { return Display; }
    }
    public sealed class Rule : System.ComponentModel.INotifyPropertyChanged
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        private bool _enabled = true, _required = true;
        private ParameterGroup _group;
        private ParameterRef _target;
        private RuleValueSource _source;
        public bool Enabled { get { return _enabled; } set { if (_enabled == value) return; _enabled = value; Changed("Enabled"); } }
        public bool Required { get { return _required; } set { if (_required == value) return; _required = value; Changed("Required"); } }
        public ParameterGroup Group { get { return _group; } set { if (_group == value) return; _group = value; Changed("Group"); } }
        public ParameterRef Target { get { return _target; } set { if (ReferenceEquals(_target, value)) return; _target = value; Changed("Target"); } }
        public RuleValueSource Source { get { return _source; } set { if (_source == value) return; _source = value; Changed("Source"); } }
        public string Constant { get; set; } = "0";
        private string _entityName;
        public string EntityName { get { return _entityName; } set { if (_entityName == value) return; _entityName = value; Changed("EntityName"); } }
        public string Expression { get; set; } = "{value}";
        public bool AnyCondition { get; set; }
        public bool CaseSensitive { get; set; } = true;
        public double Tolerance { get; set; } = 1e-8;
        public List<int> CategoryIds { get; set; } = new List<int>();
        public List<RuleCondition> Conditions { get; set; } = new List<RuleCondition>();
        public RoomField RoomField { get; set; }
        public ParameterRef RoomParameter { get; set; }
        public ParameterRef ElementParameter { get; set; }
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        private void Changed(string name) { PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name)); }
        public override string ToString() { return (EntityName ?? RuleEngine.Entity(Group)) + " → " + (Target?.Name ?? "параметр не выбран"); }
    }
    public sealed class GroupSkipSummary
    {
        private readonly List<ParameterRef> _parameters = new List<ParameterRef>();
        public IReadOnlyList<ParameterRef> Parameters { get { return _parameters; } }
        public void Add(ParameterRef parameter)
        {
            if (parameter != null && !_parameters.Exists(p => RuleEngine.SameParameter(p, parameter))) _parameters.Add(parameter);
        }
    }
    public sealed class LevelMapping
    {
        public string LevelUniqueId { get; set; }
        public string LevelName { get; set; }
        public string Value { get; set; }
    }
    public sealed class Profile
    {
        public int SchemaVersion { get; set; } = 1;
        public bool Configured { get; set; }
        public bool AddMissingCategoryBindings { get; set; }
        public bool ExpandSelectedGroups { get; set; } = true;
        public ZoneSource ZoneSource { get; set; } = ZoneSource.Room;
        public LevelSource LevelSource { get; set; } = LevelSource.ActivePlan;
        public RoomSourceMode RoomSourceMode { get; set; } = RoomSourceMode.ManualPick;
        public ParameterRef RoomCorpusParameter { get; set; }
        public List<string> Corpora { get; set; } = new List<string> { "Корпус 1", "Корпус 2", "Корпус 3" };
        public List<Rule> Rules { get; set; } = new List<Rule>();
        // Legacy advanced settings are retained for recovery, never applied by the active workflow.
        public List<Rule> ArchivedAdvancedRules { get; set; } = new List<Rule>();
        public List<LevelMapping> Levels { get; set; } = new List<LevelMapping>();
        public List<int> CategoryIds { get; set; } = new List<int>();
        public List<int> CheckCategoryIds { get; set; } = new List<int>();
    }
    public sealed class Provenance
    {
        public string RoomUniqueId { get; set; }
        public string ManualCorpus { get; set; }
        public string LevelUniqueId { get; set; }
    }
    public sealed class Resolution
    {
        public string Value { get; private set; }
        public string Error { get; private set; }
        public bool Success { get { return Error == null; } }
        public static Resolution Known(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? Unknown("Источник содержит пустое значение.") : new Resolution { Value = value };
        }
        public static Resolution Unknown(string error) { return new Resolution { Error = error }; }
    }
    public sealed class ParameterValue
    {
        public bool Exists { get; set; }
        public bool HasValue { get; set; }
        public bool Numeric { get; set; }
        public double Number { get; set; }
        public string Text { get; set; }
    }
    public sealed class Check
    {
        public CheckStatus Status { get; set; }
        public string Actual { get; set; }
        public string Expected { get; set; }
        public string Reason { get; set; }
    }
}
