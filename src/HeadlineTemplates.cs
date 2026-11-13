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
        Alert, Night, Wave,
        Frame31, Frame32, Frame33, Frame34, Frame35, Frame36, Frame37, Frame38, Frame39, Frame40,
        Frame41, Frame42, Frame43, Frame44, Frame45, Frame46, Frame47, Frame48, Frame49, Frame50,
        Frame51, Frame52, Frame53, Frame54, Frame55, Frame56, Frame57, Frame58, Frame59, Frame60,
        Frame61, Frame62, Frame63, Frame64, Frame65, Frame66, Frame67, Frame68, Frame69, Frame70,
        Frame71, Frame72, Frame73, Frame74, Frame75, Frame76, Frame77, Frame78, Frame79, Frame80,
        Frame81, Frame82, Frame83, Frame84, Frame85, Frame86, Frame87, Frame88, Frame89, Frame90,
        Frame91, Frame92, Frame93, Frame94, Frame95, Frame96, Frame97, Frame98, Frame99, Frame100,
        Frame101, Frame102, Frame103, Frame104, Frame105, Frame106, Frame107, Frame108, Frame109, Frame110,
        Frame111, Frame112, Frame113, Frame114, Frame115, Frame116, Frame117, Frame118, Frame119, Frame120,
    }

    internal sealed partial class HeadlineTemplate
    {
        private const float W = 360, H = 84;
        private static readonly HeadlineTemplate[] Original =
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
        internal const int Count = 120;

        internal readonly string Name;
        private readonly Color accent;
        internal Color Accent => accent;
        private Color? defaultFill;
        internal bool HasFill => this != Original[(int)NoteBackgroundShape.Underline] &&
            this != Original[(int)NoteBackgroundShape.Spotlight] && this != Original[(int)NoteBackgroundShape.Corners] &&
            this != Original[(int)NoteBackgroundShape.Minimal] && this != Original[(int)NoteBackgroundShape.Sparkle] &&
            this != Original[(int)NoteBackgroundShape.Speed];
        internal Color DefaultFill
        {
            get
            {
                if (defaultFill.HasValue) return defaultFill.Value;
                if (this == Original[(int)NoteBackgroundShape.Rectangle]) return Color.White;
                if (this == Original[(int)NoteBackgroundShape.Quote] || this == Original[(int)NoteBackgroundShape.Classic] ||
                    this == Original[(int)NoteBackgroundShape.Blossom] || this == Original[(int)NoteBackgroundShape.Paper] ||
                    this == Original[(int)NoteBackgroundShape.Night] || this == Original[(int)NoteBackgroundShape.Wave]) return Pale(accent);
                return Color.FromArgb(250, 252, 255);
            }
        }
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

        internal static HeadlineTemplate Get(NoteBackgroundShape shape) =>
            (int)shape < Original.Length ? Original[(int)shape] : Extra.Value[(int)shape - Original.Length];

        internal static void Draw(Graphics graphics, Rectangle bounds, NoteBackgroundShape shape, int alpha,
            Color? frameColor, Color? fillColor)
        {
            if (alpha <= 0 || bounds.Width < 2 || bounds.Height < 2) return;
            GraphicsState saved = graphics.Save();
            try
            {
                graphics.TranslateTransform(bounds.X, bounds.Y);
                graphics.ScaleTransform(bounds.Width / W, bounds.Height / H);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                HeadlineTemplate template = Get(shape);
                if ((int)shape < Original.Length) template.DrawDesign(graphics, shape, alpha, frameColor, fillColor);
                else template.DrawExtra(graphics, (int)shape + 1, alpha, frameColor, fillColor);
            }
            finally { graphics.Restore(saved); }
        }

        private void DrawDesign(Graphics g, NoteBackgroundShape shape, int alpha, Color? frameColor, Color? fillColor)
        {
            Color selected = frameColor ?? accent;
            Color color = Color.FromArgb(alpha, selected);
            Color washColor = Color.FromArgb(alpha, fillColor ?? Pale(accent));
            Color surfaceColor = Color.FromArgb(alpha, fillColor ?? DefaultFill);
            // Keep the small decoration inside a colored badge readable even when its new fill is pale.
            bool light = 299 * selected.R + 587 * selected.G + 114 * selected.B > 165000;
            Color glyph = Color.FromArgb(alpha, frameColor.HasValue && light ? Color.FromArgb(28, 39, 55) : Color.White);
            using (var line = new Pen(color, 2.4f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var tint = new SolidBrush(color))
            using (var pale = new SolidBrush(washColor))
            using (var paper = new SolidBrush(surfaceColor))
            using (var whitePaper = new SolidBrush(Color.FromArgb(alpha, fillColor ?? Color.White)))
            using (var mark = new SolidBrush(glyph))
            using (var glyphLine = new Pen(mark, 2.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (fillColor.HasValue && !HasFill) Box(g, paper, null, 5, 5, 350, 73, 10);
                switch (shape)
                {
                    case NoteBackgroundShape.Rectangle:
                        g.FillRectangle(whitePaper, 3, 4, 354, 76); g.DrawRectangle(line, 3, 4, 354, 76); break;
                    case NoteBackgroundShape.Rounded: Box(g, paper, line, 3, 4, 354, 76, 15); break;
                    case NoteBackgroundShape.Oval:
                        g.FillEllipse(paper, 3, 4, 354, 76); g.DrawEllipse(line, 3, 4, 354, 76); break;
                    case NoteBackgroundShape.Underline: g.DrawLine(line, 13, 72, 347, 72); break;
                    case NoteBackgroundShape.News:
                        Poly(g, paper, line, 32,6, 338,6, 314,78, 8,78);
                        Poly(g, tint, null, 28,3, 81,3, 59,80, 5,80);
                        Poly(g, tint, null, 344,6, 357,6, 333,78, 320,78);
                        Poly(g, mark, null, 32,24, 32,59, 58,41); break;
                    case NoteBackgroundShape.Star:
                        Badge(g, paper, line, tint, 38); Star(g, mark, 39, 42, 19, 8, 5); break;
                    case NoteBackgroundShape.Spotlight:
                        g.DrawLines(line, Points(7,42, 7,75, 352,75, 352,51));
                        using (var ray = new Pen(Color.FromArgb(alpha, frameColor.HasValue ? Mix(selected, Color.White, 1, 4) : Color.FromArgb(246, 167, 37)), 4))
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
                        using (var shadow = new SolidBrush(Color.FromArgb(alpha, frameColor.HasValue ? Mix(selected, Color.White, 1, 3) : Color.FromArgb(34, 168, 191))))
                            Poly(g, shadow, null, 33,17, 356,17, 335,81, 12,81);
                        Poly(g, paper, line, 26,5, 347,5, 326,70, 5,70); break;
                    case NoteBackgroundShape.Technical:
                        Badge(g, paper, line, tint, 17);
                        Star(g, mark, 37, 42, 20, 15, 8); g.FillEllipse(tint, 27, 32, 20, 20);
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
                        Poly(g, mark, null, 24,32, 33,41, 41,24, 48,40, 58,31, 52,55, 29,55);
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
                        g.FillEllipse(mark, 29, 38, 8, 8); g.FillEllipse(tint, 323, 33, 10, 7); g.FillEllipse(tint, 312, 50, 9, 6); break;
                    case NoteBackgroundShape.Paper:
                        Poly(g, pale, line, 7,9, 353,9, 353,74, 330,78, 311,75, 292,79, 262,76, 232,79, 210,76,
                            182,79, 160,76, 130,79, 109,76, 79,79, 54,76, 30,79, 7,77);
                        using (var tape = new SolidBrush(Color.FromArgb(alpha, frameColor.HasValue ? Mix(selected, Color.White, 1, 2) : Color.FromArgb(232, 195, 120))))
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
                        g.DrawLine(glyphLine, 33, 29, 33, 48); g.FillEllipse(mark, 30, 54, 6, 7); break;
                    case NoteBackgroundShape.Night:
                        Badge(g, pale, line, tint, 32);
                        using (var moon = new GraphicsPath())
                        {
                            moon.AddBezier(42,22, 20,13, 11,58, 34,62); moon.AddBezier(34,62, 46,65, 53,53, 53,50);
                            moon.AddBezier(53,50, 29,57, 26,32, 42,22); g.FillPath(mark, moon);
                        }
                        Star(g, mark, 53, 23, 7, 3, 4);
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

        internal static Color Pale(Color color) => Mix(color, Color.White, 8, 9);

        private static Color Mix(Color from, Color to, int part, int total)
        {
            return Color.FromArgb((from.R * (total - part) + to.R * part) / total,
                (from.G * (total - part) + to.G * part) / total,
                (from.B * (total - part) + to.B * part) / total);
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
        private const int PageSize = 30;
        private readonly Button[] choices = new Button[PageSize];
        private readonly Button[] categories = new Button[4];
        private int page;
        private NoteBackgroundShape current;
        private Color? previewColor, previewFill;
        internal event Action<NoteBackgroundShape> Selected;

        internal HeadlineTemplatePicker()
        {
            Name = "HeadlineTemplatePicker";
            Padding = new Padding(3);
            var surface = new Panel { Size = new Size(486, 464), Margin = Padding.Empty, BackColor = Color.FromArgb(237, 242, 248) };
            string[] titles = { "기본 1–30", "모던 31–60", "캐릭터 61–90", "소품 91–120" };
            for (int i = 0; i < categories.Length; i++)
            {
                int category = i;
                var tab = new Button
                {
                    Name = "HeadlineCategory" + i, Text = titles[i], FlatStyle = FlatStyle.Flat,
                    Bounds = new Rectangle(i * 121 + 1, 1, 120, 27), BackColor = Color.White
                };
                tab.FlatAppearance.BorderSize = 0;
                tab.Click += delegate { ShowCategory(category); };
                categories[i] = tab;
                surface.Controls.Add(tab);
            }
            var grid = new TableLayoutPanel
            {
                Name = "HeadlineTemplateGrid", Bounds = new Rectangle(0, 30, 486, 434), ColumnCount = 3,
                RowCount = 10, BackColor = Color.FromArgb(237, 242, 248), Margin = Padding.Empty
            };
            for (int column = 0; column < 3; column++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3));
            for (int row = 0; row < 10; row++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 10));
            for (int i = 0; i < choices.Length; i++)
            {
                var tile = new Button
                {
                    Dock = DockStyle.Fill,
                    FlatStyle = FlatStyle.Flat, Margin = new Padding(1), BackColor = Color.White, TabIndex = i
                };
                tile.FlatAppearance.BorderSize = 0;
                tile.Paint += (_, e) =>
                {
                    var bounds = new Rectangle(5, 3, tile.Width - 10, tile.Height - 19);
                    HeadlineTemplate.Draw(e.Graphics, bounds, (NoteBackgroundShape)tile.Tag, 255, previewColor, previewFill);
                    TextRenderer.DrawText(e.Graphics, tile.AccessibleName, SystemFonts.MessageBoxFont,
                        new Rectangle(2, bounds.Bottom, tile.Width - 4, 15), Color.FromArgb(32, 52, 74),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                };
                tile.Click += delegate
                {
                    var shape = (NoteBackgroundShape)tile.Tag;
                    Close(ToolStripDropDownCloseReason.ItemClicked);
                    Selected?.Invoke(shape);
                };
                choices[i] = tile;
                grid.Controls.Add(tile, i % 3, i / 3);
            }
            surface.Controls.Add(grid);
            ShowCategory(0);
            Items.Add(new ToolStripControlHost(surface) { AutoSize = false, Size = surface.Size, Margin = Padding.Empty });
        }

        private void ShowCategory(int category)
        {
            page = category;
            for (int i = 0; i < categories.Length; i++)
                categories[i].BackColor = i == page ? Color.FromArgb(216, 240, 255) : Color.White;
            for (int i = 0; i < choices.Length; i++)
            {
                int index = page * PageSize + i;
                var shape = (NoteBackgroundShape)index;
                Button tile = choices[i];
                tile.Name = "Template" + shape;
                tile.AccessibleName = (index + 1).ToString("D2") + "  " + HeadlineTemplate.Get(shape).Name;
                tile.Tag = shape;
                tile.BackColor = shape == current ? Color.FromArgb(216, 240, 255) : Color.White;
                tile.FlatAppearance.BorderSize = shape == current ? 1 : 0;
                tile.FlatAppearance.BorderColor = Color.FromArgb(50, 117, 169);
                tile.Invalidate();
            }
        }

        internal void ShowFor(Button source, NoteBackgroundShape selected, Color? color, Color? fill)
        {
            current = selected;
            previewColor = color;
            previewFill = fill;
            ShowCategory((int)selected / PageSize);
            Show(source, new Point(source.Width, source.Height), ToolStripDropDownDirection.BelowLeft);
        }
    }
}
