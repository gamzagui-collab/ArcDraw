using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace gInk
{
    // Four tiny static alpha strips per display, never a full-screen bitmap.
    // They are outside Ink and accept neither pointer input nor activation.
    internal sealed class DrawingModeBorder : IDisposable
    {
        private readonly Root root;
        private readonly List<EdgeWindow> edges = new List<EdgeWindow>();
        private bool visible;
        internal DrawingModeBorder(Root root) { this.root = root; }
        internal void Update(bool show)
        {
            if (visible == show) return;
            visible = show;
            if (!show) { foreach (EdgeWindow edge in edges) edge.Hide(); return; }
            foreach (EdgeWindow edge in edges) edge.Dispose();
            edges.Clear();
            foreach (Screen screen in Screen.AllScreens)
            {
                Rectangle r = screen.Bounds;
                Add(new Rectangle(r.Left, r.Top, r.Width, 4), 0);
                Add(new Rectangle(r.Left, r.Bottom - 4, r.Width, 4), 1);
                Add(new Rectangle(r.Left, r.Top + 4, 4, Math.Max(1, r.Height - 8)), 2);
                Add(new Rectangle(r.Right - 4, r.Top + 4, 4, Math.Max(1, r.Height - 8)), 3);
            }
        }
        private void Add(Rectangle bounds, int side)
        {
            var edge = new EdgeWindow(bounds, side);
            edges.Add(edge); edge.Show(); edge.SetPixels();
        }
        public void Dispose() { foreach (EdgeWindow edge in edges) edge.Dispose(); edges.Clear(); }

        private sealed class EdgeWindow : Form
        {
            private readonly int side;
            private readonly Rectangle anchor;
            internal EdgeWindow(Rectangle bounds, int side)
            {
                this.side = side; this.anchor = bounds; Name = "ArcDrawDrawingBorder"; Text = "ArcDraw drawing mode";
                FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual;
                AutoScaleMode = AutoScaleMode.None; Bounds = bounds; Enabled = false; TabStop = false; ShowInTaskbar = false; TopMost = true;
            }
            protected override bool ShowWithoutActivation { get { return true; } }
            protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x80000 | 0x08000000 | 0x20 | 0x80; return p; } }
            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x84) { m.Result = new IntPtr(-1); return; }
                if (m.Msg == 0x21) { m.Result = new IntPtr(3); return; }
                base.WndProc(ref m);
            }
            internal void SetPixels()
            {
                // WinForms may enforce a 6px native minimum at the current scale.
                // Only four alpha rows are painted; keep the bright edge on-screen.
                if(side==1)Top=anchor.Bottom-Height;
                if(side==3)Left=anchor.Right-Width;
                IntPtr screen = GetDC(IntPtr.Zero), memory = CreateCompatibleDC(screen), bitmap = IntPtr.Zero, previous = IntPtr.Zero;
                try
                {
                    using (var pixels = new Bitmap(Width, Height, PixelFormat.Format32bppPArgb))
                    using (Graphics g = Graphics.FromImage(pixels))
                    {
                        g.Clear(Color.Transparent);
                        int[] alpha = { 150, 90, 42, 14 };
                        for (int i = 0; i < 4; i++)
                        using (var pen = new Pen(Color.FromArgb(alpha[i], 55, 218, 255)))
                        {
                            if (side < 2) { int y = side == 0 ? i : Height - 1 - i; g.DrawLine(pen, 0, y, Width - 1, y); }
                            else { int x = side == 2 ? i : Width - 1 - i; g.DrawLine(pen, x, 0, x, Height - 1); }
                        }
                        bitmap = pixels.GetHbitmap(Color.FromArgb(0));
                    }
                    previous = SelectObject(memory, bitmap);
                    POINT target = new POINT { X = Left, Y = Top }, origin = new POINT();
                    SIZE size = new SIZE { Width = Width, Height = Height };
                    BLEND blend = new BLEND { Alpha = 255, Format = 1 };
                    if (!UpdateLayeredWindow(Handle, screen, ref target, ref size, memory, ref origin, 0, ref blend, 2))
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }
                finally
                {
                    if (previous != IntPtr.Zero) SelectObject(memory, previous);
                    if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
                    DeleteDC(memory); ReleaseDC(IntPtr.Zero, screen);
                }
            }
            [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
            [StructLayout(LayoutKind.Sequential)] private struct SIZE { public int Width, Height; }
            [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct BLEND { public byte Op, Flags, Alpha, Format; }
            [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
            [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
            [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
            [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
            [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
            [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
            [DllImport("user32.dll", SetLastError = true)] private static extern bool UpdateLayeredWindow(IntPtr window, IntPtr screen, ref POINT target, ref SIZE size, IntPtr source, ref POINT origin, int key, ref BLEND blend, int flags);
        }
    }
}
