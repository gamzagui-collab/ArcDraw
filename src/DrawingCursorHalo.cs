using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace gInk
{
    // Visual feedback only: never changes Ink cursors, stroke data or system
    // pointer settings. The hollow, click-through window follows the hotspot.
    internal sealed class DrawingCursorHalo : IDisposable
    {
        private readonly Root root;
        private readonly Timer timer;
        private HaloWindow window;
        private Point lastPoint;
        private bool positioned;
        internal DrawingCursorHalo(Root root)
        {
            this.root=root;timer=new Timer {Interval=16};timer.Tick+=delegate {Update();};
        }
        internal void Start(){timer.Start();}
        internal void Stop(){timer.Stop();if(window!=null)window.Hide();}
        private bool CanShow(Point point)
        {
            DrawingSession session=root.DrawingSession;
            if(session==null||!session.DrawingEnabled||session.Editing||root.PointerMode||root.EraserMode||root.PanMode||root.LassoMode||root.Snapping!=0||root.UponTakingSnap)return false;
            if(root.ToolSelected==Tools.txtLeftAligned||root.ToolSelected==Tools.txtRightAligned)return false;
            FormCollection collection=root.FormCollection;
            if(collection==null||!collection.Visible||!collection.Bounds.Contains(point))return false;
            if(collection.gpButtons.RectangleToScreen(collection.gpButtons.ClientRectangle).Contains(point))return false;
            if(root.Whiteboards!=null)
            {
                Control bar=root.Whiteboards.ToolbarSurface;
                if(bar.Visible&&bar.RectangleToScreen(bar.ClientRectangle).Contains(point))return false;
                foreach(WhiteboardLayerForm note in root.Whiteboards.Notes)
                    if(note.Visible&&note.Bounds.Contains(point))return note.IsDrawingSurfaceAt(point);
            }
            Control hit=Control.FromHandle(WindowFromPoint(point));
            if(hit!=null)
            {
                Form form=hit.FindForm();
                if(form!=collection&&form!=root.FormDisplay&&form!=window)return false;
            }
            return true;
        }
        private void Update()
        {
            Point point=Cursor.Position;
            if(!CanShow(point)){if(window!=null&&window.Visible)window.Hide();positioned=false;return;}
            if(window==null||window.IsDisposed||!window.Visible)
            {
                IntPtr foreground=GetForegroundWindow();
                if(window==null||window.IsDisposed)window=new HaloWindow();
                // Visual overlays must not become the collector's active owned popup.
                window.Show();window.SetPixels(point);
                if(foreground!=IntPtr.Zero&&GetForegroundWindow()!=foreground)SetForegroundWindow(foreground);
            }
            if(!positioned||point!=lastPoint){window.MoveTo(point);lastPoint=point;positioned=true;}
        }
        internal static Bitmap CreateHaloBitmap()
        {
            var bitmap=new Bitmap(48,48,PixelFormat.Format32bppPArgb);
            using(Graphics g=Graphics.FromImage(bitmap))
            {
                g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;
                RectangleF circle=new RectangleF(13,13,22,22);
                using(var pen=new Pen(Color.FromArgb(38,0,200,255),10))g.DrawEllipse(pen,circle);
                using(var pen=new Pen(Color.FromArgb(85,0,220,255),6))g.DrawEllipse(pen,circle);
                using(var pen=new Pen(Color.FromArgb(175,0,79,107),3.5f))g.DrawEllipse(pen,circle);
                using(var pen=new Pen(Color.FromArgb(255,50,234,255),1.8f))g.DrawEllipse(pen,circle);
            }
            return bitmap;
        }
        public void Dispose(){timer.Dispose();if(window!=null)window.Dispose();}
        [DllImport("user32.dll")]private static extern IntPtr WindowFromPoint(Point point);
        [DllImport("user32.dll")]private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]private static extern bool SetForegroundWindow(IntPtr window);

        private sealed class HaloWindow : Form
        {
            internal HaloWindow()
            {
                Name="DrawingCursorHalo";Text="ppInk drawing cursor";FormBorderStyle=FormBorderStyle.None;
                StartPosition=FormStartPosition.Manual;Size=new Size(48,48);Location=new Point(-10000,-10000);
                Enabled=false;TabStop=false;ShowInTaskbar=false;TopMost=true;
            }
            protected override void WndProc(ref Message m)
            {
                if(m.Msg==0x84){m.Result=new IntPtr(-1);return;}
                if(m.Msg==0x21){m.Result=new IntPtr(3);return;}
                base.WndProc(ref m);
            }
            protected override bool ShowWithoutActivation {get{return true;}}
            protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x80000|0x08000000|0x20|0x80;return p;}}
            internal void MoveTo(Point point){SetWindowPos(Handle,new IntPtr(-1),point.X-24,point.Y-24,48,48,0x10|0x1|0x4);}
            internal void SetPixels(Point point)
            {
                IntPtr screen=GetDC(IntPtr.Zero),memory=CreateCompatibleDC(screen),bitmap=IntPtr.Zero,previous=IntPtr.Zero;
                try
                {
                    using(Bitmap image=CreateHaloBitmap())bitmap=image.GetHbitmap(Color.FromArgb(0));
                    previous=SelectObject(memory,bitmap);
                    POINT target=new POINT {X=point.X-24,Y=point.Y-24},origin=new POINT();SIZE size=new SIZE {Width=48,Height=48};
                    BLEND blend=new BLEND {Alpha=255,Format=1};
                    if(!UpdateLayeredWindow(Handle,screen,ref target,ref size,memory,ref origin,0,ref blend,2))
                        throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }
                finally
                {
                    if(previous!=IntPtr.Zero)SelectObject(memory,previous);if(bitmap!=IntPtr.Zero)DeleteObject(bitmap);
                    DeleteDC(memory);ReleaseDC(IntPtr.Zero,screen);
                }
            }
            [StructLayout(LayoutKind.Sequential)]private struct POINT {public int X,Y;}
            [StructLayout(LayoutKind.Sequential)]private struct SIZE {public int Width,Height;}
            [StructLayout(LayoutKind.Sequential,Pack=1)]private struct BLEND {public byte Op,Flags,Alpha,Format;}
            [DllImport("user32.dll")]private static extern IntPtr GetDC(IntPtr window);
            [DllImport("user32.dll")]private static extern int ReleaseDC(IntPtr window,IntPtr dc);
            [DllImport("gdi32.dll")]private static extern IntPtr CreateCompatibleDC(IntPtr dc);
            [DllImport("gdi32.dll")]private static extern IntPtr SelectObject(IntPtr dc,IntPtr value);
            [DllImport("gdi32.dll")]private static extern bool DeleteObject(IntPtr value);
            [DllImport("gdi32.dll")]private static extern bool DeleteDC(IntPtr dc);
            [DllImport("user32.dll",SetLastError=true)]private static extern bool UpdateLayeredWindow(IntPtr window,IntPtr screen,ref POINT target,ref SIZE size,IntPtr source,ref POINT origin,int key,ref BLEND blend,int flags);
            [DllImport("user32.dll")]private static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
        }
    }
}
