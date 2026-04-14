using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;

namespace SAB.Helpers
{
    ///<summary>
    /// Класс для добавления кнопок на панель инструментов
    ///</summary>
    internal static class Ribbon
    {
        ///<summary>
        /// Метод для добавления простой кнопки в панель. Одна самостоятельная кнопка
        /// Кнопка в составе выпадающего списка
        ///</summary>
        /// <param name="name">Имя добавляемой кнопки</param>
        /// <param name="tooltip">Описание добавляемой кнопки</param>
        /// <param name="commandClass">Класс с командой</param>
        /// <param name="iconLPath">Путь к большой иконке</param>
        /// <param name="iconSPath">Путь к маленькой иконке</param>
        public static void AddPushButtonSingle(RibbonPanel ribbonPanel,
             string name,
             string tooltip,
             string commandClass,
             string iconLPath,
             string iconSPath)
        {
            PushButtonData buttonData = new PushButtonData(name, tooltip, Assembly.GetExecutingAssembly().Location, commandClass)
            {
                Name = name,
                ToolTip = tooltip,
                LargeImage = GetEmbeddedImage(iconLPath),
                Image = GetEmbeddedImage(iconSPath)
            };

            PushButton pushButton = ribbonPanel.AddItem(buttonData) as PushButton;
        }

        ///<summary>
        /// Метод для добавления простой кнопки в составе SplitButton.
        /// Кнопка в составе выпадающего списка
        ///</summary>
        /// <param name="pullDownButton">Кнопка по типу SplitButton</param>
        /// <param name="name">Имя добавляемой кнопки</param>
        /// <param name="tooltip">Описание добавляемой кнопки</param>
        /// <param name="commandClass">Класс с командой</param>
        /// <param name="iconLPath">Путь к большой иконке</param>
        /// <param name="iconSPath">Путь к маленькой иконке</param>
        public static void AddPushButtonToSplit(SplitButton
                pullDownButton,
            string name,
            string tooltip,
            string commandClass,
            string iconLPath,
            string iconSPath)
        {
            PushButtonData buttonData = new PushButtonData(name, tooltip, Assembly.GetExecutingAssembly().Location, commandClass)
            {
                Name = name,
                ToolTip = tooltip,
                LargeImage = GetEmbeddedImage(iconLPath),
                Image = GetEmbeddedImage(iconSPath)
            };

            pullDownButton.AddPushButton(buttonData);
        }

        ///<summary>
        /// Метод GetEmbeddedImage для загрузки изображения
        ///</summary>
        /// <param name="resourcePath">Путь к изображению.</param>
        /// <returns>null</returns>>
        public static ImageSource GetEmbeddedImage(string resourcePath)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(resourcePath))
            {
                if (stream != null)
                {
                    var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
                    return decoder.Frames[0];
                }
            }
            return null;
        }
    }
}