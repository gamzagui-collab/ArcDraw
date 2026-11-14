using System;
using System.Drawing;
using System.Windows.Forms;

namespace gInk
{
    internal sealed class ScreenFrameAreaEditor : Form
    {
        private const int Grip = 16, RelativeScale = 10000;
        private readonly Rectangle limit;
        private readonly Size minimum;
        private Rectangle original;
        private Point mouseStart;
        private int horizontal, vertical;
        internal event Action<Rectangle> Applied;

        internal ScreenFrameAreaEditor(Rectangle limit, Rectangle initial)
        {
            this.limit = limit;
            minimum = MinimumArea(limit);
            Name = "ScreenFrameAreaEditor";
            Text = "창 최대화 영역 편집";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            Opacity = .72;
            BackColor = Color.FromArgb(29, 77, 108);
            Bounds = Constrain(initial, limit);
            var commands = new FlowLayoutPanel
            {
                AutoSize = true, WrapContents = false, BackColor = Color.FromArgb(237, 248, 255)
            };
            var apply = new Button { Name = "ApplyScreenFrameArea", Text = "적용", AutoSize = true };
            var reset = new Button { Name = "ResetScreenFrameArea", Text = "자동 영역", AutoSize = true };
            var cancel = new Button { Text = "취소", AutoSize = true };
            apply.Click += delegate { Finish(Bounds); };
            reset.Click += delegate { Finish(Rectangle.Empty); };
            cancel.Click += delegate { Close(); };
            commands.Controls.AddRange(new Control[] { apply, reset, cancel });
            Controls.Add(commands);
            AcceptButton = apply;
            CancelButton = cancel;
            void PlaceCommands() => commands.Location = new Point(Math.Max(Grip, (ClientSize.Width - commands.Width) / 2), Grip + 26);
            commands.SizeChanged += delegate { PlaceCommands(); };
            Resize += delegate { PlaceCommands(); Invalidate(); };
            PlaceCommands();
        }

        private void Finish(Rectangle area)
        {
            Hide();
            Applied?.Invoke(area);
            Close();
        }

        private static Size MinimumArea(Rectangle limit) =>
            new Size(Math.Min(limit.Width - 2, Math.Max(310, limit.Width / 4)),
                Math.Min(limit.Height - 2, Math.Max(140, limit.Height / 4)));

        internal static Rectangle Constrain(Rectangle bounds, Rectangle limit)
        {
            Size minimum = MinimumArea(limit);
            int width = Math.Max(minimum.Width, Math.Min(limit.Width - 2, bounds.Width));
            int height = Math.Max(minimum.Height, Math.Min(limit.Height - 2, bounds.Height));
            return new Rectangle(Clamp(bounds.X, limit.Left + 1, limit.Right - 1 - width),
                Clamp(bounds.Y, limit.Top + 1, limit.Bottom - 1 - height), width, height);
        }

        private static int Clamp(int value, int low, int high) => Math.Max(low, Math.Min(high, value));

        internal static Rectangle ScaleForSettings(Rectangle area, Rectangle monitor) =>
            Rectangle.FromLTRB(
                Scale(area.Left - monitor.Left, monitor.Width, RelativeScale),
                Scale(area.Top - monitor.Top, monitor.Height, RelativeScale),
                Scale(area.Right - monitor.Left, monitor.Width, RelativeScale),
                Scale(area.Bottom - monitor.Top, monitor.Height, RelativeScale));

        internal static Rectangle ScaleFromSettings(Rectangle area, Rectangle monitor) =>
            Rectangle.FromLTRB(
                monitor.Left + Scale(area.Left, RelativeScale, monitor.Width),
                monitor.Top + Scale(area.Top, RelativeScale, monitor.Height),
                monitor.Left + Scale(area.Right, RelativeScale, monitor.Width),
                monitor.Top + Scale(area.Bottom, RelativeScale, monitor.Height));

        private static int Scale(int value, int from, int to) => (int)Math.Round((double)value * to / from);

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var border = new Pen(Color.FromArgb(115, 224, 255), 3))
                e.Graphics.DrawRectangle(border, 1, 1, ClientSize.Width - 3, ClientSize.Height - 3);
            foreach (int x in new[] { 2, ClientSize.Width / 2 - 5, ClientSize.Width - 12 })
                foreach (int y in new[] { 2, ClientSize.Height / 2 - 5, ClientSize.Height - 12 })
                    if (x != ClientSize.Width / 2 - 5 || y != ClientSize.Height / 2 - 5)
                        e.Graphics.FillRectangle(Brushes.White, x, y, 10, 10);
            TextRenderer.DrawText(e.Graphics, "안쪽을 끌어 이동 · 가장자리를 끌어 크기 조절", Font,
                new Rectangle(Grip, Grip, ClientSize.Width - 2 * Grip, 20), Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.EndEllipsis);
        }

        private static int Edge(int position, int length) => position < Grip ? -1 : position >= length - Grip ? 1 : 0;

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            horizontal = Edge(e.X, ClientSize.Width);
            vertical = Edge(e.Y, ClientSize.Height);
            mouseStart = PointToScreen(e.Location);
            original = Bounds;
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!Capture)
            {
                int x = Edge(e.X, ClientSize.Width), y = Edge(e.Y, ClientSize.Height);
                Cursor = x == 0 ? (y == 0 ? Cursors.SizeAll : Cursors.SizeNS) :
                    y == 0 ? Cursors.SizeWE : x == y ? Cursors.SizeNWSE : Cursors.SizeNESW;
                return;
            }
            Point current = PointToScreen(e.Location);
            int dx = current.X - mouseStart.X, dy = current.Y - mouseStart.Y;
            if (horizontal == 0 && vertical == 0)
            {
                Bounds = Constrain(new Rectangle(original.X + dx, original.Y + dy, original.Width, original.Height), limit);
                return;
            }
            int left = original.Left, top = original.Top, right = original.Right, bottom = original.Bottom;
            if (horizontal < 0) left = Clamp(left + dx, limit.Left + 1, right - minimum.Width);
            if (horizontal > 0) right = Clamp(right + dx, left + minimum.Width, limit.Right - 1);
            if (vertical < 0) top = Clamp(top + dy, limit.Top + 1, bottom - minimum.Height);
            if (vertical > 0) bottom = Clamp(bottom + dy, top + minimum.Height, limit.Bottom - 1);
            Bounds = Rectangle.FromLTRB(left, top, right, bottom);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left) Capture = false;
        }
    }
}
