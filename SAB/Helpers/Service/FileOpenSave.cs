using System;
using System.Collections.Generic;
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