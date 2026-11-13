using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace gInk
{
    // The shell owns the reservation. Never write a global work-area rectangle:
    // other monitors, the taskbar and other AppBars keep their own space.
    internal sealed class ScreenFrameAppBars : IDisposable
    {
        internal const string WorkerArgument = "--arc-frame-appbar-worker";
        private readonly Action changed;
        private readonly Action<bool> fullscreenChanged;
        private Process worker;
        private NotifyWindow notifications;

        internal ScreenFrameAppBars(Action changed, Action<bool> fullscreenChanged)
        {
            this.changed = changed;
            this.fullscreenChanged = fullscreenChanged;
        }

        internal bool Reserve(Rectangle target, Padding inset, out Rectangle outer)
        {
            outer = Rectangle.Empty;
            try
            {
                if (worker == null || worker.HasExited)
                {
                    Release();
                    if (notifications == null) notifications = new NotifyWindow(changed, fullscreenChanged);
                    var start = new ProcessStartInfo(typeof(ScreenFrameAppBars).Assembly.Location,
                        WorkerArgument + " " + notifications.Handle.ToInt64().ToString(CultureInfo.InvariantCulture))
                    {
                        UseShellExecute = false, CreateNoWindow = true,
                        RedirectStandardInput = true, RedirectStandardOutput = true
                    };
                    worker = Process.Start(start);
                    worker.StandardInput.AutoFlush = true;
                }
                worker.StandardInput.WriteLine(FormattableString.Invariant(
                    $"{target.X} {target.Y} {target.Width} {target.Height} {inset.Left} {inset.Top} {inset.Right} {inset.Bottom}"));
                Task<string> read = worker.StandardOutput.ReadLineAsync();
                if (read.Wait(1500) && read.Result != null)
                {
                    string[] values = read.Result.Split(' ');
                    if (values.Length == 4 && values.All(value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
                    {
                        int[] bounds = values.Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray();
                        outer = new Rectangle(bounds[0], bounds[1], bounds[2], bounds[3]);
                        return true;
                    }
                }
            }
            catch (Exception error) when (error is IOException || error is InvalidOperationException ||
                error is System.ComponentModel.Win32Exception || error is AggregateException) { }
            Release();
            return false;
        }

        internal void Release()
        {
            Process process = worker;
            worker = null;
            if (process == null) return;
            try { process.StandardInput.Close(); }
            catch (IOException) { }
            // Closing the pipe wakes the worker even if ArcDraw exits abruptly.
            process.WaitForExit(1500);
            process.Dispose();
        }

        public void Dispose()
        {
            Release();
            notifications?.DestroyHandle();
            notifications = null;
        }

        internal static void RunWorker(string destination)
        {
            if (!long.TryParse(destination, NumberStyles.Integer, CultureInfo.InvariantCulture, out long raw)) return;
            var parent = new IntPtr(raw);
            using (var dpi = new ScreenFrameDpiScope(true))
            using (var dispatcher = new Control())
            using (var bars = new ShellAppBars(
                () => PostMessage(parent, NotifyWindow.Callback, IntPtr.Zero, IntPtr.Zero),
                active => PostMessage(parent, NotifyWindow.Callback, new IntPtr(active ? 1 : 2), IntPtr.Zero)))
            {
                IntPtr handle = dispatcher.Handle;
                Task.Run(() =>
                {
                    try
                    {
                        string line;
                        while ((line = Console.In.ReadLine()) != null)
                        {
                            string command = line;
                            dispatcher.Invoke(new Action(() =>
                            {
                                string[] values = command.Split(' ');
                                string result = "failed";
                                if (values.Length == 8 && values.All(value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
                                {
                                    int[] data = values.Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray();
                                    var inset = new Padding(data[4], data[5], data[6], data[7]);
                                    if (bars.Reserve(new Rectangle(data[0], data[1], data[2], data[3]), inset, out Rectangle outer))
                                        result = FormattableString.Invariant($"{outer.X} {outer.Y} {outer.Width} {outer.Height}");
                                }
                                Console.Out.WriteLine(result);
                                Console.Out.Flush();
                            }));
                        }
                    }
                    catch (Exception error) when (error is IOException || error is InvalidOperationException ||
                        error is System.Reflection.TargetInvocationException) { }
                    finally
                    {
                        try { dispatcher.BeginInvoke(new Action(Application.ExitThread)); }
                        catch (InvalidOperationException) { }
                    }
                });
                Application.Run();
            }
        }

        private sealed class NotifyWindow : NativeWindow
        {
            internal const int Callback = 0x8002;
            private readonly Action changed;
            private readonly Action<bool> fullscreenChanged;

            internal NotifyWindow(Action changed, Action<bool> fullscreenChanged)
            {
                this.changed = changed;
                this.fullscreenChanged = fullscreenChanged;
                CreateHandle(new CreateParams { Parent = new IntPtr(-3) });
            }

            protected override void WndProc(ref Message message)
            {
                if (message.Msg == Callback)
                {
                    if (message.WParam == IntPtr.Zero) changed();
                    else fullscreenChanged(message.WParam == new IntPtr(1));
                }
                base.WndProc(ref message);
            }
        }

        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    }

    internal sealed class ShellAppBars : IDisposable
    {
        private readonly Edge[] edges = new Edge[4];
        private readonly Action changed;
        private readonly Action<bool> fullscreenChanged;
        private Rectangle monitor;
        private bool ready;

        internal ShellAppBars(Action changed, Action<bool> fullscreenChanged)
        {
            this.changed = changed;
            this.fullscreenChanged = fullscreenChanged;
        }

        internal bool Reserve(Rectangle target, Padding inset, out Rectangle outer)
        {
            outer = Rectangle.Empty;
            if (monitor != target) Release();
            monitor = target;
            ready = false;
            // Process top and bottom before the sides so the bars meet at the corners.
            int[] widths = { inset.Left, inset.Top, inset.Right, inset.Bottom };
            foreach (int side in new[] { 1, 3, 0, 2 })
            {
                if (edges[side] == null) edges[side] = new Edge(side, target.Location, Notify, fullscreenChanged);
                if (!edges[side].Reserve(target, widths[side]))
                {
                    Release();
                    return false;
                }
            }
            outer = Rectangle.FromLTRB(edges[0].Bounds.Left, edges[1].Bounds.Top, edges[2].Bounds.Right, edges[3].Bounds.Bottom);
            if (outer.Width <= inset.Horizontal || outer.Height <= inset.Vertical)
            {
                Release();
                return false;
            }
            ready = true;
            return true;
        }

        private void Notify()
        {
            if (ready) changed();
        }

        internal void Release()
        {
            ready = false;
            for (int i = 0; i < edges.Length; i++)
            {
                edges[i]?.Dispose();
                edges[i] = null;
            }
            monitor = Rectangle.Empty;
        }

        public void Dispose() => Release();

        private sealed class Edge : NativeWindow, IDisposable
        {
            private const int New = 0, Remove = 1, QueryPosition = 2, SetPosition = 3, WindowPositionChanged = 9;
            private const int PositionChanged = 1, FullscreenApp = 2;
            private const int Callback = 0x8001, WmWindowPosChanged = 0x0047;
            private static readonly int TaskbarCreated = RegisterWindowMessage("TaskbarCreated");
            private readonly int side;
            private readonly Action changed;
            private readonly Action<bool> fullscreenChanged;
            private bool registered;
            internal Rectangle Bounds { get; private set; }

            internal Edge(int side, Point location, Action changed, Action<bool> fullscreenChanged)
            {
                this.side = side;
                this.changed = changed;
                this.fullscreenChanged = fullscreenChanged;
                CreateHandle(new CreateParams
                {
                    Caption = "ArcDraw work area reservation",
                    Style = unchecked((int)0x80000000), // WS_POPUP, deliberately hidden
                    ExStyle = 0x08000080, // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
                    X = location.X, Y = location.Y, Width = 1, Height = 1
                });
            }

            internal bool Reserve(Rectangle monitor, int width)
            {
                var data = Data();
                if (!registered)
                {
                    data.CallbackMessage = Callback;
                    if (SHAppBarMessage(New, ref data) == UIntPtr.Zero) return false;
                    registered = true;
                }
                data.Bounds = new NativeRect(monitor);
                if (SHAppBarMessage(QueryPosition, ref data) == UIntPtr.Zero) return false;
                switch (side)
                {
                    case 0: data.Bounds.Right = data.Bounds.Left + width; break;
                    case 1: data.Bounds.Bottom = data.Bounds.Top + width; break;
                    case 2: data.Bounds.Left = data.Bounds.Right - width; break;
                    case 3: data.Bounds.Top = data.Bounds.Bottom - width; break;
                }
                Rectangle requested = data.Bounds.ToRectangle();
                if (requested == Bounds) return true;
                if (SHAppBarMessage(SetPosition, ref data) == UIntPtr.Zero) return false;
                Bounds = data.Bounds.ToRectangle();
                return SetWindowPos(Handle, IntPtr.Zero, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height, 0x0014);
            }

            protected override void WndProc(ref Message message)
            {
                if (message.Msg == Callback)
                {
                    switch (message.WParam.ToInt32())
                    {
                        case PositionChanged: changed(); break;
                        case FullscreenApp:
                            if (side == 1) fullscreenChanged(message.LParam != IntPtr.Zero);
                            break;
                    }
                }
                else if (message.Msg == TaskbarCreated && TaskbarCreated != 0)
                {
                    registered = false;
                    Bounds = Rectangle.Empty;
                    changed();
                }
                else if (message.Msg == WmWindowPosChanged && registered)
                {
                    var data = Data();
                    SHAppBarMessage(WindowPositionChanged, ref data);
                }
                base.WndProc(ref message);
            }

            private AppBarData Data() => new AppBarData
            {
                Size = (uint)Marshal.SizeOf(typeof(AppBarData)), Window = Handle, Edge = (uint)side
            };

            public void Dispose()
            {
                if (registered)
                {
                    registered = false;
                    var data = Data();
                    SHAppBarMessage(Remove, ref data);
                }
                if (Handle != IntPtr.Zero) DestroyHandle();
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AppBarData
        {
            internal uint Size;
            internal IntPtr Window;
            internal uint CallbackMessage, Edge;
            internal NativeRect Bounds;
            internal IntPtr Parameter;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            internal int Left, Top, Right, Bottom;
            internal NativeRect(Rectangle rect) { Left = rect.Left; Top = rect.Top; Right = rect.Right; Bottom = rect.Bottom; }
            internal Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
        }

        [DllImport("shell32.dll")] private static extern UIntPtr SHAppBarMessage(int message, ref AppBarData data);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int RegisterWindowMessage(string name);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    }
}
