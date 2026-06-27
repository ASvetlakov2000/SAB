using System;
using System.IO;
using Microsoft.Toolkit.Uwp.Notifications;

namespace Helpers.Notifications.ToastNotifications
{
    public static class ToastNotifier
    {
        private const int DefaultDurationSeconds = 10;
        private const string OpenFolderButtonText = "\u041E\u0442\u043A\u0440\u044B\u0442\u044C \u043F\u0430\u043F\u043A\u0443";

        public static void ShowInfo(string title, string message, int durationSeconds = DefaultDurationSeconds)
        {
            ShowToastInternal(title, message, ToastType.Info, null, durationSeconds);
        }

        public static void ShowSuccess(string title, string message, int durationSeconds = DefaultDurationSeconds)
        {
            ShowToastInternal(title, message, ToastType.Success, null, durationSeconds);
        }

        public static void ShowWarning(string title, string message, int durationSeconds = DefaultDurationSeconds)
        {
            ShowToastInternal(title, message, ToastType.Warning, null, durationSeconds);
        }

        public static void ShowError(string title, string message, int durationSeconds = DefaultDurationSeconds)
        {
            ShowToastInternal(title, message, ToastType.Error, null, durationSeconds);
        }

        public static void ShowFolderLinkInfo(string title, string message, string folderLink, int durationSeconds = DefaultDurationSeconds)
        {
            ShowToastInternal(title, message, ToastType.Info, folderLink, durationSeconds);
        }

        public static void ShowFolderLinkSuccess(string title, string message, string folderLink, int durationSeconds = DefaultDurationSeconds)
        {
            ShowToastInternal(title, message, ToastType.Success, folderLink, durationSeconds);
        }

        public static void ShowFolderLinkWarning(string title, string message, string folderLink, int durationSeconds = DefaultDurationSeconds)
        {
            ShowToastInternal(title, message, ToastType.Warning, folderLink, durationSeconds);
        }

        public static void ShowFolderLinkError(string title, string message, string folderLink, int durationSeconds = DefaultDurationSeconds)
        {
            ShowToastInternal(title, message, ToastType.Error, folderLink, durationSeconds);
        }

        public static ToastContent BuildInfo(string title, string message, int durationSeconds = DefaultDurationSeconds)
        {
            return BuildToastContent(title, message, ToastType.Info, null, durationSeconds);
        }

        public static ToastContent BuildSuccess(string title, string message, int durationSeconds = DefaultDurationSeconds)
        {
            return BuildToastContent(title, message, ToastType.Success, null, durationSeconds);
        }

        public static ToastContent BuildWarning(string title, string message, int durationSeconds = DefaultDurationSeconds)
        {
            return BuildToastContent(title, message, ToastType.Warning, null, durationSeconds);
        }

        public static ToastContent BuildError(string title, string message, int durationSeconds = DefaultDurationSeconds)
        {
            return BuildToastContent(title, message, ToastType.Error, null, durationSeconds);
        }

        public static ToastContent BuildFolderLinkInfo(string title, string message, string folderLink, int durationSeconds = DefaultDurationSeconds)
        {
            return BuildToastContent(title, message, ToastType.Info, folderLink, durationSeconds);
        }

        public static ToastContent BuildFolderLinkSuccess(string title, string message, string folderLink, int durationSeconds = DefaultDurationSeconds)
        {
            return BuildToastContent(title, message, ToastType.Success, folderLink, durationSeconds);
        }

        public static ToastContent BuildFolderLinkWarning(string title, string message, string folderLink, int durationSeconds = DefaultDurationSeconds)
        {
            return BuildToastContent(title, message, ToastType.Warning, folderLink, durationSeconds);
        }

        public static ToastContent BuildFolderLinkError(string title, string message, string folderLink, int durationSeconds = DefaultDurationSeconds)
        {
            return BuildToastContent(title, message, ToastType.Error, folderLink, durationSeconds);
        }

        private static void ShowToastInternal(
            string title,
            string message,
            ToastType toastType,
            string folderLink,
            int durationSeconds)
        {
            try
            {
                ToastContentBuilder builder = BuildToastContentBuilder(title, message, toastType, folderLink, durationSeconds);
                builder.Show();
            }
            catch
            {
                // Fallback keeps notifications visible if Windows blocks native toasts for Revit without a registered AUMID.
                ShowFallbackToast(title, message, toastType, folderLink, durationSeconds);
            }
        }

        private static ToastContent BuildToastContent(
            string title,
            string message,
            ToastType toastType,
            string folderLink,
            int durationSeconds)
        {
            ToastContentBuilder builder = BuildToastContentBuilder(title, message, toastType, folderLink, durationSeconds);
            return builder.GetToastContent();
        }

        private static ToastContentBuilder BuildToastContentBuilder(
            string title,
            string message,
            ToastType toastType,
            string folderLink,
            int durationSeconds)
        {
            ToastContentBuilder builder = new ToastContentBuilder()
                .AddText(GetTitleWithIcon(title, toastType))
                .AddText(message ?? string.Empty)
                .AddArgument("toastType", toastType.ToString())
                .AddArgument("duration", GetSafeDuration(durationSeconds).ToString());

            if (!string.IsNullOrWhiteSpace(folderLink))
            {
                ToastButton openFolderButton = new ToastButton()
                    .SetContent(OpenFolderButtonText);
                Uri folderUri = CreateFolderUri(folderLink);
                if (folderUri != null)
                {
                    openFolderButton.SetProtocolActivation(folderUri);
                }
                else
                {
                    openFolderButton
                        .AddArgument("folder", folderLink)
                        .SetBackgroundActivation();
                }

                builder.AddText(folderLink)
                    .AddArgument("folder", folderLink)
                    .AddButton(openFolderButton);
            }

            return builder;
        }

        private static Uri CreateFolderUri(string folderLink)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderLink))
                {
                    return null;
                }

                Uri uri;
                if (Uri.TryCreate(folderLink, UriKind.Absolute, out uri))
                {
                    return uri;
                }

                string fullPath = Path.GetFullPath(folderLink);
                return new Uri(fullPath);
            }
            catch
            {
                return null;
            }
        }

        private static int GetSafeDuration(int durationSeconds)
        {
            if (durationSeconds > 0)
            {
                return durationSeconds;
            }

            return DefaultDurationSeconds;
        }

        private static void ShowFallbackToast(
            string title,
            string message,
            ToastType toastType,
            string folderLink,
            int durationSeconds)
        {
            if (!string.IsNullOrWhiteSpace(folderLink))
            {
                ShowFallbackToastWithFolderLink(title, message, toastType, folderLink, durationSeconds);
                return;
            }

            switch (toastType)
            {
                case ToastType.Info:
                    SabStyledToastNotifier.ShowInfo(title, message, durationSeconds);
                    break;
                case ToastType.Success:
                    SabStyledToastNotifier.ShowSuccess(title, message, durationSeconds);
                    break;
                case ToastType.Warning:
                    SabStyledToastNotifier.ShowWarning(title, message, durationSeconds);
                    break;
                case ToastType.Error:
                    SabStyledToastNotifier.ShowError(title, message, durationSeconds);
                    break;
            }
        }

        private static void ShowFallbackToastWithFolderLink(
            string title,
            string message,
            ToastType toastType,
            string folderLink,
            int durationSeconds)
        {
            switch (toastType)
            {
                case ToastType.Info:
                    SabStyledToastNotifier.ShowFolderLinkInfo(title, message, folderLink, durationSeconds);
                    break;
                case ToastType.Success:
                    SabStyledToastNotifier.ShowFolderLinkSuccess(title, message, folderLink, durationSeconds);
                    break;
                case ToastType.Warning:
                    SabStyledToastNotifier.ShowFolderLinkWarning(title, message, folderLink, durationSeconds);
                    break;
                case ToastType.Error:
                    SabStyledToastNotifier.ShowFolderLinkError(title, message, folderLink, durationSeconds);
                    break;
            }
        }

        private static string GetTitleWithIcon(string title, ToastType toastType)
        {
            return GetIcon(toastType) + " " + (title ?? string.Empty);
        }

        private static string GetIcon(ToastType toastType)
        {
            switch (toastType)
            {
                case ToastType.Info:
                    return "ℹ️";
                case ToastType.Success:
                    return "✅";
                case ToastType.Warning:
                    return "⚠️";
                case ToastType.Error:
                    return "❌";
                default:
                    return "❔";
            }
        }
    }

    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }
}
