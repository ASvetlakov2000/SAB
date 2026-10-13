using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SAB.UI.Notifications;

namespace SAB.ParameterTools.UI
{
    // All Revit values are read synchronously by the command. The notification retains only text.
    internal static class ParameterNotifications
    {
        internal static NotificationMessage Result(string summary, IEnumerable<Issue> issues, bool committed, string operation)
        {
            var problems = (issues ?? Enumerable.Empty<Issue>()).ToList();
            var ids = problems.SelectMany(i => i.ElementIds ?? (i.ElementId == null
                ? Enumerable.Empty<long>() : new[] { Ids.Value(i.ElementId) })).Where(id => id > 0).Distinct().ToList();
            var support = new StringBuilder();
            if (problems.Count > 0)
            {
                support.AppendLine("Модуль: SAB · Параметризация");
                support.AppendLine("Операция: " + operation);
                foreach (var issue in problems)
                {
                    support.AppendLine();
                    if (!string.IsNullOrWhiteSpace(issue.Parameter)) support.AppendLine("Параметр: " + issue.Parameter);
                    if (!string.IsNullOrWhiteSpace(issue.Element)) support.AppendLine("Элемент: " + issue.Element);
                    var issueIds = issue.ElementIds ?? (issue.ElementId == null ? Enumerable.Empty<long>() : new[] { Ids.Value(issue.ElementId) });
                    if (issueIds.Any()) support.AppendLine("ElementId: " + string.Join(", ", issueIds));
                    if (issue.Actual != null) support.AppendLine("Фактическое значение: " + issue.Actual);
                    if (issue.Expected != null) support.AppendLine("Ожидаемое значение: " + issue.Expected);
                    support.AppendLine("Причина: " + issue.Reason);
                    if (!string.IsNullOrWhiteSpace(issue.Technical)) support.AppendLine(issue.Technical);
                }
            }
            return new NotificationMessage("SAB · Параметризация", committed
                ? (problems.Count == 0 ? "Завершено" : "Завершено с пропусками") : "Операция отменена",
                committed ? (problems.Count == 0 ? NotificationTone.Success : NotificationTone.Warning) : NotificationTone.Error,
                summary, string.Join(", ", ids), support.ToString().TrimEnd());
        }

        internal static void ShowResult(IntPtr owner, string summary, IEnumerable<Issue> issues, bool committed, string operation)
        { NotificationStack.Show(owner, Result(summary, issues, committed, operation)); }

        internal static void Information(IntPtr owner, string summary)
        { NotificationStack.Show(owner, new NotificationMessage("SAB · Параметризация", "Сообщение", NotificationTone.Information, summary)); }

        internal static void Cancelled(IntPtr owner)
        { NotificationStack.Show(owner, new NotificationMessage("SAB · Параметризация", "Операция отменена", NotificationTone.Error, "Операция отменена пользователем.")); }

        internal static void Error(IntPtr owner, Exception exception)
        {
            NotificationStack.Show(owner, new NotificationMessage("SAB · Параметризация", "Операция отменена", NotificationTone.Error,
                exception.Message, null, "Модуль: SAB · Параметризация\nПричина: " + exception.Message + "\n" + exception
                + "\nЖурнал: " + System.IO.Path.Combine(RuntimeHost.LogFolder, "errors.log")));
        }
    }
}
