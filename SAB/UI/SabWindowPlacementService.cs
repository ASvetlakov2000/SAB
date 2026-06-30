using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace SAB.UI
{
    public static class SabWindowPlacementService
    {
        public static void CenterOnCurrentScreen(Window window)
        {
            if (window == null)
            {
                return;
            }

            try
            {
                if (window.WindowState != WindowState.Normal)
                {
                    return;
                }

                window.UpdateLayout();

                Forms.Screen screen = GetTargetScreen(window);
                Rect workArea = ConvertToDeviceIndependentRect(window, screen.WorkingArea);
                if (workArea.Width <= 0 || workArea.Height <= 0)
                {
                    return;
                }

                double windowWidth = GetActualWindowWidth(window);
                double windowHeight = GetActualWindowHeight(window);

                // Block responsible for keeping restored window size inside current monitor bounds.
                if (windowWidth > workArea.Width && workArea.Width >= window.MinWidth)
                {
                    windowWidth = workArea.Width;
                    window.Width = windowWidth;
                }

                if (windowHeight > workArea.Height && workArea.Height >= window.MinHeight)
                {
                    windowHeight = workArea.Height;
                    window.Height = windowHeight;
                }

                window.Left = workArea.Left + (workArea.Width - windowWidth) / 2.0;
                window.Top = workArea.Top + (workArea.Height - windowHeight) / 2.0;
            }
            catch
            {
                // Window positioning is a visual improvement and must not block the Revit command.
            }
        }

        private static Forms.Screen GetTargetScreen(Window window)
        {
            WindowInteropHelper helper = new WindowInteropHelper(window);
            if (helper.Owner != IntPtr.Zero)
            {
                return Forms.Screen.FromHandle(helper.Owner);
            }

            if (window.Owner != null)
            {
                WindowInteropHelper ownerHelper = new WindowInteropHelper(window.Owner);
                if (ownerHelper.Handle != IntPtr.Zero)
                {
                    return Forms.Screen.FromHandle(ownerHelper.Handle);
                }
            }

            IntPtr mainWindowHandle = IntPtr.Zero;
            try
            {
                mainWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
            }
            catch
            {
                mainWindowHandle = IntPtr.Zero;
            }

            if (mainWindowHandle != IntPtr.Zero)
            {
                return Forms.Screen.FromHandle(mainWindowHandle);
            }

            if (helper.Handle != IntPtr.Zero)
            {
                return Forms.Screen.FromHandle(helper.Handle);
            }

            return Forms.Screen.FromPoint(Forms.Cursor.Position);
        }

        private static Rect ConvertToDeviceIndependentRect(Window window, Drawing.Rectangle pixelRectangle)
        {
            Matrix transform = Matrix.Identity;
            PresentationSource source = PresentationSource.FromVisual(window);
            if (source != null && source.CompositionTarget != null)
            {
                transform = source.CompositionTarget.TransformFromDevice;
            }

            Point topLeft = transform.Transform(new Point(pixelRectangle.Left, pixelRectangle.Top));
            Point bottomRight = transform.Transform(new Point(pixelRectangle.Right, pixelRectangle.Bottom));
            return new Rect(topLeft, bottomRight);
        }

        private static double GetActualWindowWidth(Window window)
        {
            if (IsPositive(window.ActualWidth))
            {
                return window.ActualWidth;
            }

            if (IsPositive(window.Width))
            {
                return window.Width;
            }

            return IsPositive(window.MinWidth) ? window.MinWidth : 100.0;
        }

        private static double GetActualWindowHeight(Window window)
        {
            if (IsPositive(window.ActualHeight))
            {
                return window.ActualHeight;
            }

            if (IsPositive(window.Height))
            {
                return window.Height;
            }

            return IsPositive(window.MinHeight) ? window.MinHeight : 100.0;
        }

        private static bool IsPositive(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0.0;
        }
    }
}
