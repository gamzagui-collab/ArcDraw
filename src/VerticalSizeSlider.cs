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
        private string unit = "px";

        internal const int BrushMaximum = 200;
        internal const int TextMaximum = 144;
        internal int Maximum { get; private set; } = BrushMaximum;
        internal bool IsDragging { get { return dragging; } }
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

        internal void SetMode(bool text, int maximum)
        {
            string next = text ? "pt" : "px";
            if (unit == next && Maximum == maximum) return;
            Capture = false;
            unit = next;
            Maximum = maximum;
            AccessibleName = text ? "글자 크기 (포인트)" : "브러시 크기 (픽셀)";
            Invalidate();
        }

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
                if (unit == "px")
                {
                    float diameter = Math.Min(25, Value);
                    g.DrawEllipse(Pens.White, middle - diameter / 2, 18 - diameter / 2, diameter, diameter);
                    g.FillEllipse(sample, middle - diameter / 2, 18 - diameter / 2, diameter, diameter);
                }
            }
            TextRenderer.DrawText(g, Value + " " + unit, SystemFonts.MessageBoxFont,
                new Rectangle(0, unit == "px" ? 32 : 12, Width, 19), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    internal sealed class DrawingSizeSidebar : Form
    {
        private Rectangle lastToolbar, lastNoteToolbar;

        internal VerticalSizeSlider Slider { get; } = new VerticalSizeSlider
        {
            Name = "DrawingSizeSlider", AccessibleName = "브러시 크기 (픽셀)", Dock = DockStyle.Fill
        };

        internal DrawingSizeSidebar()
        {
            Name = "DrawingSizeSidebar";
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

        internal void PlaceBesideToolbar(Rectangle toolbar, Rectangle noteToolbar)
        {
            if (Visible && lastToolbar == toolbar && lastNoteToolbar == noteToolbar) return;
            lastToolbar = toolbar;
            lastNoteToolbar = noteToolbar;
            Rectangle work = Screen.FromRectangle(toolbar).WorkingArea;
            const int gap = 6;
            int x = Math.Max(work.Left, Math.Min(toolbar.Left, work.Right - Width));
            int y = Math.Max(work.Top, Math.Min(toolbar.Top + (toolbar.Height - Height) / 2, work.Bottom - Height));
            Point left = new Point(toolbar.Left - Width - gap, y);
            Point right = new Point(toolbar.Right + gap, y);
            Point above = new Point(x, toolbar.Top - Height - gap);
            Point below = new Point(x, toolbar.Bottom + gap);
            bool moreRoomOnRight = work.Right - toolbar.Right > toolbar.Left - work.Left;
            bool moreRoomBelow = work.Bottom - toolbar.Bottom > toolbar.Top - work.Top;
            var candidates = new[]
            {
                moreRoomOnRight ? right : left, moreRoomOnRight ? left : right,
                moreRoomBelow ? below : above, moreRoomBelow ? above : below
            };
            foreach (Point candidate in candidates)
            {
                var bounds = new Rectangle(candidate, Size);
                if (work.Contains(bounds) && !bounds.IntersectsWith(noteToolbar))
                {
                    if (Location != candidate) Location = candidate;
                    return;
                }
            }
            Point nearest = candidates[0];
            Location = new Point(Math.Max(work.Left, Math.Min(nearest.X, work.Right - Width)), y);
        }

        internal void EnableForTextEditor()
        {
            // ShowDialog disables other windows on this thread, including this
            // non-activating tool. Re-enable just this window for live sizing.
            if (!IsWindowEnabled(Handle))
            {
                EnableWindow(Handle, true);
                RaiseWithoutActivation();
            }
        }

        internal void RaiseWithoutActivation()
        {
            if (Visible) SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
        }

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll")]
        private static extern bool IsWindowEnabled(IntPtr window);
        [DllImport("user32.dll")]
        private static extern bool EnableWindow(IntPtr window, bool enabled);
    }
}
