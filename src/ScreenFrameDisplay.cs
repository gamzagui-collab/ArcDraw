using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace gInk
{
    // Frame windows and the AppBar worker use physical pixels. The rest of
    // ArcDraw still uses the application's existing DPI context.
    internal readonly struct ScreenFrameDpiScope : IDisposable
    {
        private readonly IntPtr previous;

        internal ScreenFrameDpiScope(bool physicalPixels)
        {
            previous = IntPtr.Zero;
            if (!physicalPixels) return;
            try { previous = SetThreadDpiAwarenessContext(new IntPtr(-3)); }
            catch (EntryPointNotFoundException) { }
        }

        public void Dispose()
        {
            if (previous != IntPtr.Zero) SetThreadDpiAwarenessContext(previous);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    }

    internal static class ScreenFrameDisplay
    {
        internal static bool Read(string device, out Rectangle bounds, out Rectangle workingArea)
        {
            Rectangle selected = Rectangle.Empty, selectedWork = Rectangle.Empty;
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr monitor, IntPtr dc, IntPtr rect, IntPtr data) =>
            {
                var info = new MonitorInfo { Size = Marshal.SizeOf(typeof(MonitorInfo)) };
                if (!GetMonitorInfo(monitor, ref info)) return true;
                bool exact = !String.IsNullOrEmpty(device) && String.Equals(device, info.Device, StringComparison.OrdinalIgnoreCase);
                if (exact || selected.IsEmpty || (info.Flags & 1) != 0)
                {
                    selected = info.Monitor.ToRectangle();
                    selectedWork = info.Work.ToRectangle();
                }
                return !exact;
            }, IntPtr.Zero);
            bounds = selected;
            workingArea = selectedWork;
            return !bounds.IsEmpty;
        }

        private delegate bool MonitorCallback(IntPtr monitor, IntPtr dc, IntPtr rect, IntPtr data);

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            internal int Left, Top, Right, Bottom;
            internal Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct MonitorInfo
        {
            internal int Size;
            internal Rect Monitor, Work;
            internal uint Flags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string Device;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorCallback callback, IntPtr data);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    }
}
