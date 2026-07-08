using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;

namespace SyncReminderTest
{
    internal class SyncReminderController
    {
        // Block responsible for the duck overlay position. Adjust this value if your Revit ribbon is taller or shorter.
        private const double WorkspaceTopOffset = 235;

        // Set to true only when checking event flow. It will show TaskDialog messages.
        private static readonly bool DebugMode = false;

        private readonly UIControlledApplication _application;
        private readonly Dictionary<string, SyncSessionState> _sessions;
        private readonly ReminderSettingsService _settingsService;
        private ReminderSettings _settings;
        private TimeSpan _reminderDelay;
        private DateTime _lastIdlingCheck;
        private IntPtr _revitWindowHandle;
        private DuckReminderWindow _duckWindow;
        private string _activeDocumentKey;
        private bool _isStarted;
        private bool _isReminderVisible;

        public SyncReminderController(UIControlledApplication application)
        {
            _application = application;
            _sessions = new Dictionary<string, SyncSessionState>(StringComparer.OrdinalIgnoreCase);
            _settingsService = new ReminderSettingsService();
            _settings = _settingsService.Load();
            _reminderDelay = TimeSpan.FromMinutes(_settings.ReminderDelayMinutes);
            _lastIdlingCheck = DateTime.MinValue;
        }

        public void Start()
        {
            if (_isStarted)
            {
                return;
            }

            _application.ControlledApplication.DocumentOpened += OnDocumentOpened;
            _application.ControlledApplication.DocumentClosing += OnDocumentClosing;
            _application.ControlledApplication.DocumentSynchronizedWithCentral += OnDocumentSynchronizedWithCentral;
            _application.Idling += OnIdling;

            _revitWindowHandle = RevitWindowUtils.GetRevitMainWindowHandle();
            _isStarted = true;

            ShowDebugMessage("Sync reminder started.");
        }

        public void Stop()
        {
            if (!_isStarted)
            {
                return;
            }

            _application.ControlledApplication.DocumentOpened -= OnDocumentOpened;
            _application.ControlledApplication.DocumentClosing -= OnDocumentClosing;
            _application.ControlledApplication.DocumentSynchronizedWithCentral -= OnDocumentSynchronizedWithCentral;
            _application.Idling -= OnIdling;

            CloseOverlays();
            _sessions.Clear();
            _isStarted = false;

            ShowDebugMessage("Sync reminder stopped.");
        }

        public void ShowSettingsWindow(UIApplication uiApplication)
        {
            try
            {
                if (_revitWindowHandle == IntPtr.Zero)
                {
                    _revitWindowHandle = RevitWindowUtils.GetRevitMainWindowHandle();
                }

                SettingsWindow settingsWindow = new SettingsWindow(_settings);
                WindowInteropHelper helper = new WindowInteropHelper(settingsWindow);
                helper.Owner = _revitWindowHandle;

                bool? result = settingsWindow.ShowDialog();
                if (result != true)
                {
                    return;
                }

                _settingsService.Save(settingsWindow.Settings);
                ApplySettings(settingsWindow.Settings);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Sync Reminder - Settings", ex.Message);
            }
        }

        private void OnDocumentOpened(object sender, DocumentOpenedEventArgs e)
        {
            try
            {
                RegisterOrResetDocument(e.Document, "Document opened");
            }
            catch (Exception ex)
            {
                ShowError("DocumentOpened", ex);
            }
        }

        private void OnDocumentClosing(object sender, DocumentClosingEventArgs e)
        {
            try
            {
                Document document = e.Document;
                string documentKey = GetDocumentKey(document);

                if (!string.IsNullOrEmpty(documentKey))
                {
                    _sessions.Remove(documentKey);
                }

                if (string.Equals(_activeDocumentKey, documentKey, StringComparison.OrdinalIgnoreCase))
                {
                    _activeDocumentKey = null;
                    HideReminder();
                }
            }
            catch (Exception ex)
            {
                ShowError("DocumentClosing", ex);
            }
        }

        private void OnDocumentSynchronizedWithCentral(object sender, DocumentSynchronizedWithCentralEventArgs e)
        {
            try
            {
                Document document = e.Document;
                if (document == null)
                {
                    return;
                }

                ShowDebugMessage("Synchronized event. Status: " + e.Status);

                // Block responsible for resetting only after a real successful central sync.
                if (e.Status != RevitAPIEventStatus.Succeeded)
                {
                    return;
                }

                RegisterOrResetDocument(document, "Synchronized with central");

                string documentKey = GetDocumentKey(document);
                if (string.Equals(_activeDocumentKey, documentKey, StringComparison.OrdinalIgnoreCase))
                {
                    HideReminder();
                }
            }
            catch (Exception ex)
            {
                ShowError("DocumentSynchronizedWithCentral", ex);
            }
        }

        private void OnIdling(object sender, IdlingEventArgs e)
        {
            try
            {
                DateTime now = DateTime.Now;
                if ((now - _lastIdlingCheck).TotalSeconds < 5)
                {
                    return;
                }

                _lastIdlingCheck = now;

                if (_settings == null || !_settings.IsEnabled)
                {
                    HideReminder();
                    return;
                }

                UIApplication uiApplication = sender as UIApplication;
                if (uiApplication == null || uiApplication.ActiveUIDocument == null)
                {
                    _activeDocumentKey = null;
                    HideReminder();
                    return;
                }

                UIDocument uiDocument = uiApplication.ActiveUIDocument;
                Document document = uiDocument.Document;

                if (!IsValidActiveDocument(uiDocument, document))
                {
                    _activeDocumentKey = null;
                    HideReminder();
                    return;
                }

                SyncSessionState session = RegisterDocumentIfNeeded(document);
                if (session == null)
                {
                    _activeDocumentKey = null;
                    HideReminder();
                    return;
                }

                _activeDocumentKey = session.DocumentKey;

                TimeSpan timeAfterSync = now - session.LastSuccessfulSyncTime;
                if (timeAfterSync >= _reminderDelay)
                {
                    ShowReminder(session, timeAfterSync);
                    return;
                }

                HideReminder();
            }
            catch (Exception ex)
            {
                ShowError("Idling", ex);
            }
        }

        private bool IsValidActiveDocument(UIDocument uiDocument, Document document)
        {
            if (uiDocument == null || document == null)
            {
                return false;
            }

            if (!document.IsValidObject)
            {
                return false;
            }

            if (document.IsFamilyDocument)
            {
                return false;
            }

            if (document.IsLinked)
            {
                return false;
            }

            if (!document.IsWorkshared)
            {
                return false;
            }

            if (document.IsDetached)
            {
                return false;
            }

            View activeView = uiDocument.ActiveView;
            if (activeView == null || !activeView.IsValidObject)
            {
                return false;
            }

            return true;
        }

        private SyncSessionState RegisterDocumentIfNeeded(Document document)
        {
            if (!IsTrackableDocument(document))
            {
                return null;
            }

            string documentKey = GetDocumentKey(document);
            if (string.IsNullOrEmpty(documentKey))
            {
                return null;
            }

            SyncSessionState session;
            if (_sessions.TryGetValue(documentKey, out session))
            {
                return session;
            }

            session = new SyncSessionState();
            session.DocumentKey = documentKey;
            session.DocumentTitle = document.Title;
            session.LastSuccessfulSyncTime = DateTime.Now;
            _sessions[documentKey] = session;

            ShowDebugMessage("Timer started for " + session.DocumentTitle);

            return session;
        }

        private void RegisterOrResetDocument(Document document, string reason)
        {
            if (!IsTrackableDocument(document))
            {
                return;
            }

            string documentKey = GetDocumentKey(document);
            if (string.IsNullOrEmpty(documentKey))
            {
                return;
            }

            SyncSessionState session;
            if (!_sessions.TryGetValue(documentKey, out session))
            {
                session = new SyncSessionState();
                session.DocumentKey = documentKey;
                _sessions[documentKey] = session;
            }

            session.DocumentTitle = document.Title;
            session.LastSuccessfulSyncTime = DateTime.Now;

            ShowDebugMessage(reason + ": timer reset for " + session.DocumentTitle);
        }

        private bool IsTrackableDocument(Document document)
        {
            if (document == null || !document.IsValidObject)
            {
                return false;
            }

            if (document.IsFamilyDocument || document.IsLinked)
            {
                return false;
            }

            if (!document.IsWorkshared || document.IsDetached)
            {
                return false;
            }

            return true;
        }

        private string GetDocumentKey(Document document)
        {
            if (document == null || !document.IsValidObject)
            {
                return string.Empty;
            }

            string path = document.PathName;
            if (!string.IsNullOrWhiteSpace(path))
            {
                return path.Trim();
            }

            return document.Title + "|" + document.GetHashCode();
        }

        private void ShowReminder(SyncSessionState session, TimeSpan timeAfterSync)
        {
            if (_revitWindowHandle == IntPtr.Zero)
            {
                _revitWindowHandle = RevitWindowUtils.GetRevitMainWindowHandle();
            }

            Rect revitBounds;
            if (!RevitWindowUtils.TryGetWindowBounds(_revitWindowHandle, out revitBounds))
            {
                HideReminder();
                return;
            }

            Rect duckArea = new Rect(
                revitBounds.Left + 16,
                revitBounds.Top + WorkspaceTopOffset,
                Math.Max(260, revitBounds.Width - 32),
                Math.Max(180, revitBounds.Height - WorkspaceTopOffset - 28));

            if (_duckWindow == null)
            {
                _duckWindow = new DuckReminderWindow(_revitWindowHandle);
            }

            _duckWindow.SetAllowedArea(duckArea);
            _duckWindow.ShowDuck();

            if (!_isReminderVisible)
            {
                _isReminderVisible = true;
                ShowDebugMessage("Reminder visible for " + session.DocumentTitle + ". Minutes: " + timeAfterSync.TotalMinutes.ToString("0.0"));
            }
        }

        private void HideReminder()
        {
            if (_duckWindow != null)
            {
                _duckWindow.HideDuck();
            }

            _isReminderVisible = false;
        }

        private void ApplySettings(ReminderSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            _settings = settings.Clone();
            _settings.AnimationMode = ReminderAnimationMode.DuckOnly;

            if (_settings.ReminderDelayMinutes < 1)
            {
                _settings.ReminderDelayMinutes = 1;
            }

            if (_settings.ReminderDelayMinutes > 720)
            {
                _settings.ReminderDelayMinutes = 720;
            }

            _reminderDelay = TimeSpan.FromMinutes(_settings.ReminderDelayMinutes);

            if (!_settings.IsEnabled)
            {
                HideReminder();
                return;
            }
        }

        private void CloseOverlays()
        {
            if (_duckWindow != null)
            {
                _duckWindow.CloseDuck();
                _duckWindow = null;
            }

            _isReminderVisible = false;
        }

        private void ShowDebugMessage(string message)
        {
            if (!DebugMode)
            {
                return;
            }

            TaskDialog.Show("Sync Reminder Debug", message);
        }

        private void ShowError(string context, Exception exception)
        {
            if (exception == null)
            {
                return;
            }

            TaskDialog.Show("Sync Reminder - " + context, exception.Message);
        }
    }
}
