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
             string iconSPath,
             bool useGrayIcons = false)
        {
            PushButtonData buttonData = new PushButtonData(name, tooltip, Assembly.GetExecutingAssembly().Location, commandClass)
            {
                Name = name,
                ToolTip = tooltip,
                LargeImage = GetEmbeddedImage(iconLPath, useGrayIcons),
                Image = GetEmbeddedImage(iconSPath, useGrayIcons)
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
            string iconSPath,
            bool smallIcon = false,
            bool useGrayIcons = false)
        {
            PushButtonData buttonData = new PushButtonData(name, tooltip, Assembly.GetExecutingAssembly().Location, commandClass)
            {
                Name = name,
                ToolTip = tooltip,
                LargeImage = GetEmbeddedImage(smallIcon ? iconSPath : iconLPath, useGrayIcons),
                Image = GetEmbeddedImage(iconSPath, useGrayIcons)
            };

            pullDownButton.AddPushButton(buttonData);
        }

        ///<summary>
        /// Метод GetEmbeddedImage для загрузки изображения
        ///</summary>
        /// <param name="resourcePath">Путь к изображению.</param>
        /// <returns>null</returns>>
        public static ImageSource GetEmbeddedImage(string resourcePath, bool useGrayColor = false)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream(resourcePath))
            {
                if (stream != null)
                {
                    var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                    BitmapSource frame = decoder.Frames[0];
                    if (useGrayColor)
                    {
                        frame = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
                    }
                    // Ribbon sizes are WPF device-independent units. A 32px PNG at 72 DPI
                    // otherwise occupies 42.7 units and is clipped by a 32-unit control.
                    int stride = (frame.PixelWidth * frame.Format.BitsPerPixel + 7) / 8;
                    byte[] pixels = new byte[stride * frame.PixelHeight];
                    frame.CopyPixels(pixels, stride, 0);
                    if (useGrayColor)
                    {
                        // Preserve the original silhouette and transparency while replacing
                        // the timer's pink accent with a neutral ribbon color.
                        for (int pixel = 0; pixel < pixels.Length; pixel += 4)
                        {
                            pixels[pixel] = 112;
                            pixels[pixel + 1] = 112;
                            pixels[pixel + 2] = 112;
                        }
                    }
                    var image = BitmapSource.Create(frame.PixelWidth, frame.PixelHeight, 96, 96,
                        frame.Format, frame.Palette, pixels, stride);
                    image.Freeze();
                    return image;
                }
            }
            return null;
        }
    }
}
