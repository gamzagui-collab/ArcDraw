using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace gInk
{
    internal sealed class DrawingTargetSwitcher : IDisposable
    {
        private readonly Root root;
        private readonly List<WhiteboardLayerForm> targets=new List<WhiteboardLayerForm>();
        private TargetWindow window;
        private int selected;
        internal bool IsOpen {get;private set;}
        internal DrawingTargetSwitcher(Root root){this.root=root;}
        internal void Step(bool reverse)
        {
            WhiteboardLayerForm previous=IsOpen?targets[selected]:root.Whiteboards.ActiveNote;
            targets.Clear();targets.Add(null);
            foreach(WhiteboardLayerForm note in root.Whiteboards.Notes)if(!note.IsDisposed)targets.Add(note);
            selected=Math.Max(0,targets.IndexOf(previous));selected=(selected+(reverse?targets.Count-1:1))%targets.Count;
            IsOpen=true;
            if(window==null||window.IsDisposed)window=new TargetWindow();
            var labels=new List<string>();foreach(WhiteboardLayerForm note in targets)labels.Add(note==null?"화면 (바탕화면)":"노트 "+note.Number+(note.UserHidden?"  ·  숨김":""));
            window.SetTargets(labels,selected,Screen.FromControl(root.FormCollection.gpButtons).WorkingArea);
            if(window.Owner!=root.FormCollection)window.Owner=root.FormCollection;
            if(!window.Visible)window.Show();window.RaiseWithoutFocus();
        }
        internal void Commit()
        {
            if(!IsOpen)return;
            WhiteboardLayerForm target=targets[selected];Cancel();
            if(target!=null&&!target.IsDisposed&&root.Whiteboards.Notes.Contains(target))
            {root.Whiteboards.SetActive(target);target.FocusDrawingSurface();}
            else {root.Whiteboards.SelectScreen();root.DrawingSession.QueueDrawingFocus();}
            root.DrawingSession.RefreshDrawingTarget();
        }
        internal void Cancel(){IsOpen=false;if(window!=null&&!window.IsDisposed)window.Hide();targets.Clear();}
        public void Dispose(){Cancel();if(window!=null)window.Dispose();}

        private sealed class TargetWindow : Form
        {
            private List<string> labels=new List<string>();private int selected,first,shown;
            internal TargetWindow()
            {
                Name="ArcDrawTargetSwitcher";Text="ArcDraw 필기 대상 전환";FormBorderStyle=FormBorderStyle.None;
                StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;ShowInTaskbar=false;TopMost=true;DoubleBuffered=true;
                BackColor=Color.FromArgb(23,31,43);ForeColor=Color.White;Font=new Font("Malgun Gothic",10f);
            }
            internal void SetTargets(List<string> values,int index,Rectangle work)
            {
                labels=values;selected=index;shown=Math.Min(values.Count,Math.Max(1,Math.Min(8,(work.Height-130)/44)));
                first=Math.Max(0,Math.Min(selected-shown/2,values.Count-shown));
                Size=new Size(Math.Max(200,Math.Min(420,work.Width-24)),92+shown*44);
                Location=new Point(work.Left+(work.Width-Width)/2,work.Top+(work.Height-Height)/2);
                AccessibleName="필기 대상 선택: "+labels[selected];Invalidate();
            }
            protected override bool ShowWithoutActivation {get{return true;}}
            protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x80;return p;}}
            protected override void WndProc(ref Message m){if(m.Msg==0x21){m.Result=new IntPtr(3);return;}base.WndProc(ref m);}
            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);Graphics g=e.Graphics;
                using(var edge=new Pen(Color.FromArgb(70,211,242)))g.DrawRectangle(edge,0,0,Width-1,Height-1);
                TextRenderer.DrawText(g,"필기 대상 선택",Font,new Rectangle(18,12,Width-36,28),Color.White,TextFormatFlags.Left|TextFormatFlags.VerticalCenter);
                for(int row=0;row<shown;row++)
                {
                    int index=first+row;Rectangle r=new Rectangle(12,48+row*44,Width-24,39);bool current=index==selected;
                    using(var fill=new SolidBrush(current?Color.FromArgb(15,111,132):Color.FromArgb(35,45,60)))g.FillRectangle(fill,r);
                    if(current)using(var edge=new Pen(Color.FromArgb(75,224,248),2))g.DrawRectangle(edge,r);
                    TextRenderer.DrawText(g,(current?"✓  ":"    ")+labels[index],Font,Rectangle.Inflate(r,-10,0),Color.White,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
                }
                using(var hint=new Font(Font.FontFamily,8.5f))
                {
                    Color muted=Color.FromArgb(182,204,219);
                    TextRenderer.DrawText(g,"Alt 놓아 선택 · Tab 다음 · Esc 취소",hint,new Rectangle(16,Height-34,Width-94,25),muted,
                        TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
                    TextRenderer.DrawText(g,(selected+1)+"/"+labels.Count,hint,new Rectangle(Width-74,Height-34,58,25),muted,
                        TextFormatFlags.Right|TextFormatFlags.VerticalCenter);
                }
            }
            internal void RaiseWithoutFocus(){SetWindowPos(Handle,new IntPtr(-1),0,0,0,0,0x1|0x2|0x10);}
            [DllImport("user32.dll")]private static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int w,int h,uint flags);
        }
    }
    public sealed partial class WhiteboardLayerForm
    {
        internal void FocusDrawingSurface(){if(!closing&&Visible){Activate();paper.Focus();}}
    }
}
