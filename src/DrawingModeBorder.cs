using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace gInk
{
    // Four tiny alpha strips around the actual target, never a full-screen bitmap.
    // They are outside Ink and accept neither pointer input nor activation.
    internal sealed class DrawingModeBorder : IDisposable
    {
        private const int Thickness=12;
        private readonly Root root;
        private readonly List<EdgeWindow> edges = new List<EdgeWindow>();
        private bool visible;
        private bool updating;
        private WhiteboardLayerForm trackedNote;
        private readonly List<Rectangle> frames=new List<Rectangle>();
        internal DrawingModeBorder(Root root) { this.root = root; }
        internal void Update(bool show)
        {
            if(updating||(!show&&!visible))return;
            updating=true;
            try
            {
                if(!show){visible=false;Watch(null);foreach(EdgeWindow edge in edges)edge.Hide();return;}
                WhiteboardLayerForm note=root.Whiteboards==null?null:root.Whiteboards.ActiveNote;
                if(note!=null&&(note.IsDisposed||!note.Visible))note=null;
                bool targetChanged=trackedNote!=note;Watch(note);
                if (note != null && !note.ShowDrawingFrame)
                {
                    if (visible) foreach (EdgeWindow edge in edges) edge.Hide();
                    visible = false;
                    return;
                }
                var next=new List<Rectangle>();
                if(note!=null)next.Add(note.Bounds);else foreach(Screen screen in Screen.AllScreens)next.Add(screen.Bounds);
                bool changed=!visible||targetChanged||frames.Count!=next.Count;
                if(!changed)for(int i=0;i<next.Count;i++)if(frames[i]!=next[i]){changed=true;break;}
                if(!changed)return;
                visible=true;frames.Clear();frames.AddRange(next);
                while(edges.Count>next.Count*4){int last=edges.Count-1;edges[last].Dispose();edges.RemoveAt(last);}
                Form owner=note==null?(Form)root.FormCollection:note;
                for(int i=0;i<next.Count;i++)
                {
                    Rectangle r=next[i];Rectangle[] strips={new Rectangle(r.Left,r.Top,r.Width,Thickness),new Rectangle(r.Left,r.Bottom-Thickness,r.Width,Thickness),
                        new Rectangle(r.Left,r.Top+Thickness,Thickness,Math.Max(1,r.Height-2*Thickness)),new Rectangle(r.Right-Thickness,r.Top+Thickness,Thickness,Math.Max(1,r.Height-2*Thickness))};
                    for(int side=0;side<4;side++)
                    {
                        int index=i*4+side;
                        if(index==edges.Count)edges.Add(new EdgeWindow(strips[side],side));
                        else if(edges[index].IsDisposed)edges[index]=new EdgeWindow(strips[side],side);
                        EdgeWindow edge=edges[index];if(edge.Owner!=owner){edge.Owner=null;edge.Owner=owner;}
                        edge.Place(strips[side]);if(!edge.Visible)edge.Show();edge.RaiseWithoutFocus();
                    }
                }
            }
            finally{updating=false;}
        }
        private void Watch(WhiteboardLayerForm note)
        {
            if(trackedNote==note)return;
            if(trackedNote!=null){trackedNote.LocationChanged-=TargetChanged;trackedNote.SizeChanged-=TargetChanged;trackedNote.VisibleChanged-=TargetChanged;trackedNote.Disposed-=TargetChanged;}
            trackedNote=note;
            if(note!=null){note.LocationChanged+=TargetChanged;note.SizeChanged+=TargetChanged;note.VisibleChanged+=TargetChanged;note.Disposed+=TargetChanged;}
        }
        private void TargetChanged(object sender,EventArgs e){if(visible)Update(true);}
        public void Dispose() { visible=false;Watch(null);foreach (EdgeWindow edge in edges) edge.Dispose(); edges.Clear(); }

        private sealed class EdgeWindow : Form
        {
            private readonly int side;
            private Rectangle anchor;
            private bool pixelsReady;
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
            internal void Place(Rectangle bounds)
            {
                if(!IsHandleCreated){IntPtr created=Handle;}
                Size old=Size;anchor=bounds;Bounds=bounds;
                if(side==1)Top=anchor.Bottom-Height;if(side==3)Left=anchor.Right-Width;
                if(!pixelsReady||old!=Size){SetPixels();pixelsReady=true;}
            }
            internal void RaiseWithoutFocus(){SetWindowPos(Handle,new IntPtr(-1),0,0,0,0,0x1|0x2|0x10);}
            internal void SetPixels()
            {
                // Anchor the far edges even if Windows adjusts the native size.
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
                        for (int i = 0; i < Thickness; i++)
                        using (var pen = new Pen(Color.FromArgb(alpha[i/3], 55, 218, 255)))
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
            [DllImport("user32.dll")]private static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
        }
    }
}
