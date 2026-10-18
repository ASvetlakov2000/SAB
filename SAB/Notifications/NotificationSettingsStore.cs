using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace SAB.Notifications
{
    public sealed class RuleSetting
    {
        public string Action = "Show";
        public bool Consent;
    }

    public sealed class Settings
    {
        // Missing/legacy schema must never authorize blanket suppression.
        public int SchemaVersion;
        public bool Enabled;
        public bool FamilyException = true, GroupException = true, WorksharedException = true;
        public int ToastSeconds = 10;
        public string JournalFolder;
        public Dictionary<string, RuleSetting> Rules = new Dictionary<string, RuleSetting>();
    }

    public sealed class NotificationSettingsStore
    {
        readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        public string Home { get; }
        public string LoadNotice { get; private set; }
        public bool LoadFailed { get; private set; }
        public NotificationSettingsStore(string home) { Home = Path.GetFullPath(home); }
        public Settings Load()
        {
            LoadNotice = "";
            LoadFailed = false;
            var path = Path.Combine(Home, "settings.json");
            if (!File.Exists(path)) return Normalize(new Settings());
            try
            {
                var value = json.Deserialize<Settings>(File.ReadAllText(path));
                if (value == null || value.SchemaVersion != 2)
                {
                    LoadNotice = "Прежние общие уровни отключены. Выберите отдельные правила.";
                    return Normalize(new Settings());
                }
                return Normalize(value);
            }
            catch (Exception)
            {
                LoadFailed = true;
                LoadNotice = "Не удалось прочитать настройки. Подавление выключено; исходный файл сохранён.";
                return Normalize(new Settings());
            }
        }
        public Settings Clone(Settings value) => Normalize(json.Deserialize<Settings>(json.Serialize(value)));
        public Settings Normalize(Settings value)
        {
            value.SchemaVersion = 2;
            value.Rules = value.Rules ?? new Dictionary<string, RuleSetting>();
            if (value.ToastSeconds < 3 || value.ToastSeconds > 60) value.ToastSeconds = 10;
            if (string.IsNullOrWhiteSpace(value.JournalFolder)) value.JournalFolder = Path.Combine(Home, "Journal");
            return value;
        }
        public void Save(Settings value)
        {
            var copy = Clone(value);
            Directory.CreateDirectory(Home);
            string path = Path.Combine(Home, "settings.json"), temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var bytes = new UTF8Encoding(false).GetBytes(json.Serialize(copy));
                    stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
                }
                if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        public List<LogRow> ReadJournal()
        {
            var rows = new List<LogRow>();
            string path = Path.Combine(Home, "spool.jsonl");
            if (!File.Exists(path)) return rows;
            // Bound memory even for old spools; read tail with sharing while Revit records events.
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                bool tail = stream.Length > 4 * 1024 * 1024;
                if (tail) stream.Seek(-4 * 1024 * 1024, SeekOrigin.End);
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    if (tail) reader.ReadLine();
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        try
                        {
                            var item = json.Deserialize<Dictionary<string, object>>(line);
                            if (item == null) continue;
                            Func<string, string> field = key => item.TryGetValue(key, out var v) ? Convert.ToString(v) : "";
                            rows.Add(new LogRow { Time = field("Time"), Name = field("Text"), Action = field("Action"), Code = field("Failure"), Model = field("Model"), Ids = field("Ids") });
                        }
                        catch (ArgumentException) { /* Ignore only malformed historical records. */ }
                        catch (InvalidOperationException) { }
                    }
                }
            }
            return rows.GroupBy(r => r.Time + "\n" + r.Code + "\n" + r.Model + "\n" + r.Name).Select(g => g.Last()).Reverse().Take(500).ToList();
        }
    }
}
