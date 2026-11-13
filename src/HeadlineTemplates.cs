using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace gInk
{
    // The first four names are written by older versions to ArcDraw.notes.xml.
    internal enum NoteBackgroundShape
    {
        Rectangle, Rounded, Oval, Underline, News, Star, Spotlight, Document, Ribbon,
        Corners, Slash, Idea, Minimal, Offset, Technical, SpeechBubble, Dashed, Leaf,
        Sparkle, Notebook, Quote, Speed, Royal, Classic, Blossom, Paper, Hud,
        Alert, Night, Wave
    }

    internal sealed class HeadlineTemplate
    {
        private const float W = 360, H = 84;
        internal static readonly HeadlineTemplate[] All =
        {
            new HeadlineTemplate("클래식 사각형", 0x255280), new HeadlineTemplate("둥근 프레임", 0x257C9D),
            new HeadlineTemplate("타원", 0x6952AD, 60, 17, 60, 17), new HeadlineTemplate("기본 밑줄", 0x257C9D, 16, 8, 16, 21),
            new HeadlineTemplate("뉴스 사선", 0x195FD0, 82, 12, 43), new HeadlineTemplate("별 배지", 0x7552C6, 75, 12, 28),
            new HeadlineTemplate("스포트라이트", 0x25477F, 58, 12, 20, 19), new HeadlineTemplate("브리핑 카드", 0x3B78C8, 75),
            new HeadlineTemplate("북마크 리본", 0xCE2D60, 69, 12, 33), new HeadlineTemplate("코너 프레임", 0x214877, 27, 12, 27),
            new HeadlineTemplate("보라 사선", 0x622FB1, 77, 12, 36), new HeadlineTemplate("아이디어", 0xD98900, 71, 12, 28),
            new HeadlineTemplate("미니멀 포인트", 0x008D87, 31, 8, 37, 20), new HeadlineTemplate("이중 레이어", 0x1F5689, 29, 10, 38, 22),
            new HeadlineTemplate("테크 배지", 0x203E74, 74), new HeadlineTemplate("말풍선", 0x645BAA, 24, 12, 24, 24),
            new HeadlineTemplate("점선 캡슐", 0x416781, 24, 13, 24), new HeadlineTemplate("리프", 0x00816E, 73),
            new HeadlineTemplate("별빛 밑줄", 0x7946D7, 19, 7, 65, 22), new HeadlineTemplate("스프링 노트", 0x365AA7, 23, 23, 23, 11),
            new HeadlineTemplate("인용 카드", 0x6457AC, 39, 12, 39), new HeadlineTemplate("스피드 라인", 0x265ECC, 69, 8, 31, 20),
            new HeadlineTemplate("왕관 배너", 0x542E9A, 75, 13, 36), new HeadlineTemplate("골드 오너먼트", 0x9A680B, 51, 13, 51, 13),
            new HeadlineTemplate("벚꽃 캡슐", 0xC54D82, 66, 12, 48), new HeadlineTemplate("테이프 메모", 0x91694D, 39, 15, 20, 15),
            new HeadlineTemplate("사이버 HUD", 0x007FAD, 30, 15, 30, 15), new HeadlineTemplate("강조 알림", 0xCD3348, 73, 12, 29),
            new HeadlineTemplate("달과 구름", 0x213A76, 78, 13, 72), new HeadlineTemplate("웨이브", 0x1386A5, 20, 7, 43, 28),
        };

        internal readonly string Name;
        private readonly Color accent;
        private readonly RectangleF text;

        private HeadlineTemplate(string name, int rgb, float left = 15, float top = 12, float right = 20, float bottom = 12)
        {
            Name = name;
            accent = Color.FromArgb(255, (rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
            text = RectangleF.FromLTRB(left, top, W - right, H - bottom);
        }

        internal Rectangle CaptionBounds(Rectangle body)
        {
            return Rectangle.FromLTRB(
                body.X + (int)Math.Ceiling(text.Left * body.Width / W),
                body.Y + (int)Math.Ceiling(text.Top * body.Height / H),
                body.X + (int)Math.Floor(text.Right * body.Width / W),
                body.Y + (int)Math.Floor(text.Bottom * body.Height / H));
        }

        internal static HeadlineTemplate Get(NoteBackgroundShape shape) => All[(int)shape];

        internal static void Draw(Graphics graphics, Rectangle bounds, NoteBackgroundShape shape, int alpha)
        {
            if (alpha <= 0 || bounds.Width < 2 || bounds.Height < 2) return;
            GraphicsState saved = graphics.Save();
            try
            {
                graphics.TranslateTransform(bounds.X, bounds.Y);
                graphics.ScaleTransform(bounds.Width / W, bounds.Height / H);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Get(shape).DrawDesign(graphics, shape, alpha);
            }
            finally { graphics.Restore(saved); }
        }

        private void DrawDesign(Graphics g, NoteBackgroundShape shape, int alpha)
        {
            Color color = Color.FromArgb(alpha, accent);
            Color washColor = Color.FromArgb(alpha, (accent.R + 255 * 8) / 9, (accent.G + 255 * 8) / 9, (accent.B + 255 * 8) / 9);
            using (var line = new Pen(color, 2.4f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var tint = new SolidBrush(color))
            using (var pale = new SolidBrush(washColor))
            using (var paper = new SolidBrush(Color.FromArgb(alpha, 250, 252, 255)))
            using (var white = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255)))
            using (var glyphLine = new Pen(white, 2.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                switch (shape)
                {
                    case NoteBackgroundShape.Rectangle:
                        g.FillRectangle(white, 3, 4, 354, 76); g.DrawRectangle(line, 3, 4, 354, 76); break;
                    case NoteBackgroundShape.Rounded: Box(g, paper, line, 3, 4, 354, 76, 15); break;
                    case NoteBackgroundShape.Oval:
                        g.FillEllipse(paper, 3, 4, 354, 76); g.DrawEllipse(line, 3, 4, 354, 76); break;
                    case NoteBackgroundShape.Underline: g.DrawLine(line, 13, 72, 347, 72); break;
                    case NoteBackgroundShape.News:
                        Poly(g, paper, line, 32,6, 338,6, 314,78, 8,78);
                        Poly(g, tint, null, 28,3, 81,3, 59,80, 5,80);
                        Poly(g, tint, null, 344,6, 357,6, 333,78, 320,78);
                        Poly(g, white, null, 32,24, 32,59, 58,41); break;
                    case NoteBackgroundShape.Star:
                        Badge(g, paper, line, tint, 38); Star(g, white, 39, 42, 19, 8, 5); break;
                    case NoteBackgroundShape.Spotlight:
                        g.DrawLines(line, Points(7,42, 7,75, 352,75, 352,51));
                        using (var ray = new Pen(Color.FromArgb(alpha, 246, 167, 37), 4))
                        { g.DrawLine(ray, 15, 26, 8, 16); g.DrawLine(ray, 25, 23, 25, 9); g.DrawLine(ray, 34, 26, 43, 16); }
                        break;
                    case NoteBackgroundShape.Document:
                        Badge(g, paper, line, tint, 12);
                        g.DrawRectangle(glyphLine, 24, 21, 26, 41);
                        for (int y = 30; y <= 54; y += 8) g.DrawLine(glyphLine, 29, y, 45, y);
                        break;
                    case NoteBackgroundShape.Ribbon:
                        Poly(g, paper, line, 19,10, 352,10, 337,42, 352,73, 19,73);
                        Poly(g, tint, null, 8,4, 61,4, 61,81, 35,67, 8,81); break;
                    case NoteBackgroundShape.Corners:
                        g.DrawLines(line, Points(5,24, 5,6, 26,6)); g.DrawLines(line, Points(334,6, 355,6, 355,24));
                        g.DrawLines(line, Points(5,60, 5,78, 26,78)); g.DrawLines(line, Points(334,78, 355,78, 355,60));
                        Poly(g, pale, null, 319,58, 346,58, 329,76, 302,76); break;
                    case NoteBackgroundShape.Slash:
                        Poly(g, paper, line, 63,8, 355,8, 333,76, 41,76);
                        Poly(g, tint, null, 24,8, 48,8, 26,76, 2,76);
                        Poly(g, tint, null, 53,8, 58,8, 36,76, 31,76); break;
                    case NoteBackgroundShape.Idea:
                        Box(g, paper, line, 4, 6, 352, 72, 36);
                        g.FillEllipse(tint, 4, 6, 63, 72); g.DrawEllipse(glyphLine, 24, 22, 23, 25);
                        g.DrawLines(glyphLine, Points(28,44, 30,56, 41,56, 43,44));
                        g.DrawLine(glyphLine, 32, 62, 39, 62); break;
                    case NoteBackgroundShape.Minimal:
                        g.FillRectangle(tint, 7, 20, 7, 51); g.DrawLine(line, 22, 70, 318, 70);
                        for (int x = 326; x <= 352; x += 12) g.FillEllipse(tint, x, 67, 6, 6);
                        break;
                    case NoteBackgroundShape.Offset:
                        using (var shadow = new SolidBrush(Color.FromArgb(alpha, 34, 168, 191)))
                            Poly(g, shadow, null, 33,17, 356,17, 335,81, 12,81);
                        Poly(g, paper, line, 26,5, 347,5, 326,70, 5,70); break;
                    case NoteBackgroundShape.Technical:
                        Badge(g, paper, line, tint, 17);
                        Star(g, white, 37, 42, 20, 15, 8); g.FillEllipse(tint, 27, 32, 20, 20);
                        g.DrawEllipse(glyphLine, 31, 36, 12, 12); break;
                    case NoteBackgroundShape.SpeechBubble:
                        using (var bubble = new GraphicsPath())
                        {
                            bubble.AddArc(5,5,20,20,180,90); bubble.AddArc(335,5,20,20,270,90);
                            bubble.AddArc(335,47,20,20,0,90); bubble.AddLines(Points(74,67, 52,80, 50,67));
                            bubble.AddArc(5,47,20,20,90,90); bubble.CloseFigure();
                            g.FillPath(paper, bubble); g.DrawPath(line, bubble);
                        }
                        break;
                    case NoteBackgroundShape.Dashed:
                        line.DashStyle = DashStyle.Dash;
                        Box(g, paper, line, 5, 7, 350, 70, 26); break;
                    case NoteBackgroundShape.Leaf:
                        Badge(g, paper, line, tint, 15);
                        using (var leaf = new GraphicsPath())
                        {
                            leaf.AddBezier(21,58, 16,32, 37,23, 54,21); leaf.AddBezier(54,21, 53,48, 39,59, 21,58);
                            g.DrawPath(glyphLine, leaf); g.DrawLine(glyphLine, 17, 63, 48, 28);
                        }
                        break;
                    case NoteBackgroundShape.Sparkle:
                        g.DrawBezier(line, 7,73, 102,33, 230,94, 303,67);
                        Star(g, tint, 326, 36, 14, 5, 4); Star(g, tint, 347, 19, 9, 3, 4); break;
                    case NoteBackgroundShape.Notebook:
                        Box(g, paper, line, 6, 14, 348, 65, 13);
                        for (int x = 26; x < 80; x += 22) g.DrawArc(line, x, 3, 9, 21, 50, 300);
                        break;
                    case NoteBackgroundShape.Quote:
                        Box(g, pale, null, 5, 7, 350, 70, 16);
                        Quote(g, tint, 15, 16, false); Quote(g, tint, 328, 50, true); break;
                    case NoteBackgroundShape.Speed:
                        for (int x = 5; x <= 29; x += 12) Poly(g, tint, null, x + 18,24, x + 26,24, x + 8,71, x,71);
                        g.DrawLine(line, 50, 70, 338, 70); Poly(g, tint, null, 334,58, 354,58, 347,72, 327,72); break;
                    case NoteBackgroundShape.Royal:
                        Poly(g, paper, line, 27,7, 354,7, 333,77, 5,77); Poly(g, tint, null, 24,7, 75,7, 54,77, 3,77);
                        Poly(g, white, null, 24,32, 33,41, 41,24, 48,40, 58,31, 52,55, 29,55);
                        g.DrawLine(glyphLine, 30, 60, 51, 60); break;
                    case NoteBackgroundShape.Classic:
                        Box(g, pale, line, 4, 6, 352, 72, 28); Box(g, null, line, 9, 11, 342, 62, 23);
                        Star(g, tint, 30, 42, 13, 4, 4); Star(g, tint, 330, 42, 13, 4, 4); break;
                    case NoteBackgroundShape.Blossom:
                        Box(g, pale, line, 4, 6, 352, 72, 36);
                        for (int i = 0; i < 5; i++)
                        {
                            double a = i * Math.PI * 2 / 5;
                            g.FillEllipse(tint, 33 + (float)Math.Cos(a) * 12 - 7, 42 + (float)Math.Sin(a) * 12 - 7, 14, 14);
                        }
                        g.FillEllipse(white, 29, 38, 8, 8); g.FillEllipse(tint, 323, 33, 10, 7); g.FillEllipse(tint, 312, 50, 9, 6); break;
                    case NoteBackgroundShape.Paper:
                        Poly(g, pale, line, 7,9, 353,9, 353,74, 330,78, 311,75, 292,79, 262,76, 232,79, 210,76,
                            182,79, 160,76, 130,79, 109,76, 79,79, 54,76, 30,79, 7,77);
                        using (var tape = new SolidBrush(Color.FromArgb(alpha, 232, 195, 120)))
                            Poly(g, tape, null, 9,10, 55,2, 58,15, 12,23);
                        for (int y = 30; y <= 64; y += 16) g.DrawEllipse(line, 15, y, 8, 8);
                        break;
                    case NoteBackgroundShape.Hud:
                        Poly(g, paper, line, 5,25, 23,7, 93,7, 101,12, 336,12, 355,29, 355,59, 337,77, 224,77,
                            213,72, 22,72, 5,56);
                        g.DrawLines(line, Points(9,42, 9,28, 26,12, 88,12)); g.DrawLines(line, Points(267,72, 334,72, 351,56));
                        for (int x = 286; x <= 310; x += 10) Poly(g, tint, null, x,12, x + 7,12, x + 14,5, x + 7,5);
                        break;
                    case NoteBackgroundShape.Alert:
                        Box(g, paper, line, 11, 7, 345, 70, 32);
                        Poly(g, tint, null, 27,6, 39,6, 65,68, 60,77, 5,77, 2,68);
                        g.DrawLine(glyphLine, 33, 29, 33, 48); g.FillEllipse(white, 30, 54, 6, 7); break;
                    case NoteBackgroundShape.Night:
                        Badge(g, pale, line, tint, 32);
                        using (var moon = new GraphicsPath())
                        {
                            moon.AddBezier(42,22, 20,13, 11,58, 34,62); moon.AddBezier(34,62, 46,65, 53,53, 53,50);
                            moon.AddBezier(53,50, 29,57, 26,32, 42,22); g.FillPath(white, moon);
                        }
                        Star(g, white, 53, 23, 7, 3, 4);
                        using (var cloud = new GraphicsPath())
                        {
                            cloud.AddBezier(294,77, 295,62, 311,59, 318,66);
                            cloud.AddBezier(318,66, 323,43, 347,47, 349,60);
                            cloud.AddBezier(349,60, 358,58, 360,76, 350,77); cloud.CloseFigure();
                            g.FillPath(tint, cloud);
                        }
                        break;
                    case NoteBackgroundShape.Wave:
                        using (var wave = new GraphicsPath())
                        {
                            wave.AddBezier(4,67, 73,22, 112,89, 185,66);
                            wave.AddBezier(185,66, 239,43, 296,85, 356,52);
                            wave.AddLine(356,52,356,80); wave.AddLine(356,80,4,80); wave.CloseFigure();
                            g.FillPath(pale, wave);
                        }
                        g.DrawBezier(line, 4,67, 73,22, 112,89, 185,66);
                        g.DrawBezier(line, 185,66, 239,43, 296,85, 356,52);
                        g.FillEllipse(tint, 332, 30, 8, 8); g.FillEllipse(tint, 349, 19, 6, 6); break;
                }
            }
        }

        private static void Badge(Graphics g, Brush fill, Pen outline, Brush tint, float radius)
        {
            Box(g, fill, outline, 4, 5, 352, 74, radius);
            using (GraphicsPath path = Rounded(4, 5, 352, 74, radius))
            {
                GraphicsState state = g.Save();
                try { g.SetClip(path, CombineMode.Intersect); Poly(g, tint, null, 3,3, 74,3, 57,81, 3,81); }
                finally { g.Restore(state); }
            }
        }

        private static void Quote(Graphics g, Brush tint, float x, float y, bool close)
        {
            for (int i = 0; i < 2; i++, x += 10)
            {
                g.FillEllipse(tint, x, y + (close ? 0 : 9), 7, 9);
                Poly(g, tint, null, x, y + 11, x + 7, y + 2, x + 5, y + (close ? 19 : 0), x + 2, y + 14);
            }
        }

        private static GraphicsPath Rounded(float x, float y, float w, float h, float r)
        {
            float d = Math.Min(r * 2, Math.Min(w, h));
            var path = new GraphicsPath();
            path.AddArc(x, y, d, d, 180, 90); path.AddArc(x + w - d, y, d, d, 270, 90);
            path.AddArc(x + w - d, y + h - d, d, d, 0, 90); path.AddArc(x, y + h - d, d, d, 90, 90);
            path.CloseFigure(); return path;
        }

        private static void Box(Graphics g, Brush brush, Pen pen, float x, float y, float w, float h, float radius)
        {
            using (GraphicsPath path = Rounded(x, y, w, h, radius))
            { if (brush != null) g.FillPath(brush, path); if (pen != null) g.DrawPath(pen, path); }
        }

        private static PointF[] Points(params float[] xy)
        {
            var points = new PointF[xy.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = new PointF(xy[i * 2], xy[i * 2 + 1]);
            return points;
        }

        private static void Poly(Graphics g, Brush brush, Pen pen, params float[] xy)
        {
            PointF[] points = Points(xy);
            if (brush != null) g.FillPolygon(brush, points);
            if (pen != null) g.DrawPolygon(pen, points);
        }

        private static void Star(Graphics g, Brush fill, float x, float y, float radius, float inner, int tips)
        {
            var points = new PointF[tips * 2];
            for (int i = 0; i < points.Length; i++)
            {
                double a = i * Math.PI / tips - Math.PI / 2;
                float r = i % 2 == 0 ? radius : inner;
                points[i] = new PointF(x + (float)Math.Cos(a) * r, y + (float)Math.Sin(a) * r);
            }
            g.FillPolygon(fill, points);
        }
    }

    internal sealed class HeadlineTemplatePicker : ToolStripDropDown
    {
        private readonly Button[] choices = new Button[HeadlineTemplate.All.Length];
        internal event Action<NoteBackgroundShape> Selected;

        internal HeadlineTemplatePicker()
        {
            Name = "HeadlineTemplatePicker";
            Padding = new Padding(3);
            var grid = new TableLayoutPanel
            {
                Name = "HeadlineTemplateGrid", Size = new Size(426, 400), ColumnCount = 3,
                RowCount = 10, BackColor = Color.FromArgb(237, 242, 248), Margin = Padding.Empty
            };
            for (int column = 0; column < 3; column++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
            for (int row = 0; row < 10; row++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 10));
            for (int i = 0; i < choices.Length; i++)
            {
                var shape = (NoteBackgroundShape)i;
                var tile = new Button
                {
                    Name = "Template" + shape, AccessibleName = HeadlineTemplate.Get(shape).Name, Dock = DockStyle.Fill,
                    FlatStyle = FlatStyle.Flat, Margin = new Padding(1), BackColor = Color.White, TabIndex = i
                };
                tile.FlatAppearance.BorderSize = 0;
                tile.Paint += (_, e) =>
                {
                    var bounds = new Rectangle(5, 3, tile.Width - 10, tile.Height - 19);
                    HeadlineTemplate.Draw(e.Graphics, bounds, shape, 255);
                    TextRenderer.DrawText(e.Graphics, tile.AccessibleName, SystemFonts.MessageBoxFont,
                        new Rectangle(2, bounds.Bottom, tile.Width - 4, 15), Color.FromArgb(32, 52, 74),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                };
                tile.Click += delegate { Close(ToolStripDropDownCloseReason.ItemClicked); Selected?.Invoke(shape); };
                choices[i] = tile;
                grid.Controls.Add(tile, i % 3, i / 3);
            }
            Items.Add(new ToolStripControlHost(grid) { AutoSize = false, Size = grid.Size, Margin = Padding.Empty });
        }

        internal void ShowFor(Button source, NoteBackgroundShape current)
        {
            for (int i = 0; i < choices.Length; i++)
            {
                choices[i].BackColor = i == (int)current ? Color.FromArgb(216, 240, 255) : Color.White;
                choices[i].FlatAppearance.BorderSize = i == (int)current ? 1 : 0;
                choices[i].FlatAppearance.BorderColor = Color.FromArgb(50, 117, 169);
            }
            Show(source, new Point(source.Width, source.Height), ToolStripDropDownDirection.BelowLeft);
        }
    }
}
