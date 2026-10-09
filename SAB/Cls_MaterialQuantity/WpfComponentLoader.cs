using System;
using System.Windows;

namespace SAB.Helpers
{
    internal static class WpfComponentLoader
    {
        internal static void Load(object component, string resourcePath)
        {
            // Revit can load more than one SAB build. A name-only pack URI can
            // then resolve to a different assembly than the window's assembly.
            var assemblyName = component.GetType().Assembly.GetName();
            var uri = new Uri("/" + assemblyName.Name + ";v" + assemblyName.Version +
                ";component/" + resourcePath, UriKind.Relative);
            Application.LoadComponent(component, uri);
        }
    }
}
