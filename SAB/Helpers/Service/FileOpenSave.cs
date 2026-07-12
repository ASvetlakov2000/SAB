using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Notifications.Wpf;
using Application = Autodesk.Revit.ApplicationServices.Application;
using Binding = Autodesk.Revit.DB.Binding;

namespace asBIM
{
    ///<summary>
    /// Класс для открытия файла и передачи его пути
    ///</summary>
    public static class OpenFile
    {
        ///<summary>
        /// Метод для открытия файла и передачи его пути
        ///</summary>
        /// <param name="title">Заголовок в UI.</param>
        /// <param name="format">Формат открываемого файлаа.</param>
        /// <returns>path - путь к файлу</returns>>
        public static string OpenSingleFile(string title,string format)
        {
            var path = string.Empty;
            using var openFileDialog = new OpenFileDialog
            {
                // Заголовок
                Title = title,
                // Фильтр выбора формата
                Filter = $"Revit (*.{format})|*.{format}|" + "All files (*.*)|*.*"
            };
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                path = openFileDialog.FileName;
            }
            return path;
        }
    }

    public static class CreateFile
    {
        ///<summary>
        /// Метод для открытия файла и передачи его пути
        ///</summary>
        /// <param name="title">Заголовок в UI.</param>
        /// <param name="format">Формат открываемого файлаа.</param>
        /// <returns>path - путь к файлу</returns>>
        public static string CreateSingleFile(string title,string format)
        {
            var path = string.Empty;
            using var createFileDialog = new SaveFileDialog()
            {
                // Заголовок
                Title = title,
                // Фильтр выбора формата
                Filter = $"Revit (*.{format})|*.{format}|" + "All files (*.*)|*.*"
            };
            if (createFileDialog.ShowDialog() == DialogResult.OK)
            {
                path = createFileDialog.FileName;
            }
            return path;
        }
    }

    ///<summary>
    /// Класс для выбора папки через диалог с полем пути.
    /// Подходит для сценариев, где нужно вставить путь вручную.
    ///</summary>
    public static class OpenFolder
    {
        ///<summary>
        /// Возвращает выбранную папку через диалог с предзаполненным именем.
        /// </summary>
        /// <param name="title">Заголовок окна.</param>
        /// <param name="suggestedName">Предзаполненное имя в поле.</param>
        /// <param name="initialDirectory">Стартовая директория.</param>
        /// <returns>Путь к папке или пустая строка при отмене.</returns>
        public static string SelectFolderPath(
            string title,
            string suggestedName = "",
            string initialDirectory = "",
            IntPtr ownerHandle = default(IntPtr))
        {
            string safeSuggestedName = string.IsNullOrWhiteSpace(suggestedName) ? "Output" : suggestedName;

            using var saveDialog = new SaveFileDialog
            {
                Title = title,
                Filter = "All files (*.*)|*.*",
                FileName = safeSuggestedName,
                AddExtension = false,
                CheckFileExists = false,
                CheckPathExists = true,
                OverwritePrompt = false,
                ValidateNames = false
            };

            if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            {
                saveDialog.InitialDirectory = initialDirectory;
            }
            else
            {
                saveDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            DialogResult dialogResult = ownerHandle == IntPtr.Zero
                ? saveDialog.ShowDialog()
                : saveDialog.ShowDialog(new DialogOwnerWindow(ownerHandle));

            if (dialogResult != DialogResult.OK)
            {
                return string.Empty;
            }

            return ResolveFolderPath(saveDialog.FileName);
        }

        // Блок преобразования выбранного значения из диалога в путь к папке.
        private static string ResolveFolderPath(string selectedPath)
        {
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return string.Empty;
            }

            string normalizedPath = selectedPath.Trim().Trim('"');

            if (Directory.Exists(normalizedPath))
            {
                return normalizedPath;
            }

            if (normalizedPath.EndsWith("\\") || normalizedPath.EndsWith("/"))
            {
                return normalizedPath.TrimEnd('\\', '/');
            }

            string folderPath = Path.GetDirectoryName(normalizedPath);

            if (!string.IsNullOrWhiteSpace(folderPath))
            {
                return folderPath;
            }

            return normalizedPath;
        }

        // Блок владельца диалога: окно выбора пути не теряется за Revit.
        private sealed class DialogOwnerWindow : IWin32Window
        {
            public DialogOwnerWindow(IntPtr handle)
            {
                Handle = handle;
            }

            public IntPtr Handle { get; private set; }
        }
    }

    ///<summary>
    /// Класс для таймера в уведомлении
    ///</summary>
    internal class TimeOfWorkConverter
    {
        public double timeInSecOutput;
        public double timeInMinOutput;

        // TODO: Если количество минут = 0
        public static TimeOfWorkConverter ConvertTime(double timeInSec)
        {
            if (timeInSec <= 60.0)
            {
                return new TimeOfWorkConverter
                    { timeInSecOutput = timeInSec % 60.0, timeInMinOutput = 0 };
            }
            else
            {
                return new TimeOfWorkConverter
                    { timeInSecOutput = timeInSec % 60.0, timeInMinOutput = (timeInSec / 60.0) - 1 };
            }
        }
    }
}
