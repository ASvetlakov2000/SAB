using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SAB.SyncReminder
{
    internal static class SyncReminderWindowUtils
    {
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect lpRect);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        public static IntPtr GetRevitMainWindowHandle()
        {
            Process process = Process.GetCurrentProcess();
            return process.MainWindowHandle;
        }

        public static bool TryGetWindowBounds(IntPtr windowHandle, out Rect bounds)
        {
            bounds = Rect.Empty;

            if (windowHandle == IntPtr.Zero)
            {
                return false;
            }

            if (!IsWindowVisible(windowHandle) || IsIconic(windowHandle))
            {
                return false;
            }

            NativeRect nativeRect;
            if (!GetWindowRect(windowHandle, out nativeRect))
            {
                return false;
            }

            Point topLeft = ConvertPixelsToDip(windowHandle, new Point(nativeRect.Left, nativeRect.Top));
            Point bottomRight = ConvertPixelsToDip(windowHandle, new Point(nativeRect.Right, nativeRect.Bottom));

            double width = bottomRight.X - topLeft.X;
            double height = bottomRight.Y - topLeft.Y;

            if (width <= 0 || height <= 0)
            {
                return false;
            }

            bounds = new Rect(topLeft.X, topLeft.Y, width, height);
            return true;
        }

        public static bool CanShowOverlayForRevit(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                return false;
            }

            if (!IsWindowVisible(windowHandle) || IsIconic(windowHandle))
            {
                return false;
            }

            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
            {
                return true;
            }

            uint foregroundProcessId;
            GetWindowThreadProcessId(foregroundWindow, out foregroundProcessId);

            uint currentProcessId = (uint)Process.GetCurrentProcess().Id;
            return foregroundProcessId == currentProcessId;
        }

        public static void MakeWindowNoActivate(Window window)
        {
            if (window == null)
            {
                return;
            }

            WindowInteropHelper helper = new WindowInteropHelper(window);
            IntPtr handle = helper.Handle;
            if (handle == IntPtr.Zero)
            {
                return;
            }

            int extendedStyle = GetExtendedStyle(handle);
            extendedStyle = extendedStyle | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
            SetExtendedStyle(handle, extendedStyle);
        }

        private static Point ConvertPixelsToDip(IntPtr windowHandle, Point point)
        {
            HwndSource source = HwndSource.FromHwnd(windowHandle);
            if (source == null || source.CompositionTarget == null)
            {
                return point;
            }

            return source.CompositionTarget.TransformFromDevice.Transform(point);
        }

        private static int GetExtendedStyle(IntPtr handle)
        {
            if (IntPtr.Size == 8)
            {
                return GetWindowLongPtr64(handle, GWL_EXSTYLE).ToInt32();
            }

            return GetWindowLong32(handle, GWL_EXSTYLE);
        }

        private static void SetExtendedStyle(IntPtr handle, int value)
        {
            if (IntPtr.Size == 8)
            {
                SetWindowLongPtr64(handle, GWL_EXSTYLE, new IntPtr(value));
                return;
            }

            SetWindowLong32(handle, GWL_EXSTYLE, value);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;

            public int Top;

            public int Right;

            public int Bottom;
        }
    }
}
