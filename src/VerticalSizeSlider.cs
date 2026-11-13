using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace gInk
{
    internal sealed class VerticalSizeSlider : Control
    {
        private const int TrackTop = 60;
        private const int TrackEndInset = 18;
        private int value = 1;
        private bool dragging;
        private int dragTop;
        private Color sampleColor = Color.White;

        internal int Maximum { get; set; } = 200;
        internal bool IsDragging { get { return dragging; } }
        internal string Unit { get; set; } = "px";
        internal Color SampleColor
        {
            get { return sampleColor; }
            set { if (sampleColor != value) { sampleColor = value; Invalidate(); } }
        }
        internal int Value
        {
            get { return value; }
            set
            {
                int next = Math.Max(1, Math.Min(Maximum, value));
                if (this.value == next) return;
                this.value = next;
                Invalidate();
            }
        }

        internal event Action<int> Dragged;
        internal event Action<int> DragFinished;

        internal VerticalSizeSlider()
        {
            Size = new Size(56, 222);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            Cursor = Cursors.SizeNS;
        }

        private int TrackBottom { get { return Math.Max(TrackTop + 1, Height - TrackEndInset); } }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || e.Y < TrackTop - 12) return;
            dragging = true;
            dragTop = PointToScreen(Point.Empty).Y;
            Capture = true;
            SlideTo(e.Y);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) SlideTo(e.Y);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!dragging || e.Button != MouseButtons.Left) return;
            SlideTo(e.Y);
            Capture = false;
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!dragging || Capture) return;
            dragging = false;
            DragFinished?.Invoke(Value);
        }

        private void SlideTo(int y)
        {
            y = PointToScreen(new Point(0, y)).Y - dragTop;
            double fraction = Math.Max(0, Math.Min(1, (TrackBottom - y) / (double)(TrackBottom - TrackTop)));
            int next = 1 + (int)Math.Round((Maximum - 1) * fraction * fraction);
            if (Value == next) return;
            Value = next;
            Dragged?.Invoke(Value);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.FromArgb(31, 34, 40));
            int middle = Width / 2;
            float thumb = TrackBottom - (float)Math.Sqrt((Value - 1.0) / (Maximum - 1)) * (TrackBottom - TrackTop);
            using (var track = new Pen(Color.FromArgb(94, 101, 112), 4) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var active = new Pen(Color.FromArgb(79, 216, 219), 4) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var sample = new SolidBrush(SampleColor))
            {
                g.DrawLine(track, middle, TrackTop, middle, TrackBottom);
                g.DrawLine(active, middle, thumb, middle, TrackBottom);
                g.FillEllipse(Brushes.White, middle - 7, thumb - 7, 14, 14);
                if (Unit == "px")
                {
                    float diameter = Math.Min(25, Value);
                    g.DrawEllipse(Pens.White, middle - diameter / 2, 18 - diameter / 2, diameter, diameter);
                    g.FillEllipse(sample, middle - diameter / 2, 18 - diameter / 2, diameter, diameter);
                }
            }
            TextRenderer.DrawText(g, Value + " " + Unit, SystemFonts.MessageBoxFont,
                new Rectangle(0, Unit == "px" ? 32 : 12, Width, 19), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    internal sealed class PenSizeSidebar : Form
    {
        internal VerticalSizeSlider Slider { get; } = new VerticalSizeSlider
        {
            Name = "BrushSizeSlider", AccessibleName = "브러시 크기 (픽셀)", Dock = DockStyle.Fill
        };

        internal PenSizeSidebar()
        {
            Name = "PenSizeSidebar";
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = Slider.Size;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Controls.Add(Slider);
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x08000080; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW.
                return parameters;
            }
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0021) // WM_MOUSEACTIVATE
            {
                message.Result = new IntPtr(3);
                return;
            }
            base.WndProc(ref message);
        }

        internal void PlaceBesideDrawingArea(FormCollection host)
        {
            Rectangle toolbar = host.gpButtons.RectangleToScreen(host.gpButtons.ClientRectangle);
            Rectangle work = Rectangle.Intersect(host.Bounds, Screen.FromRectangle(toolbar).WorkingArea);
            if (work.IsEmpty) work = Screen.FromRectangle(toolbar).WorkingArea;
            bool toolbarOnLeft = toolbar.Left + toolbar.Width / 2 < work.Left + work.Width / 2;
            int x = toolbarOnLeft ? work.Right - Width - 16 : work.Left + 16;
            int y = work.Top + Math.Max(0, (work.Height - Height) / 2);
            Point target = new Point(Math.Max(work.Left, x), y);
            if (Location != target) Location = target;
        }

        internal void RaiseWithoutActivation()
        {
            if (Visible) SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
        }

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    }
}
