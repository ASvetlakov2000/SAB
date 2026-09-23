using System;
using System.Diagnostics;
using System.IO;
using Autodesk.Revit.UI;
using RevitLibraryBuilder.Services.Regulations;
using Forms = System.Windows.Forms;

namespace RevitLibraryBuilder.Commands.Regulations
{
    internal static class RegulationsLandingWorkflow
    {
        public static Result Run(ExternalCommandData commandData, bool changePath, ref string message)
        {
            var settings = new RegulationsLandingSettings();
            string path = string.Empty;
            string reason = string.Empty;
            try { path = settings.Load(); }
            catch (Exception exception) { reason = "Не удалось прочитать настройку: " + exception.Message; }

            while (true)
            {
                if (changePath || !RegulationsLandingSettings.IsValid(path) || !string.IsNullOrEmpty(reason))
                {
                    if (!changePath)
                    {
                        var notice = new TaskDialog("Регламенты")
                        {
                            MainInstruction = "Не удалось открыть лендинг регламентов",
                            MainContent = (string.IsNullOrEmpty(reason) ? "Путь не задан или HTML-файл недоступен." : reason)
                                + "\n\nТекущий путь:\n" + (string.IsNullOrEmpty(path) ? "(не задан)" : path),
                            CommonButtons = TaskDialogCommonButtons.Cancel
                        };
                        notice.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Выбрать другой HTML-файл");
                        if (notice.Show() != TaskDialogResult.CommandLink1) return Result.Cancelled;
                    }

                    using (var picker = new Forms.OpenFileDialog())
                    {
                        picker.Title = "Лендинг регламентов — выберите файл или вставьте полный путь";
                        picker.Filter = "HTML-страницы (*.html;*.htm)|*.html;*.htm";
                        picker.CheckFileExists = true;
                        picker.Multiselect = false;
                        picker.RestoreDirectory = true;
                        if (RegulationsLandingSettings.IsValid(path))
                        {
                            picker.InitialDirectory = Path.GetDirectoryName(path);
                            picker.FileName = Path.GetFileName(path);
                        }
                        if (picker.ShowDialog(new RevitWindow(commandData.Application.MainWindowHandle)) != Forms.DialogResult.OK)
                            return Result.Cancelled;
                        try
                        {
                            settings.Save(picker.FileName);
                            path = RegulationsLandingSettings.Normalize(picker.FileName);
                        }
                        catch (Exception exception)
                        {
                            message = "Не удалось сохранить путь. Предыдущая настройка сохранена.\n" + exception.Message;
                            TaskDialog.Show("Регламенты", message);
                            return Result.Failed;
                        }
                    }
                    if (changePath)
                    {
                        TaskDialog.Show("Регламенты", "Путь к лендингу сохранён:\n" + path);
                        return Result.Succeeded;
                    }
                    reason = string.Empty;
                }

                try
                {
                    Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                    return Result.Succeeded;
                }
                catch (Exception exception)
                {
                    reason = "Ошибка открытия HTML-файла: " + exception.Message;
                }
            }
        }

        private sealed class RevitWindow : Forms.IWin32Window
        {
            public RevitWindow(IntPtr handle) { Handle = handle; }
            public IntPtr Handle { get; private set; }
        }
    }
}
