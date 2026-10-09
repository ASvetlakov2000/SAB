using System;
using System.Runtime.InteropServices;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;

namespace SAB.MaterialQuantity
{
    internal static class MaterialQuantityUiRefresh
    {
        private static bool _queued;

        internal static void Queue(UIApplication application)
        {
            if (_queued) return;
            _queued = true;
            EventHandler<IdlingEventArgs> handler = null;
            handler = (sender, args) =>
            {
                var ui = sender as UIApplication ?? application;
                try
                {
                    IntPtr handle = ui.MainWindowHandle;
                    var document = ui.ActiveUIDocument?.Document;
                    // Never repaint inside a modal dialog or a document transaction.
                    if (!IsWindowEnabled(handle) || (document != null && document.IsModifiable)) return;

                    application.Idling -= handler;
                    _queued = false;
                    try
                    {
                        if (ui.ActiveUIDocument != null)
                            ui.ActiveUIDocument.RefreshActiveView();
                    }
                    catch (Exception exception)
                    {
                        DiagnosticLog.Write(ui.Application.VersionNumber, "Active view refresh failed: " + exception);
                    }

                    // Equivalent to a window repaint after exposing the desktop;
                    // keeps the user's window position, focus and selection intact.
                    const uint invalidate = 0x0001, erase = 0x0004, allChildren = 0x0080,
                        updateNow = 0x0100, frame = 0x0400;
                    bool refreshed = RedrawWindow(handle, IntPtr.Zero, IntPtr.Zero,
                        invalidate | erase | allChildren | updateNow | frame);
                    DiagnosticLog.Write(ui.Application.VersionNumber, refreshed
                        ? "Deferred UI repaint completed" : "Deferred UI repaint was not completed");
                }
                catch (Exception exception)
                {
                    application.Idling -= handler;
                    _queued = false;
                    DiagnosticLog.Write(application.Application.VersionNumber, "Deferred UI repaint failed: " + exception);
                }
            };
            try
            {
                application.Idling += handler;
            }
            catch (Exception exception)
            {
                _queued = false;
                DiagnosticLog.Write(application.Application.VersionNumber, "Cannot queue UI repaint: " + exception);
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowEnabled(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RedrawWindow(IntPtr window, IntPtr updateRect, IntPtr updateRegion, uint flags);
    }
}
