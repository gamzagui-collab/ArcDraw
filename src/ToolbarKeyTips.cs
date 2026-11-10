using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace gInk
{
    // A single non-activating, click-through window owns the momentary key tips.
    // It never changes button geometry, keyboard bindings or application focus.
    internal sealed class ToolbarKeyTips : IDisposable
    {
        private readonly Root root;
        private KeyTipWindow window;
        internal ToolbarKeyTips(Root root){this.root=root;}
        internal void Hide(){if(window!=null&&window.Visible)window.Hide();}
        internal void Update(bool show)
        {
            if(!show||root.FormCollection==null||!root.FormCollection.Visible){Hide();return;}
            var buttons=new List<Button>();Collect(root.FormCollection.gpButtons,buttons);
            if(root.Whiteboards!=null)Collect(root.Whiteboards.ToolbarSurface,buttons);
            var tags=new List<KeyTip>();
            using(var font=new Font("Segoe UI",12f,FontStyle.Bold,GraphicsUnit.Pixel))
            foreach(Button button in buttons)
            {
                string text=ToolbarDesign.HintFor(button);if(text=="")continue;
                if(text=="⇧Del")text="Shift+Del";
                Size size=TextRenderer.MeasureText(text,font,new Size(Int32.MaxValue,30),TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
                size=new Size(Math.Max(24,size.Width+14),size.Height+8);
                Point anchor=button.PointToScreen(new Point(button.Width/2,button.Height-3));
                Rectangle work=Screen.FromPoint(anchor).WorkingArea;
                int x=Math.Max(work.Left,Math.Min(anchor.X-size.Width/2,work.Right-size.Width-3));
                int y=anchor.Y;
                Rectangle target=new Rectangle(new Point(x,y),size);
                for(int attempt=0;attempt<12;attempt++)
                {
                    bool collision=false;Rectangle padded=Rectangle.Inflate(target,2,2);
                    foreach(KeyTip placed in tags)if(padded.IntersectsWith(placed.Bounds)){collision=true;break;}
                    if(!collision)break;target.Y+=size.Height+3;
                }
                if(target.Bottom>work.Bottom)target.Y=Math.Max(work.Top,button.PointToScreen(Point.Empty).Y-size.Height-3);
                tags.Add(new KeyTip {Text=text,Bounds=target});
            }
            if(tags.Count==0){Hide();return;}
            Rectangle area=tags[0].Bounds;foreach(KeyTip tag in tags)area=Rectangle.Union(area,tag.Bounds);
            area.Inflate(3,3);
            foreach(KeyTip tag in tags)tag.Bounds.Offset(-area.Left,-area.Top);
            if(window==null||window.IsDisposed)window=new KeyTipWindow();
            window.SetTags(tags,area);
            if(!window.Visible)window.Show();
            SetWindowPos(window.Handle,new IntPtr(-1),0,0,0,0,0x1|0x2|0x10);
        }
        private static void Collect(Control control,List<Button> buttons)
        {
            if(control==null||!control.Visible)return;
            Button button=control as Button;if(button!=null)buttons.Add(button);
            foreach(Control child in control.Controls)Collect(child,buttons);
        }
        public void Dispose(){if(window!=null)window.Dispose();}
        [DllImport("user32.dll")]private static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);
        private sealed class KeyTip {internal string Text;internal Rectangle Bounds;}
        private sealed class KeyTipWindow : Form
        {
            private List<KeyTip> tags=new List<KeyTip>();
            internal KeyTipWindow()
            {
                Name="ToolbarShortcutKeyTips";Text="ppInk shortcuts (Alt held)";
                FormBorderStyle=FormBorderStyle.None;StartPosition=FormStartPosition.Manual;
                Enabled=false;TabStop=false;ShowInTaskbar=false;TopMost=true;BackColor=Color.Fuchsia;TransparencyKey=Color.Fuchsia;DoubleBuffered=true;
            }
            protected override bool ShowWithoutActivation {get{return true;}}
            protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x20|0x80;return p;}}
            internal void SetTags(List<KeyTip> values,Rectangle area)
            {
                bool changed=Bounds!=area||values.Count!=tags.Count;
                if(!changed)for(int i=0;i<tags.Count;i++)if(tags[i].Text!=values[i].Text||tags[i].Bounds!=values[i].Bounds){changed=true;break;}
                if(!changed)return;tags=values;Bounds=area;Invalidate();
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                // Color-keyed edges must stay exact to avoid magenta fringes.
                base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.None;
                using(var font=new Font("Segoe UI",12f,FontStyle.Bold,GraphicsUnit.Pixel))
                using(var shadow=new SolidBrush(Color.FromArgb(78,87,101)))
                using(var fill=new SolidBrush(Color.FromArgb(24,30,40)))
                foreach(KeyTip tag in tags)
                {
                    Rectangle r=tag.Bounds;
                    using(GraphicsPath path=Round(new Rectangle(r.X+2,r.Y+2,r.Width,r.Height)))e.Graphics.FillPath(shadow,path);
                    using(GraphicsPath path=Round(r))e.Graphics.FillPath(fill,path);
                    TextRenderer.DrawText(e.Graphics,tag.Text,font,r,Color.White,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
                }
            }
            private static GraphicsPath Round(Rectangle r)
            {
                var path=new GraphicsPath();const int d=7;
                path.AddArc(r.Left,r.Top,d,d,180,90);path.AddArc(r.Right-d,r.Top,d,d,270,90);
                path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);path.AddArc(r.Left,r.Bottom-d,d,d,90,90);path.CloseFigure();return path;
            }
        }
    }
}
