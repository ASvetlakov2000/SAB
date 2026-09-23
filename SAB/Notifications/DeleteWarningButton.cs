using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;

namespace SAB.Notifications
{
    // DocWarnDialog treats an API override differently from its native OK button.
    // Arm only after recognizing the current deletion warning, never for arbitrary dialogs.
    internal sealed class DeleteWarningButton : IDisposable
    {
        readonly DispatcherTimer timer = new DispatcherTimer();
        readonly HashSet<IntPtr> existing = new HashSet<IntPtr>();
        IntPtr owner;
        uint thread;
        DateTime deadline;
        Action<bool> complete;
        public DeleteWarningButton() { timer.Interval = TimeSpan.FromMilliseconds(50); timer.Tick += Tick; }
        public void Arm(IntPtr mainWindow, Action<bool> callback)
        {
            Cancel(); owner = mainWindow;
            uint pid; thread = GetWindowThreadProcessId(owner, out pid);
            if (thread == 0) { callback(false); return; }
            EnumThreadWindows(thread,(h,p) => { if (IsWindowVisible(h)) existing.Add(h); return true; },IntPtr.Zero);
            complete = callback; deadline = DateTime.UtcNow.AddSeconds(5); timer.Start();
        }
        void Tick(object sender, EventArgs e)
        {
            IntPtr button = IntPtr.Zero;
            EnumThreadWindows(thread,(h,p) =>
            {
                if (existing.Contains(h) || !IsWindowVisible(h) || GetWindow(h,4) != owner) return true;
                IntPtr ok = GetDlgItem(h,1), cancel = GetDlgItem(h,2);
                string caption = Text(ok).Replace("&", "").Trim();
                if (ok == IntPtr.Zero || cancel == IntPtr.Zero || !IsWindowEnabled(ok) || (caption != "OK" && caption != "ОК")) return true;
                bool expand = false, show = false;
                EnumChildWindows(h,(child,param) => {
                    string text = Text(child).Replace("&", "");
                    expand |= text.StartsWith("Развернуть") || text.StartsWith("Expand");
                    show |= text == "Показать" || text == "Show";
                    return true;
                },IntPtr.Zero);
                if (expand && show) { button = ok; return false; }
                return true;
            },IntPtr.Zero);
            if (button != IntPtr.Zero)
            {
                var callback = complete; Cancel();
                // Posting avoids executing a Revit operation within this timer callback.
                bool posted = PostMessage(button,0x00F5,IntPtr.Zero,IntPtr.Zero); // BM_CLICK
                callback?.Invoke(posted);
            }
            else if (DateTime.UtcNow >= deadline) { var callback = complete; Cancel(); callback?.Invoke(false); }
        }
        public void Cancel() { timer.Stop(); complete = null; existing.Clear(); }
        public void Dispose() { Cancel(); timer.Tick -= Tick; }
        static string Text(IntPtr h) { var text = new StringBuilder(512); GetWindowText(h,text,text.Capacity); return text.ToString(); }
        delegate bool EnumWindow(IntPtr h, IntPtr p);
        [DllImport("user32.dll")] static extern bool EnumThreadWindows(uint thread,EnumWindow callback,IntPtr p);
        [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr h,EnumWindow callback,IntPtr p);
        [DllImport("user32.dll")] static extern IntPtr GetDlgItem(IntPtr h,int id);
        [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h,uint command);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern bool IsWindowEnabled(IntPtr h);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h,StringBuilder text,int size);
        [DllImport("user32.dll")] static extern bool PostMessage(IntPtr h,uint message,IntPtr w,IntPtr l);
    }
}
