using System;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.Revit.UI;

namespace SyncReminderTest
{
    public class App : IExternalApplication
    {
        private const string RibbonTabName = "Sync Reminder";
        private const string RibbonPanelName = "Синхронизация";

        private SyncReminderController _controller;

        internal static SyncReminderController CurrentController { get; private set; }

        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                if (application == null)
                {
                    TaskDialog.Show("Sync Reminder", "Revit application is not available.");
                    return Result.Failed;
                }

                CreateRibbon(application);

                _controller = new SyncReminderController(application);
                CurrentController = _controller;
                _controller.Start();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Sync Reminder", ex.Message);
                return Result.Failed;
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                if (_controller != null)
                {
                    _controller.Stop();
                    _controller = null;
                }

                CurrentController = null;

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Sync Reminder", ex.Message);
                return Result.Failed;
            }
        }

        private void CreateRibbon(UIControlledApplication application)
        {
            try
            {
                application.CreateRibbonTab(RibbonTabName);
            }
            catch
            {
                // Revit throws if the tab already exists. This is safe during add-in reload testing.
            }

            RibbonPanel panel = GetOrCreatePanel(application);
            string assemblyPath = Assembly.GetExecutingAssembly().Location;

            PushButtonData settingsButtonData = new PushButtonData(
                "SyncReminderSettings",
                "Настройки\nсинхронизации",
                assemblyPath,
                "SyncReminderTest.SettingsCommand");

            settingsButtonData.ToolTip = "Настроить напоминание о синхронизации с центральной моделью.";
            panel.AddItem(settingsButtonData);
        }

        private RibbonPanel GetOrCreatePanel(UIControlledApplication application)
        {
            IList<RibbonPanel> panels = application.GetRibbonPanels(RibbonTabName);
            for (int i = 0; i < panels.Count; i++)
            {
                RibbonPanel panel = panels[i];
                if (panel != null && panel.Name == RibbonPanelName)
                {
                    return panel;
                }
            }

            return application.CreateRibbonPanel(RibbonTabName, RibbonPanelName);
        }
    }
}
