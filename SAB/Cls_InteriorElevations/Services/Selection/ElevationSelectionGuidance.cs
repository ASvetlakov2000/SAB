using System;
using System.Diagnostics;
using SAB.UI.Notifications;

namespace SAB.InteriorElevations.Services.Selection
{
    /// <summary>Selection hints contain no Revit objects and cannot interrupt model selection.</summary>
    public static class ElevationSelectionGuidance
    {
        private static WeakReference<NotificationStack> _previousStack;
        private static NotificationMessage _previousMessage;

        public static NotificationMessage Overview() => new NotificationMessage("SAB · Развёртки", "Выбор линий", NotificationTone.Information,
            "Выберите линии по очереди.\nНажмите Escape, чтобы завершить выбор.\nЗатем выберите помещение.\nДля нескольких групп сначала завершите выбор всех групп.");

        public static NotificationMessage Group(int number) => new NotificationMessage("SAB · Развёртки", "Группа №" + number, NotificationTone.Information,
            "Выберите линии группы №" + number + " и нажмите Escape. Затем добавьте следующую группу или завершите выбор групп и выберите помещение.");

        public static NotificationMessage GroupCompleted(int number) => new NotificationMessage("SAB · Развёртки", "Группа №" + number + " выбрана", NotificationTone.Information,
            "Добавьте следующую группу или завершите выбор групп, чтобы выбрать помещение.");

        public static NotificationMessage Room() => new NotificationMessage("SAB · Развёртки", "Выбор помещения", NotificationTone.Information,
            "Линии выбраны, выберите помещение.");

        public static void Show(IntPtr owner, NotificationMessage message)
        {
            try
            {
                var stack = NotificationStack.Show(owner, message);
                Dismiss();
                _previousStack = new WeakReference<NotificationStack>(stack);
                _previousMessage = message;
            }
            catch (Exception exception) { Debug.WriteLine("SAB selection guidance: " + exception.Message); }
        }

        public static void Dismiss()
        {
            try
            {
                NotificationStack stack;
                if (_previousStack != null && _previousStack.TryGetTarget(out stack)) stack.Dismiss(_previousMessage);
            }
            catch (Exception exception) { Debug.WriteLine("SAB selection guidance cleanup: " + exception.Message); }
            finally { _previousStack = null; _previousMessage = null; }
        }
    }
}
