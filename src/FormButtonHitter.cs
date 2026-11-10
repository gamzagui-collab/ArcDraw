using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace gInk
{
	public partial class FormButtonHitter : Form
	{
		Root Root;
		FormCollection FC;
        private bool pointerToolbarDrag,suppressToolbarClick;
        protected override bool ShowWithoutActivation {get{return true;}}
        // DateTime MouseTimeDown;
		// http://www.csharp411.com/hide-form-from-alttab/
		protected override CreateParams CreateParams
		{
			get
			{
				CreateParams cp = base.CreateParams;
				// turn on WS_EX_TOOLWINDOW style bit
				cp.ExStyle |= 0x80 | 0x08000000;
				return cp;
			}
		}

		public FormButtonHitter(Root root)
		{
			Root = root;
			FC = Root.FormCollection;
			InitializeComponent();
            MouseMove+=PointerToolbarMouseMove;
            MouseUp+=PointerToolbarMouseUp;

            Initialize();

		}

        public void Initialize()
        {
            Left = FC.gpButtons.Left + FC.Left;
            Top = FC.gpButtons.Top + FC.Top;
            Width = FC.gpButtons.Width;
            Height = FC.gpButtons.Height;
            timer1.Enabled = true;
        }

        protected override void WndProc(ref Message msg)
        {
            if(msg.Msg==0x21 && Root.DrawingSession!=null && Root.DrawingSession.Running)
            {msg.Result=new IntPtr(3);return;} // MA_NOACTIVATE: keep the desktop/game focused.
            //if ((msg.Msg == 0x001C) || (msg.Msg == 6)) //WM_ACTIVATEAPP : generated through alt+tab
            if ((msg.Msg == 6)) //WM_ACTIVATE : generated through alt+tab
            {
                if(Control.MouseButtons == MouseButtons.Right)
                {
                    Root.FormCollection.AddM3UEntry();
                    return;
                }
                // Console.WriteLine(DateTime.Now.ToString() + " !msgH " + msg.Msg.ToString()+" - "+ msg.WParam.ToString());
                Form f = Control.FromHandle(msg.LParam) as Form;
                if (DateTime.Now < Root.PointerChangeDate || (msg.WParam == IntPtr.Zero && msg.LParam == IntPtr.Zero) || (msg.WParam == IntPtr.Zero && msg.LParam != IntPtr.Zero && f!=null && f?.CompanyName != CompanyName))
                    return;
                if (Root.PointerMode && (Root.DrawingSession == null || !Root.DrawingSession.Running))
                {
                    //Console.WriteLine("activating from hitter " + (Root.PointerMode ? "pointer" : "not") + (Root.Docked ? "docked" : "not"));
                    Root.FormCollection.AltTabActivate();
                    return;
                }
            }
            /*else if (msg.Msg != 0x0113)
            {
                Console.WriteLine(DateTime.Now.ToString()+" msgH " + msg.Msg.ToString());
            }*/
            base.WndProc(ref msg);
        }

        private void FormButtonHitter_Click(object sender, EventArgs e)
		{
            if(suppressToolbarClick)return;
			MouseEventArgs m = (MouseEventArgs)e;
            TimeSpan tsp= DateTime.Now - Root.FormCollection.MouseTimeDown;
            //MessageBox.Show(string.Format("t = {0:N3}",tsp.TotalSeconds));
			foreach (Control control in FC.gpButtons.Controls)
			{
				if (control is Button && control.Visible && control.Enabled)
				{
					if (m.X >= control.Left && m.X <= control.Right && m.Y >= control.Top && m.Y <= control.Bottom)
					{ ((Button)control).PerformClick();break; }
				}
			}
		}


		public void ToTopMost()
		{
            TopMost = true;
			UInt32 dwExStyle = GetWindowLong(this.Handle, -20);
			SetWindowLong(this.Handle, -20, dwExStyle | 0x00080000);
			//SetLayeredWindowAttributes(this.Handle, 0x00FFFFFF, 200, 0x2);
			SetWindowPos(this.Handle, (IntPtr)(-1), 0, 0, 0, 0, 0x0002 | 0x0001 | 0x0020 | 0x0010);
		}

		public void timer1_Tick(object sender, EventArgs e)
		{
            if(Root.DrawingSession!=null&&Root.DrawingSession.ToolbarHidden) { if(Visible)Hide();return; }
			if (true||this.Visible)  // we force resizing weither visible or not in order to have the good size during alt+tab
			{
                Size s = FC.VisibleToolbar;
                Rectangle next=new Rectangle(FC.gpButtons.Left+FC.Left,FC.gpButtons.Top+FC.Top,s.Width,s.Height);
                if(Bounds!=next)
                {
                    if(IsHandleCreated)SetWindowPos(Handle,IntPtr.Zero,next.Left,next.Top,next.Width,next.Height,0x0010|0x0004);
                    else Bounds=next;
                }
			}
		}

		[DllImport("user32.dll")]
		static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
		[DllImport("user32.dll", SetLastError = true)]
		static extern UInt32 GetWindowLong(IntPtr hWnd, int nIndex);
		[DllImport("user32.dll")]
		static extern int SetWindowLong(IntPtr hWnd, int nIndex, UInt32 dwNewLong);
		[DllImport("user32.dll")]
		public extern static bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

        private void FormButtonHitter_MouseDown(object sender, MouseEventArgs e)
        {
            //Console.WriteLine("BH", (sender as Control).Name);
            Root.FormCollection.MouseTimeDown = DateTime.Now;
            pointerToolbarDrag=FC.BeginPointerToolbarDrag(this,e);
            suppressToolbarClick=pointerToolbarDrag;
        }
        private void PointerToolbarMouseMove(object sender,MouseEventArgs e)
        { if(pointerToolbarDrag)FC.MovePointerToolbarDrag(); }
        private void PointerToolbarMouseUp(object sender,MouseEventArgs e)
        {
            if(!pointerToolbarDrag)return;
            FC.MovePointerToolbarDrag();FC.EndPointerToolbarDrag();pointerToolbarDrag=false;
        }
    }
}
