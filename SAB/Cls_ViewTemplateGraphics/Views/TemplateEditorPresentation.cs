using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SAB.ViewTemplateGraphics.Views
{
    // UI-only adapter. Revit collection and writes remain in the existing services/command.
    public static class TemplateEditorPresentation
    {
        internal static bool PersistenceEnabled { get; set; } = true;
        public static void Attach(Window window)
        {
            window.PreviewKeyDown += (sender, args) =>
            {
                var box = Keyboard.FocusedElement as TextBox;
                if (args.Key != Key.Enter || box == null ||
                    !((box.Tag as string ?? "").Contains("Search") || box.Name.Contains("Search"))) return;
                DependencyObject parent = VisualTreeHelper.GetParent(box);
                while (parent != null && !(parent is Grid)) parent = VisualTreeHelper.GetParent(parent);
                var find = parent == null ? null : Children<Button>(parent).FirstOrDefault(button =>
                    button.Name.StartsWith("Find", StringComparison.Ordinal) || (button.Tag as string ?? "").StartsWith("Find", StringComparison.Ordinal));
                if (find == null) return;
                find.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                args.Handled = true;
            };
        }

        public static void Show(string title, string message)
        {
            var window = Application.Current == null ? null : Application.Current.Windows.OfType<Window>()
                .Where(w => w is ViewTemplateGraphicsWindow || w is AddFiltersWindow || w is GraphicOverrideEditorWindow)
                .OrderBy(w => w.IsActive).LastOrDefault();
            var status = window == null ? null : Children<TextBlock>(window).FirstOrDefault(x => x.Name == "TemplateEditorValidation");
            if (status != null)
            {
                status.Text = message;
                status.Visibility = Visibility.Visible;
                return;
            }
            Autodesk.Revit.UI.TaskDialog.Show(title, message);
        }

        public static IEnumerable<T> Children<T>(DependencyObject node) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            {
                var child = VisualTreeHelper.GetChild(node, i);
                if (child is T item) yield return item;
                foreach (var nested in Children<T>(child)) yield return nested;
            }
        }
    }
}
