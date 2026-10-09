using System.Windows;
using System.Windows.Media;

namespace SAB.ParameterTools
{
    internal static class ParameterToolsIcons
    {
        internal static ImageSource Create(string name, int size)
        {
            var group = new DrawingGroup();
            var ink = new SolidColorBrush(Color.FromRgb(40, 49, 61));
            var accent = new SolidColorBrush(Color.FromRgb(15, 108, 189));
            using (var dc = group.Open())
            {
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, 32, 32));
                var pen = new Pen(ink, 1.8);
                if (name == "Apply" || name == "Preview")
                {
                    dc.DrawImage(Helpers.Ribbon.GetEmbeddedImage("SAB.Resources.ParameterIndex_" + size + ".png"), new Rect(0, 0, 32, 32));
                    if (name == "Apply")
                        dc.DrawGeometry(accent, null, Geometry.Parse("M 19,17 L 26,17 L 26,12 L 32,21 L 26,30 L 26,25 L 19,25 Z"));
                    else
                        dc.DrawGeometry(null, new Pen(accent, 3), Geometry.Parse("M 17,23 L 22,28 L 31,16"));
                }
                else if (name == "Check" || name == "Clear")
                {
                    var resource = name == "Check" ? "ParameterSearch" : "ParameterSearchReset";
                    dc.DrawImage(Helpers.Ribbon.GetEmbeddedImage("SAB.Resources." + resource + "_" + size + ".png"), new Rect(0, 0, 32, 32));
                }
                else if (name == "Corpus")
                {
                    dc.DrawRectangle(null, pen, new Rect(5, 9, 10, 19));
                    dc.DrawRectangle(null, pen, new Rect(17, 4, 10, 24));
                    for (int y = 9; y < 26; y += 5)
                        dc.DrawLine(new Pen(accent, 2), new Point(20, y), new Point(24, y));
                }
                else if (name == "Settings")
                {
                    for (int y = 8; y <= 24; y += 8)
                        dc.DrawLine(pen, new Point(4, y), new Point(28, y));
                    dc.DrawEllipse(accent, null, new Point(11, 8), 3, 3);
                    dc.DrawEllipse(accent, null, new Point(22, 16), 3, 3);
                    dc.DrawEllipse(accent, null, new Point(13, 24), 3, 3);
                }
            }
            group.Transform = new ScaleTransform(size / 32.0, size / 32.0);
            var image = new DrawingImage(group); image.Freeze(); return image;
        }
    }
}
