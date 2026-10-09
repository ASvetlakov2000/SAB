using System;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Events;

namespace SAB.ParameterTools
{
    // Lifecycle is owned by SAB.MainPanel; this module has no separate add-in registration.
    internal static class ParameterToolsModule
    {
        internal static RuntimeHost Host { get; private set; }

        internal static void Start(UIControlledApplication application, RibbonPanel panel)
        {
            var fill = (SplitButton)panel.AddItem(new SplitButtonData("SAB_ParameterTools_FillMenu", "Параметры"));
            fill.IsSynchronizedWithCurrentItem = false;
            fill.AddPushButton(Data<FillParametersCommand>("Fill", "Занести\nпараметры", "Выделить элементы, запустить команду и выбрать помещение.", "Apply"));
            fill.AddPushButton(Data<SettingsCommand>("Settings", "Настройки параметров", "Параметры, источники, категории и матрица уровней.", "Settings"));
            panel.AddStackedItems(
                Data<CheckParametersCommand>("Check", "Проверить параметры", "Выбрать категории текущего вида и включить динамические фильтры пустых обязательных параметров.", "Check"),
                Data<ClearHighlightCommand>("Clear", "Снять подсветку", "Снять фильтры проверки с текущего вида и восстановить подсветку старой версии.", "Clear"));
            Host = new RuntimeHost();
            application.ControlledApplication.DocumentSaving += Saving;
            application.ControlledApplication.DocumentSavingAs += SavingAs;
            application.ControlledApplication.DocumentClosing += Closing;
        }

        private static PushButtonData Data<T>(string id, string label, string tip, string icon)
        {
            return new PushButtonData("SAB_ParameterTools_" + id, label,
                typeof(ParameterToolsModule).Assembly.Location, typeof(T).FullName)
            {
                ToolTip = tip,
                AvailabilityClassName = typeof(ProjectAvailability).FullName,
                LargeImage = ParameterToolsIcons.Create(icon, 32),
                Image = ParameterToolsIcons.Create(icon, 16)
            };
        }

        private static void Saving(object sender, DocumentSavingEventArgs args)
        {
            try { Host.Highlight.Restore(args.Document); }
            catch (Exception ex) { if (args.Cancellable) args.Cancel(); TaskDialog.Show("SAB — подсветка", "Не удалось снять подсветку перед сохранением.\n" + ex.Message); }
        }
        private static void SavingAs(object sender, DocumentSavingAsEventArgs args)
        {
            try { Host.Highlight.Restore(args.Document); }
            catch (Exception ex) { if (args.Cancellable) args.Cancel(); TaskDialog.Show("SAB — подсветка", "Не удалось снять подсветку перед сохранением.\n" + ex.Message); }
        }
        private static void Closing(object sender, DocumentClosingEventArgs args)
        {
            try { Host.Highlight.Restore(args.Document); Host.Forget(args.Document); }
            catch (Exception ex)
            {
                Host.Log(ex); if (args.Cancellable) args.Cancel();
                TaskDialog.Show("SAB — подсветка", "Не удалось снять подсветку перед закрытием.\n" + ex.Message);
            }
        }
        internal static void Stop(UIControlledApplication application)
        {
            if (Host == null) return;
            application.ControlledApplication.DocumentSaving -= Saving;
            application.ControlledApplication.DocumentSavingAs -= SavingAs;
            application.ControlledApplication.DocumentClosing -= Closing;
            Host.Dispose(); Host = null;
        }
    }

    public sealed class ProjectAvailability : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication app, Autodesk.Revit.DB.CategorySet categories)
        {
            return ParameterToolsModule.Host != null && app.ActiveUIDocument != null && !app.ActiveUIDocument.Document.IsFamilyDocument;
        }
    }
}
