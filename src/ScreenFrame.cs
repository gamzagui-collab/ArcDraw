using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace gInk
{
    internal sealed class ScreenFrame : IDisposable
    {
        private readonly DrawingSettings settings;
        private readonly NoteVisualWindow[] edges = new NoteVisualWindow[4];
        private Form options;
        private Rectangle lastBounds;
        private int lastWidth, lastTheme = -1;

        internal ScreenFrame(DrawingSettings settings)
        {
            this.settings = settings;
            SystemEvents.DisplaySettingsChanged += OnDisplayChanged;
        }

        private void OnDisplayChanged(object sender, EventArgs e)
        {
            NoteVisualWindow window = edges[0];
            if (window == null || !window.IsHandleCreated) return;
            try { window.BeginInvoke(new Action(Refresh)); }
            catch (InvalidOperationException) { }
        }

        internal void Refresh()
        {
            if (!settings.ScreenFrameEnabled)
            {
                foreach (var edge in edges) edge?.Hide();
                return;
            }
            Screen screen = Screen.AllScreens.FirstOrDefault(item => item.DeviceName == settings.ScreenFrameMonitor) ?? Screen.PrimaryScreen;
            Rectangle bounds = screen.Bounds;
            int width = Math.Max(3, Math.Min(bounds.Width, bounds.Height) * settings.ScreenFrameWidth / 100);
            if (bounds == lastBounds && width == lastWidth && settings.ScreenFrameTheme == lastTheme && edges[0] != null)
            {
                foreach (var edge in edges) if (!edge.Visible) edge.Show();
                return;
            }
            Rectangle[] strips =
            {
                new Rectangle(0, 0, bounds.Width, width),
                new Rectangle(0, bounds.Height - width, bounds.Width, width),
                new Rectangle(0, width, width, bounds.Height - 2 * width),
                new Rectangle(bounds.Width - width, width, width, bounds.Height - 2 * width)
            };
            for (int i = 0; i < strips.Length; i++)
            {
                Rectangle strip = strips[i];
                if (edges[i] == null) edges[i] = new NoteVisualWindow("ScreenFrameEdge" + i);
                var location = new Point(bounds.X + strip.X, bounds.Y + strip.Y);
                edges[i].Bounds = new Rectangle(location, strip.Size);
                using (var bitmap = new Bitmap(strip.Width, strip.Height, PixelFormat.Format32bppPArgb))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.TranslateTransform(-strip.X, -strip.Y);
                        ScreenFrameStyle.Paint(g, bounds.Size, width, settings.ScreenFrameTheme);
                    }
                    edges[i].Present(bitmap, location);
                }
                if (!edges[i].Visible) edges[i].Show();
            }
            lastBounds = bounds;
            lastWidth = width;
            lastTheme = settings.ScreenFrameTheme;
        }

        internal void ShowOptions()
        {
            if (options != null)
            {
                options.Activate();
                return;
            }
            options = new Form
            {
                Name = "ScreenFrameOptions", Text = "화면 액자", ClientSize = new Size(360, 201),
                StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog,
                ShowInTaskbar = false, TopMost = true, MaximizeBox = false, MinimizeBox = false
            };
            var enabled = new CheckBox { Name = "ScreenFrameEnabled", Text = "화면 액자 표시", Checked = settings.ScreenFrameEnabled, Bounds = new Rectangle(14, 10, 330, 24) };
            var theme = new ComboBox { Name = "ScreenFrameTheme", DropDownStyle = ComboBoxStyle.DropDownList, Bounds = new Rectangle(96, 44, 250, 24) };
            theme.Items.AddRange(ScreenFrameStyle.Names);
            theme.SelectedIndex = settings.ScreenFrameTheme;
            var monitor = new ComboBox { Name = "ScreenFrameMonitor", DropDownStyle = ComboBoxStyle.DropDownList, Bounds = new Rectangle(96, 76, 250, 24) };
            Screen[] screens = Screen.AllScreens;
            int selectedMonitor = Array.FindIndex(screens, item => item.DeviceName == settings.ScreenFrameMonitor);
            for (int i = 0; i < screens.Length; i++)
                monitor.Items.Add("화면 " + (i + 1) + "  ·  " + screens[i].Bounds.Width + "×" + screens[i].Bounds.Height + (screens[i].Primary ? "  (주 화면)" : ""));
            monitor.SelectedIndex = selectedMonitor < 0 ? Array.FindIndex(screens, item => item.Primary) : selectedMonitor;
            var width = new TrackBar
            {
                Name = "ScreenFrameWidth", AccessibleName = "화면 안쪽을 덮는 폭", Minimum = 1, Maximum = 12, Value = settings.ScreenFrameWidth,
                TickStyle = TickStyle.None, AutoSize = false, Bounds = new Rectangle(90, 113, 208, 26)
            };
            var amount = new Label { Text = width.Value + "%", Bounds = new Rectangle(306, 117, 40, 24) };
            var done = new Button { Name = "CloseScreenFrameOptions", Text = "닫기", Bounds = new Rectangle(264, 159, 82, 28) };
            var update = new Timer { Interval = 30 };
            update.Tick += delegate { update.Stop(); Refresh(); };
            enabled.CheckedChanged += delegate { settings.ScreenFrameEnabled = enabled.Checked; Refresh(); settings.SaveToolSettings(); };
            theme.SelectedIndexChanged += delegate { settings.ScreenFrameTheme = theme.SelectedIndex; Refresh(); settings.SaveToolSettings(); };
            monitor.SelectedIndexChanged += delegate
            {
                if (monitor.SelectedIndex < 0) return;
                settings.ScreenFrameMonitor = screens[monitor.SelectedIndex].DeviceName;
                Refresh();
                settings.SaveToolSettings();
            };
            width.ValueChanged += delegate { settings.ScreenFrameWidth = width.Value; amount.Text = width.Value + "%"; update.Start(); };
            done.Click += delegate { options.Close(); };
            options.CancelButton = done;
            options.FormClosed += delegate
            {
                options = null;
                update.Dispose();
                Refresh();
                settings.SaveToolSettings();
            };
            options.Controls.AddRange(new Control[] { enabled, theme, monitor, width, amount, done,
                new Label { Text = "테마", Location = new Point(14, 47), AutoSize = true },
                new Label { Text = "대상 화면", Location = new Point(14, 79), AutoSize = true },
                new Label { Text = "안쪽 폭", Location = new Point(14, 117), AutoSize = true } });
            options.Show();
        }

        public void Dispose()
        {
            SystemEvents.DisplaySettingsChanged -= OnDisplayChanged;
            options?.Close();
            for (int i = 0; i < edges.Length; i++)
            {
                edges[i]?.Dispose();
                edges[i] = null;
            }
        }
    }

    internal static class ScreenFrameStyle
    {
        internal static readonly string[] Names =
        {
            "엔틱 클래식", "모던 미니멀", "사이버펑크", "달빛 무협", "파스텔 구름",
            "심플 라인", "금빛 물결", "벚꽃", "초록 잎", "판타지"
        };

        private static readonly Color[] Colors =
        {
            Color.FromArgb(245, 185, 91), Color.FromArgb(171, 203, 232), Color.Cyan,
            Color.FromArgb(245, 209, 151), Color.FromArgb(244, 185, 228), Color.WhiteSmoke,
            Color.Gold, Color.FromArgb(246, 149, 187), Color.FromArgb(175, 216, 107), Color.MediumPurple
        };

        internal static void Paint(Graphics g, Size screen, int width, int theme)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color color = Colors[theme];
            if (theme != 5)
            {
                int alpha = theme == 4 ? 115 : 72;
                for (int edge = 0; edge < 4; edge++)
                {
                    Rectangle strip = edge < 2
                        ? new Rectangle(0, edge == 0 ? 0 : screen.Height - width, screen.Width, width)
                        : new Rectangle(edge == 2 ? 0 : screen.Width - width, width, width, screen.Height - width * 2);
                    Color edgeColor = theme == 2 && edge == 3 ? Color.Magenta : color;
                    Color outside = Color.FromArgb(alpha, edgeColor), inside = Color.FromArgb(0, edgeColor);
                    bool reversed = edge == 1 || edge == 3;
                    using (var tint = new LinearGradientBrush(strip, reversed ? inside : outside, reversed ? outside : inside,
                        edge < 2 ? LinearGradientMode.Vertical : LinearGradientMode.Horizontal))
                        g.FillRectangle(tint, strip);
                }
            }
            float stroke = Math.Max(1, width * .10f);
            float inset = width * .22f;
            using (var border = new LinearGradientBrush(new Rectangle(Point.Empty, screen), color,
                theme == 2 ? Color.Magenta : theme == 4 ? Color.FromArgb(172, 190, 255) : Color.FromArgb(220, color), 15f))
            using (var line = new Pen(border, stroke) { LineJoin = LineJoin.Round })
            using (var glow = new Pen(Color.FromArgb(62, color), Math.Max(1, width * .35f)) { LineJoin = LineJoin.Round })
            using (var path = Outline(screen, inset, theme == 1 || theme == 2 ? width * .75f : width * .30f))
            {
                if (theme != 5) g.DrawPath(glow, path);
                g.DrawPath(line, path);
                if (theme != 5)
                {
                    using (GraphicsPath inner = Outline(screen, width * .51f, width * .34f))
                    using (var innerLine = new Pen(Color.FromArgb(120, color), Math.Max(1, stroke * .55f)))
                        g.DrawPath(innerLine, inner);
                }
                for (int corner = 0; corner < 4; corner++)
                {
                    GraphicsState saved = g.Save();
                    g.TranslateTransform(corner % 2 == 0 ? 0 : screen.Width - 1, corner < 2 ? 0 : screen.Height - 1);
                    g.ScaleTransform(corner % 2 == 0 ? 1 : -1, corner < 2 ? 1 : -1);
                    DrawCorner(g, width, theme, corner, color, line);
                    g.Restore(saved);
                }
            }
        }

        private static GraphicsPath Outline(Size size, float inset, float cut)
        {
            float right = size.Width - inset - 1, bottom = size.Height - inset - 1;
            var path = new GraphicsPath();
            path.AddPolygon(new[]
            {
                new PointF(inset + cut, inset), new PointF(right - cut, inset),
                new PointF(right, inset + cut), new PointF(right, bottom - cut),
                new PointF(right - cut, bottom), new PointF(inset + cut, bottom),
                new PointF(inset, bottom - cut), new PointF(inset, inset + cut)
            });
            return path;
        }

        private static void DrawCorner(Graphics g, float width, int theme, int corner, Color color, Pen line)
        {
            if (theme == 5) return;
            if (theme == 2 && corner % 2 == 1) color = Color.Magenta;
            using (var strong = new SolidBrush(color))
            using (var pale = new SolidBrush(Color.FromArgb(160, color)))
            {
                switch (theme)
                {
                    case 0:
                    case 6:
                    case 9:
                        g.DrawBezier(line, 0, width * .82f, width * .8f, 0, width * 1.6f, width, width * 3.1f, width * .26f);
                        g.DrawBezier(line, width * .82f, 0, 0, width * .8f, width, width * 1.6f, width * .26f, width * 3.1f);
                        Diamond(g, strong, width * 1.52f, width * .47f, width * .42f);
                        Diamond(g, pale, width * .46f, width * 1.54f, width * .32f);
                        if (theme == 9) Diamond(g, Brushes.LightSkyBlue, width * 2.5f, width * .52f, width * .35f);
                        break;
                    case 1:
                    case 2:
                        for (int i = 0; i < 3; i++)
                        {
                            float x = width * (1.1f + i * .49f);
                            g.FillPolygon(strong, new[] { new PointF(x, width * .19f), new PointF(x + width * .36f, width * .19f),
                                new PointF(x + width * .08f, width * .68f), new PointF(x - width * .28f, width * .68f) });
                        }
                        break;
                    case 3:
                        g.DrawRectangle(line, width * .16f, width * .16f, width * .54f, width * .54f);
                        using (var red = new SolidBrush(Color.FromArgb(200, 203, 54, 46)))
                            for (int i = 0; i < 5; i++) Diamond(g, red, width * (1.1f + i * .43f), width * (.25f + (i % 3) * .19f), width * .19f);
                        if (corner == 1) g.FillEllipse(strong, width * 2.5f, width * .13f, width * .68f, width * .68f);
                        break;
                    case 4:
                        for (int i = 0; i < 4; i++)
                            g.FillEllipse(i % 2 == 0 ? Brushes.Lavender : pale, width * i * .52f, width * .14f, width * .83f, width * .69f);
                        Diamond(g, Brushes.Khaki, width * 2.75f, width * .46f, width * .39f);
                        break;
                    case 7:
                    case 8:
                        for (int i = 0; i < 5; i++)
                        {
                            float x = width * (.26f + i * .49f), y = width * (.23f + (i % 2) * .33f);
                            g.FillEllipse(i % 2 == 0 ? strong : pale, x, y, width * .42f, width * .28f);
                            if (theme == 7) g.FillEllipse(Brushes.MistyRose, x + width * .12f, y - width * .10f, width * .22f, width * .47f);
                        }
                        break;
                }
            }
        }

        private static void Diamond(Graphics g, Brush brush, float x, float y, float radius)
        {
            g.FillPolygon(brush, new[] { new PointF(x - radius, y), new PointF(x, y - radius),
                new PointF(x + radius, y), new PointF(x, y + radius) });
        }
    }
}
