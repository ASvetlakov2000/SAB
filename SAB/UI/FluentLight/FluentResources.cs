using System;
using System.IO;
using System.Windows;

namespace SAB.UI.FluentLight
{
    /// <summary>Loads the shared on-disk dictionary from compiled as well as loose XAML.</summary>
    public sealed class FluentResourceDictionary : ResourceDictionary
    {
        public FluentResourceDictionary() { MergedDictionaries.Add(FluentResources.CreateDictionary()); }
    }

    /// <summary>Scoped resources for migrated SAB windows. Never changes Revit application resources.</summary>
    public static class FluentResources
    {
        private static ResourceDictionary _resources;

        public static ResourceDictionary CreateDictionary()
        {
            var path = Path.Combine(Path.GetDirectoryName(typeof(FluentResources).Assembly.Location),
                "UI", "Styles", "SABFluentLight.xaml");
            return new ResourceDictionary { Source = new Uri(path, UriKind.Absolute) };
        }

        // Some controls are assembled before attachment to a Window. Resolve their explicit styles
        // against this dictionary rather than Application.Current (owned by Revit).
        public static object Find(object key)
        {
            if (_resources == null) _resources = CreateDictionary();
            return _resources[key];
        }
    }
}
