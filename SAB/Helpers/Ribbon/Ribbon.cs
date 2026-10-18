using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Autodesk.Revit.UI;

namespace SAB.Helpers
{
    ///<summary>
    /// Класс для добавления кнопок на панель инструментов
    ///</summary>
    internal static class Ribbon
    {
        internal static readonly Color SettingsIconColor = Color.FromRgb(0, 0, 0);
        internal static readonly Color LibraryIconColor = Color.FromRgb(128, 128, 128);
        internal static readonly Color AlbumIconColor = Color.FromRgb(92, 109, 187);
        internal static readonly Color MaterialIconColor = Color.FromRgb(246, 169, 80);
        internal static readonly Color ParameterIconColor = Color.FromRgb(134, 162, 147);

        private static Color? GetIconColor(string resourcePath)
        {
            // Both ribbon sizes and every nested command share the module palette.
            int sizeSeparator = resourcePath.LastIndexOf('_');
            if (sizeSeparator < 0) return null;
            switch (resourcePath.Substring(0, sizeSeparator))
            {
                case "SAB.Resources.OpenPluginInstructionsHtmlCommand":
                case "SAB.Resources.SyncReminderSettings":
                case "SAB.Resources.Notifications":
                    return SettingsIconColor;
                case "SAB.Resources.ExportTypesSingleFileCommand":
                    return LibraryIconColor;
                case "SAB.Resources.CreateViewsAndSheets":
                case "SAB.Resources.DeleteViewsAndSheets":
                case "SAB.Resources.EditViewTemplateGraphics":
                case "SAB.Resources.FilledRegionCreate":
                case "SAB.Resources.FilledRegionSettings":
                    return AlbumIconColor;
                case "SAB.Resources.MaterialDataSheet":
                case "SAB.Resources.MaterialLayers":
                    return MaterialIconColor;
                case "SAB.Resources.ParameterIndex":
                case "SAB.Resources.ParameterSearch":
                case "SAB.Resources.ParameterSearchReset":
                    return ParameterIconColor;
                default:
                    return null;
            }
        }

        internal static PushButtonData CreatePushButtonData(string name, string text, string commandClass,
            string iconLPath, string iconSPath, int iconSize = 16)
        {
            ImageSource image = GetEmbeddedImage(iconSize > 16 ? iconLPath : iconSPath);
            if (image != null && iconSize != 16)
            {
                var drawing = new DrawingImage(new ImageDrawing(image, new System.Windows.Rect(0, 0, iconSize, iconSize)));
                drawing.Freeze(); image = drawing;
            }
            return new PushButtonData(name, text, Assembly.GetExecutingAssembly().Location, commandClass)
            {
                ToolTip = text,
                LargeImage = GetEmbeddedImage(iconLPath),
                Image = image
            };
        }

        internal static ImageSource GetMaterialQuantityImage(int size)
        {
            var drawing = new DrawingGroup();
            using (var context = drawing.Open())
            {
                context.DrawRectangle(Brushes.Transparent, null, new System.Windows.Rect(0, 0, 32, 32));
                // Keep the badge area transparent so the ribbon background shows through.
                context.PushClip(new CombinedGeometry(GeometryCombineMode.Exclude,
                    new RectangleGeometry(new System.Windows.Rect(0, 0, 32, 32)),
                    new RectangleGeometry(new System.Windows.Rect(18, 18, 14, 14))));
                context.DrawImage(GetEmbeddedImage("SAB.Resources.MaterialDataSheet_" + size + ".png"),
                    new System.Windows.Rect(0, 0, 32, 32));
                context.Pop();
                context.DrawImage(GetEmbeddedImage("SAB.Resources.MaterialLayers_32.png"),
                    new System.Windows.Rect(17, 17, 15, 15));
            }
            drawing.Transform = new ScaleTransform(size / 32.0, size / 32.0);
            var image = new DrawingImage(drawing);
            image.Freeze();
            return image;
        }

        internal static ImageSource GetElevationDecorationImage(int size)
        {
            // Lucide pencil-ruler, ISC. Original SVG and attribution ship with SAB.
            var drawing = new DrawingGroup();
            var pen = new Pen(new SolidColorBrush(Color.FromRgb(251, 152, 152)), 1)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
                LineJoin = PenLineJoin.Round
            };
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                "SAB.Resources.DecorateInteriorElevations.svg"))
            using (var context = drawing.Open())
            {
                context.DrawRectangle(Brushes.Transparent, null, new System.Windows.Rect(0, 0, 24, 24));
                foreach (var path in XDocument.Load(stream).Root.Elements())
                {
                    context.DrawGeometry(null, pen, Geometry.Parse((string)path.Attribute("d")));
                }
            }
            drawing.Transform = new ScaleTransform(size / 24.0, size / 24.0);
            var image = new DrawingImage(drawing);
            image.Freeze();
            return image;
        }

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
                    Color? iconColor = useGrayColor ? Color.FromRgb(112, 112, 112) : GetIconColor(resourcePath);
                    if (iconColor.HasValue)
                    {
                        frame = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
                    }
                    // Ribbon sizes are WPF device-independent units. A 32px PNG at 72 DPI
                    // otherwise occupies 42.7 units and is clipped by a 32-unit control.
                    int stride = (frame.PixelWidth * frame.Format.BitsPerPixel + 7) / 8;
                    byte[] pixels = new byte[stride * frame.PixelHeight];
                    frame.CopyPixels(pixels, stride, 0);
                    if (iconColor.HasValue)
                    {
                        bool libraryCutouts = !useGrayColor &&
                            resourcePath.StartsWith("SAB.Resources.ExportTypesSingleFileCommand_");
                        // Preserve alpha except for the library glyph's opaque white details:
                        // turn those into negative space, including their antialiased edges.
                        for (int pixel = 0; pixel < pixels.Length; pixel += 4)
                        {
                            if (libraryCutouts)
                            {
                                // The source body is RGB103/108/115; lighter pixels blend toward white.
                                int coverage = System.Math.Min(255 - 115, 255 - System.Math.Max(
                                    pixels[pixel], System.Math.Max(pixels[pixel + 1], pixels[pixel + 2])));
                                pixels[pixel + 3] = (byte)((pixels[pixel + 3] * coverage + 70) / 140);
                            }
                            pixels[pixel] = iconColor.Value.B;
                            pixels[pixel + 1] = iconColor.Value.G;
                            pixels[pixel + 2] = iconColor.Value.R;
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
