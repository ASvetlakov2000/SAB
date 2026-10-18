using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;

namespace SAB.Notifications
{
    public class Entry
    {
        public string Time = DateTimeOffset.Now.ToString("o");
        public string Model, Transaction, Failure, Severity, Text, Ids, Action, LogPath, HtmlLogPath;
    }

    // First-stage settings integration: observation never changes failure processing.
    public class NotificationController
    {
        internal static NotificationController Current;
        internal static readonly string Home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RevitQuiet");
        internal Settings Config;
        internal NotificationSettingsStore Store;
        internal bool Enabled;
        readonly JavaScriptSerializer json = new JavaScriptSerializer();
        readonly HashSet<string> observed = new HashSet<string>();
        string journalPath;
        internal string JournalError;
        public Result Start(UIControlledApplication app, RibbonPanel settingsPanel)
        {
            Current = this;
            Store = new NotificationSettingsStore(Home); Config = Store.Load(); Enabled = Config.Enabled;
            journalPath = app.ControlledApplication.RecordingJournalFilename;
            SAB.Helpers.Ribbon.AddPushButtonSingle(settingsPanel, "QuietSettings", "Уведомления", typeof(Options).FullName, "SAB.Resources.Notifications_32.png", "SAB.Resources.Notifications_16.png");
            app.ControlledApplication.FailuresProcessing += Failures;
            app.DialogBoxShowing += Dialog; app.Idling += Idle;
            return Result.Succeeded;
        }
        public Result Stop(UIControlledApplication app)
        {
            app.ControlledApplication.FailuresProcessing -= Failures;
            app.DialogBoxShowing -= Dialog; app.Idling -= Idle;
            observed.Clear(); Current = null; return Result.Succeeded;
        }
        internal void Apply(Settings settings) { Config = settings; Enabled = settings.Enabled; JournalError = null; }
        static string Model(Document doc) => doc == null ? "Без документа" : string.IsNullOrEmpty(doc.PathName) ? doc.Title : doc.PathName;
        void Failures(object sender, FailuresProcessingEventArgs e)
        {
            if (!Enabled) return;
            try
            {
                var accessor = e.GetFailuresAccessor();
                foreach (var failure in accessor.GetFailureMessages())
                    Record(new Entry {
                        Model = Model(accessor.GetDocument()), Transaction = accessor.GetTransactionName(),
                        Failure = failure.GetFailureDefinitionId().Guid.ToString(),
                        Severity = failure.GetSeverity().ToString(), Text = failure.GetDescriptionText(),
                        Ids = string.Join(",", failure.GetFailingElementIds().Concat(failure.GetAdditionalElementIds()).Select(id => id.ToString()).Distinct()),
                        Action = "Не подавлено; передано штатному обработчику Revit"
                    });
                // Do not set processing results: preserve native UI and other add-ins' results.
                // A failure GUID proves neither an OK button nor a safe automatic answer.
            }
            catch (Exception ex) { JournalError = "Не удалось записать сообщение: " + ex.Message; }
        }
        void Dialog(object sender, DialogBoxShowingEventArgs e)
        {
            if (!Enabled) return;
            try
            {
                string text = (e as TaskDialogShowingEventArgs)?.Message ?? (e as MessageBoxShowingEventArgs)?.Message;
                if (text == null && e.DialogId == "Dialog_Revit_DocWarnDialog") text = DeleteWarningJournal.Read(journalPath);
                Record(new Entry {
                    Model = "", Failure = e.DialogId, Severity = e.GetType().Name,
                    Text = text ?? "Revit API не предоставляет текст этого диалога",
                    Action = "Не подавлено; автоматическое подтверждение не проверено"
                });
                // No automatic result, native button posting or model resolution at this stage.
            }
            catch (Exception ex) { JournalError = "Не удалось записать диалог: " + ex.Message; }
        }
        void Record(Entry entry)
        {
            string key = entry.Model + "\n" + entry.Transaction + "\n" + entry.Failure + "\n" + entry.Text + "\n" + entry.Ids;
            if (observed.Contains(key)) return;
            Directory.CreateDirectory(Home);
            entry.LogPath = Path.Combine(Home, "spool.jsonl");
            File.AppendAllText(entry.LogPath, json.Serialize(entry) + Environment.NewLine);
            observed.Add(key);
            if (observed.Count > 1000) observed.Clear();
        }
        void Idle(object sender, IdlingEventArgs e) { observed.Clear(); }
        internal SettingsWindow CreateWindow()
        {
            var window = new SettingsWindow(Store, Store.Clone(Config), Apply);
            if (!string.IsNullOrEmpty(JournalError)) window.Model.SetRuntimeError(JournalError);
            return window;
        }
        internal string GetJournal(UIApplication ui) => new SettingsModel(Store, Config).ExportJournal();
        internal static void OpenFile(string path) => Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    [Transaction(TransactionMode.Manual)] public class Toggle : IExternalCommand
    {
        public Result Execute(ExternalCommandData d, ref string message, ElementSet elements)
        {
            // Enabling is allowed only through the settings window and its Save action.
            return new Options().Execute(d, ref message, elements);
        }
    }
    [Transaction(TransactionMode.Manual)] public class History : IExternalCommand
    {
        public Result Execute(ExternalCommandData d, ref string message, ElementSet elements)
        {
            try { NotificationController.OpenFile(NotificationController.Current.GetJournal(d.Application)); return Result.Succeeded; }
            catch (Exception ex) { message = ex.Message; return Result.Failed; }
        }
    }
    [Transaction(TransactionMode.Manual)] public class Options : IExternalCommand
    {
        public Result Execute(ExternalCommandData d, ref string message, ElementSet elements)
        {
            try { BuildWindow(d.Application).ShowDialog(); return Result.Succeeded; }
            catch (Exception ex) { message = ex.Message; return Result.Failed; }
        }
        public static Window BuildWindow(UIApplication ui)
        {
            var app = NotificationController.Current;
            if (app == null) throw new InvalidOperationException("Модуль уведомлений SAB не запущен.");
            var window = app.CreateWindow();
            if (ui != null) {
                new System.Windows.Interop.WindowInteropHelper(window).Owner = ui.MainWindowHandle;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            return window;
        }
    }
}
