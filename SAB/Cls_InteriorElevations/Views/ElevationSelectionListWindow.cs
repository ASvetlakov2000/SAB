using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using SAB.InteriorElevations.Models;
using SAB.UI;

namespace SAB.InteriorElevations.Views
{
    /// <summary>Detached values only; no Revit object is retained by this read-only surface.</summary>
    public sealed class ElevationSelectionListItem
    {
        public int Index { get; private set; }
        public string RoomNumber { get; private set; }
        public string RoomName { get; private set; }
        public int LineGroupsCount { get; private set; }
        public int SelectedLinesCount { get; private set; }
        public string StatusText { get; private set; }

        public ElevationSelectionListItem(int index, string number, string name, int groups, int lines, string status)
        {
            Index = index; RoomNumber = number ?? string.Empty; RoomName = name ?? string.Empty;
            LineGroupsCount = groups; SelectedLinesCount = lines; StatusText = status ?? string.Empty;
        }

        public static IList<ElevationSelectionListItem> FromSelections(IEnumerable<MultiRoomSelectionItem> selections)
        {
            if (selections == null) return new List<ElevationSelectionListItem>();
            return selections.Where(row => row != null).Select((row, index) => new ElevationSelectionListItem(
                index + 1, row.RoomNumber, row.RoomName, row.LineGroupsCount, row.SelectedLinesCount, row.StatusText)).ToList();
        }
    }

    public sealed class ElevationSelectionListWindow : Window
    {
        public ReadOnlyCollection<ElevationSelectionListItem> Rows { get; private set; }
        public string Totals { get; private set; }

        public ElevationSelectionListWindow(IEnumerable<ElevationSelectionListItem> rows)
        {
            Rows = new ReadOnlyCollection<ElevationSelectionListItem>((rows ?? new ElevationSelectionListItem[0]).ToList());
            Totals = "Помещений: " + Rows.Count + " · Контуров: " + Rows.Sum(row => row.LineGroupsCount) +
                " · Линий: " + Rows.Sum(row => row.SelectedLinesCount);
            string path = Path.Combine(Path.GetDirectoryName(typeof(ElevationSelectionListWindow).Assembly.Location),
                "Cls_InteriorElevations", "Views", "ElevationSelectionListWindow.xaml");
            using (var stream = File.OpenRead(path))
            {
                var loaded = (Window)XamlReader.Load(stream, new ParserContext { BaseUri = new Uri(path, UriKind.Absolute) });
                Title = loaded.Title; Width = loaded.Width; Height = loaded.Height;
                MinWidth = loaded.MinWidth; MinHeight = loaded.MinHeight;
                FontFamily = loaded.FontFamily; FontSize = loaded.FontSize;
                ResizeMode = loaded.ResizeMode; WindowStartupLocation = WindowStartupLocation.CenterOwner;
                Background = loaded.Background; Resources = loaded.Resources; Content = loaded.Content;
                ((Button)loaded.FindName("DoneButton")).Click += (sender, args) => Close();
            }
            DataContext = this;
            PreviewKeyDown += (sender, args) => { if (args.Key == Key.Escape) { Close(); args.Handled = true; } };
            WindowSizeSettingsService.Apply(this, "InteriorElevations.SelectionListWindow.V1");
        }
    }
}
