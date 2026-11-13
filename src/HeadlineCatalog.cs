using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;

namespace gInk
{
    internal sealed partial class HeadlineTemplate
    {
        private enum Banner
        {
            News, Play, Crest, Info, Tag, Green, Paper, Corners, Folder, Stripes,
            Capsule, Scope, Broadcast, Cinema, Floating, Quote, Glass, Spotlight, Medal, Circuit,
            Racing, Laurel, Alert, Speech, Sparkle, Hud, Fold, Speed, Wave, Gradient,
            Bevel, Cloud, Ribbon, Soft, Space, Scroll, Ticket, Checkered, Industrial, Night,
            Field, AirMail, Snow, Gold, Liquid, Modern, Film, Sound
        }

        private Banner banner;
        private static readonly Lazy<HeadlineTemplate[]> Extra = new Lazy<HeadlineTemplate[]>(CreateExtra);
        private static readonly Lazy<Bitmap[]> Illustrations = new Lazy<Bitmap[]>(LoadIllustrations);

        private static HeadlineTemplate[] CreateExtra()
        {
            var designs = new List<HeadlineTemplate>();
            void Add(string name, int rgb, Banner style, float left = 24, float right = 24, float top = 17, float bottom = 17)
            {
                var design = new HeadlineTemplate(name, rgb, left, top, right, bottom) { banner = style };
                design.defaultFill = Pale(design.Accent);
                designs.Add(design);
            }

            Add("블루 뉴스", 0x166BDF, Banner.News, 85, 48);
            Add("플레이 캡슐", 0x176AD1, Banner.Play, 90);
            Add("왕관 북마크", 0x7041B5, Banner.Crest, 80);
            Add("안내 카드", 0x008784, Banner.Info, 81);
            Add("보라 태그", 0x783CB9, Banner.Tag, 75, 39);
            Add("초록 잎사귀", 0x008C79, Banner.Green, 81);
            Add("테이프 페이퍼", 0x977348, Banner.Paper, 36);
            Add("포커스 코너", 0x1657AD, Banner.Corners);
            Add("폴더 탭", 0x2874E0, Banner.Folder, 22, 22, 25);
            Add("핑크 사선", 0xCB2758, Banner.Stripes, 108, 38);
            Add("보라 육각 캡슐", 0x6337B3, Banner.Capsule, 44, 43);
            Add("조준 프레임", 0x007D8D, Banner.Scope, 31, 31, 12, 22);
            Add("블루 방송 라인", 0x0A55AC, Banner.Broadcast, 91, 59);
            Add("클래퍼 필름", 0x193D77, Banner.Cinema, 76, 50, 24);
            Add("플로팅 카드", 0x26578D, Banner.Floating, 23, 29, 16, 25);
            Add("보라 인용", 0x7943C2, Banner.Quote, 40, 42);
            Add("블루 글래스", 0x306AD1, Banner.Glass, 90, 34);
            Add("조명 캡슐", 0xB17A09, Banner.Spotlight, 81);
            Add("금빛 메달", 0xAB760A, Banner.Medal, 91);
            Add("회로 프레임", 0x1955AA, Banner.Circuit, 40, 39);
            Add("옐로 레이싱", 0x1D426E, Banner.Racing, 36, 41, 22, 25);
            Add("월계수 프레임", 0xA26D09, Banner.Laurel, 49, 49);
            Add("주의 배너", 0xD82754, Banner.Alert, 84, 39);
            Add("강조 말풍선", 0x7239AC, Banner.Speech, 23, 25, 15, 30);
            Add("별빛 구분선", 0x2C4E80, Banner.Sparkle, 22, 22, 12, 30);
            Add("시안 HUD", 0x008BAE, Banner.Hud, 43, 43);
            Add("접힌 종이", 0xA56B00, Banner.Fold, 23, 44);
            Add("보라 속도선", 0x7943C6, Banner.Speed, 88, 43, 12, 26);
            Add("아쿠아 웨이브", 0x008CA8, Banner.Wave, 23, 42, 9, 29);
            Add("보라 그라데이션", 0x5238C8, Banner.Gradient, 111);

            Add("로봇", 0x0A66CD, Banner.Bevel, 96, 44);
            Add("고양이", 0xDA6B22, Banner.Soft, 96, 32);
            Add("토끼 리본", 0xC53770, Banner.Ribbon, 96, 44);
            Add("곰돌이", 0xA05B37, Banner.Checkered, 96, 41);
            Add("여우", 0xE56D15, Banner.News, 96, 48);
            Add("강아지 구름", 0x3678CE, Banner.Cloud, 96, 31);
            Add("병아리 말풍선", 0xAE7A12, Banner.Speech, 96, 28, 15, 29);
            Add("우주비행사", 0x7144C6, Banner.Space, 96, 46);
            Add("유령", 0x673FA7, Banner.Liquid, 96, 32);
            Add("마법사 두루마리", 0xBD8431, Banner.Scroll, 96, 35);
            Add("탐정", 0x8D552B, Banner.Ticket, 96, 33);
            Add("요리사", 0xC93447, Banner.Checkered, 96, 41);
            Add("작업자", 0xA17513, Banner.Industrial, 96, 40);
            Add("상담원", 0x2169C5, Banner.Bevel, 96, 41);
            Add("독서", 0x408F39, Banner.Soft, 96, 29);
            Add("과학자", 0x2684BA, Banner.Capsule, 96, 45);
            Add("게이밍", 0x145E9A, Banner.Night, 96, 45);
            Add("화가", 0xC46729, Banner.Modern, 96, 39);
            Add("축구", 0x268A40, Banner.Field, 96, 53);
            Add("음악 감상", 0x9A3EBD, Banner.Sound, 96, 42);
            Add("여행 사진", 0x2856A3, Banner.AirMail, 96, 43);
            Add("정원사", 0x4F8B37, Banner.Soft, 96, 32);
            Add("눈사람", 0x258DC5, Banner.Snow, 96, 42);
            Add("사자 왕관", 0xAA7414, Banner.Gold, 96, 43);
            Add("펭귄 얼음", 0x3466B7, Banner.Snow, 96, 42);
            Add("판다 대나무", 0x467F32, Banner.Field, 96, 46);
            Add("다람쥐 단풍", 0xB3602A, Banner.Scroll, 96, 35);
            Add("해달 물결", 0x1687BB, Banner.Wave, 96, 34, 9, 29);
            Add("돌고래", 0x009BC1, Banner.Liquid, 96, 34);
            Add("잠자는 양", 0x8558C3, Banner.Cloud, 96, 31);

            Add("위스키", 0x995213, Banner.Gold, 96, 42);
            Add("와인 보틀", 0x861F49, Banner.Liquid, 96, 41);
            Add("맥주", 0xCE8307, Banner.Soft, 96, 33);
            Add("칵테일", 0xC42A68, Banner.News, 96, 45);
            Add("와인잔", 0x8F2344, Banner.Wave, 96, 33, 9, 28);
            Add("커피", 0x9B5C32, Banner.Ticket, 96, 31);
            Add("방송 마이크", 0x1767AE, Banner.Broadcast, 96, 57);
            Add("카메라", 0x283F62, Banner.Racing, 96, 42, 21, 24);
            Add("영화 필름", 0x333943, Banner.Film, 96, 35, 24, 24);
            Add("확성기", 0xCE2A36, Banner.Alert, 96, 42);
            Add("전구", 0xC78105, Banner.Bevel, 96, 43);
            Add("연필", 0x244F85, Banner.Speed, 96, 43, 12, 26);
            Add("책", 0x945324, Banner.Ticket, 96, 33);
            Add("트로피", 0x836015, Banner.Gold, 96, 43);
            Add("알람 시계", 0xB9292B, Banner.Soft, 96, 31);
            Add("서류 가방", 0x8C5732, Banner.Racing, 96, 41, 20, 24);
            Add("선물 리본", 0xBC263B, Banner.Liquid, 96, 33);
            Add("음표", 0x7141AB, Banner.Sound, 96, 42);
            Add("초록 잎", 0x4D8D27, Banner.Wave, 96, 34, 9, 28);
            Add("히비스커스", 0xCC4C7D, Banner.Cloud, 96, 31);
            Add("지구본", 0x2164A7, Banner.Modern, 96, 43);
            Add("비행기", 0x2498D1, Banner.Cloud, 96, 37);
            Add("안전모", 0x5C6B7A, Banner.Industrial, 96, 43);
            Add("쇼핑 카트", 0xB92540, Banner.Alert, 96, 39);
            Add("졸업", 0x274678, Banner.Racing, 96, 43, 20, 24);
            Add("하트", 0xD33863, Banner.Wave, 96, 34, 9, 28);
            Add("보호 방패", 0x126BB1, Banner.Bevel, 96, 43);
            Add("돋보기", 0x1981B4, Banner.Glass, 96, 43);
            Add("목표", 0xC32936, Banner.Capsule, 96, 54);
            Add("캘린더", 0xB82B3F, Banner.Fold, 96, 48);
            return designs.ToArray();
        }

        private void DrawExtra(Graphics g, int number, int alpha, Color? frameColor, Color? fillColor)
        {
            Color edge = frameColor ?? accent;
            Color surface = fillColor ?? DefaultFill;
            using (var pen = new Pen(Color.FromArgb(alpha, edge), 2.7f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            using (var strong = new SolidBrush(Color.FromArgb(alpha, edge)))
            using (var soft = new SolidBrush(Color.FromArgb(alpha, Mix(edge, Color.White, 3, 5))))
            using (var fill = new SolidBrush(Color.FromArgb(alpha, surface)))
            using (var white = new SolidBrush(Color.FromArgb(alpha, Color.White)))
            {
                DrawBanner(g, number, pen, strong, soft, fill, white, edge, alpha, fillColor.HasValue);
            }
            if (number >= 61) DrawIllustration(g, number, alpha);
        }

        private void DrawBanner(Graphics g, int number, Pen pen, Brush strong, Brush soft, Brush fill,
            Brush white, Color edge, int alpha, bool explicitFill)
        {
            switch (banner)
            {
                case Banner.News:
                case Banner.Broadcast:
                case Banner.Stripes:
                case Banner.Glass:
                    Poly(g, fill, pen, 30,13, 354,13, 328,71, 5,71);
                    Poly(g, strong, null, 26,13, 85,13, 60,71, 2,71);
                    if (banner == Banner.News || banner == Banner.Glass)
                        Poly(g, soft, null, 48,5, 99,5, 94,13, 45,13);
                    if (banner != Banner.News)
                        for (int i = 0; i < 3; i++)
                            Poly(g, i % 2 == 0 ? soft : strong, null, 303 + i * 13,60, 312 + i * 13,60, 306 + i * 13,74, 297 + i * 13,74);
                    break;
                case Banner.Play:
                case Banner.Info:
                case Banner.Green:
                case Banner.Gradient:
                    Box(g, fill, pen, 5, 9, 350, 66, banner == Banner.Info || banner == Banner.Green ? 16 : 33);
                    using (var badge = Rounded(5, 9, 350, 66, banner == Banner.Info || banner == Banner.Green ? 16 : 33))
                    {
                        GraphicsState state = g.Save();
                        try
                        {
                            g.SetClip(badge, CombineMode.Intersect);
                            if (banner == Banner.Gradient)
                            {
                                using (var gradient = new LinearGradientBrush(new RectangleF(0, 6, 108, 70),
                                    Color.FromArgb(alpha, Mix(edge, Color.White, 1, 4)), Color.FromArgb(alpha, edge), 38f))
                                    Poly(g, gradient, null, 2,7, 111,7, 76,78, 2,78);
                            }
                            else
                            {
                                Poly(g, soft, null, 2,7, 89,7, 68,78, 2,78);
                                Poly(g, strong, null, 2,7, 72,7, 51,78, 2,78);
                            }
                        }
                        finally { g.Restore(state); }
                    }
                    if (banner == Banner.Play) Poly(g, white, null, 28,28, 28,57, 51,42);
                    if (banner == Banner.Info || banner == Banner.Green)
                    {
                        using (var mark = new Pen(white, 2.6f))
                        {
                            if (banner == Banner.Info) { g.DrawEllipse(mark, 20, 24, 32, 34); g.DrawLine(mark, 36, 39, 36, 49); g.FillEllipse(white, 34, 32, 4, 4); }
                            else { g.DrawEllipse(mark, 24, 25, 24, 29); g.DrawLine(mark, 20, 57, 50, 22); }
                        }
                    }
                    break;
                case Banner.Crest:
                    Box(g, fill, pen, 8, 12, 347, 60, 14);
                    Poly(g, strong, null, 8,5, 72,5, 72,80, 40,65, 8,80);
                    Poly(g, white, null, 21,29, 30,39, 41,24, 51,39, 61,29, 55,52, 26,52);
                    break;
                case Banner.Tag:
                case Banner.Capsule:
                case Banner.Bevel:
                case Banner.Space:
                case Banner.Night:
                    Poly(g, fill, pen, 24,10, 328,10, 356,40, 330,74, 24,74, 4,42);
                    if (banner == Banner.Tag)
                    {
                        Poly(g, strong, null, 24,10, 76,10, 59,74, 24,74, 4,42);
                        g.FillEllipse(white, 30, 36, 12, 12);
                    }
                    else
                    {
                        g.DrawLines(pen, Points(15,44, 31,18, 48,18)); g.DrawLines(pen, Points(303,66, 326,66, 344,42));
                        if (banner == Banner.Space) Star(g, soft, 328, 19, 12, 4, 4);
                        if (banner == Banner.Night) for (int i = 0; i < 3; i++) g.DrawLines(pen, Points(303+i*10,51, 309+i*10,57, 303+i*10,63));
                    }
                    break;
                case Banner.Paper:
                case Banner.Fold:
                    Poly(g, fill, pen, 8,11, 316,11, 352,34, 352,74, 292,76, 232,74, 178,77, 121,74, 67,77, 8,74);
                    if (banner == Banner.Fold) Poly(g, soft, null, 316,11, 316,34, 352,34);
                    else { Poly(g, soft, null, 15,14, 68,4, 72,16, 18,24); for (int y = 30; y < 70; y += 16) g.DrawEllipse(pen, 17, y, 7, 7); }
                    break;
                case Banner.Corners:
                case Banner.Scope:
                case Banner.Sparkle:
                case Banner.Speed:
                    if (explicitFill || banner == Banner.Scope) Box(g, fill, banner == Banner.Scope ? pen : null, 5, 6, 350, 72, 14);
                    if (banner == Banner.Corners || banner == Banner.Scope)
                    {
                        g.DrawLines(pen, Points(14,24,14,14,30,14)); g.DrawLines(pen, Points(330,14,346,14,346,24));
                        g.DrawLines(pen, Points(14,58,14,69,30,69)); g.DrawLines(pen, Points(330,69,346,69,346,58));
                    }
                    else
                    {
                        g.DrawLine(pen, banner == Banner.Speed ? 73 : 8, 67, banner == Banner.Speed ? 316 : 159, 67);
                        if (banner == Banner.Sparkle)
                        { Star(g, strong, 180, 67, 13, 4, 4); g.DrawLine(pen, 201, 67, 351, 67); }
                        else
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                Poly(g, strong, null, 22+i*17,31, 30+i*17,31, 13+i*17,69, 5+i*17,69);
                                g.FillEllipse(strong, 323+i*10, 64, 5, 5);
                            }
                        }
                    }
                    break;
                case Banner.Folder:
                    Poly(g, soft, null, 108,20, 117,10, 188,10, 201,20);
                    Poly(g, strong, null, 21,23, 34,7, 113,7, 129,23);
                    Box(g, fill, pen, 5, 21, 350, 54, 12);
                    break;
                case Banner.Cinema:
                case Banner.Film:
                    Poly(g, strong, null, 28,11, 354,11, 333,75, 6,75);
                    Poly(g, fill, null, 49,21, 339,21, 325,65, 33,65);
                    for (int x = 44; x < 339; x += 18)
                    {
                        g.FillRectangle(white, x, 14, 8, 4); g.FillRectangle(white, x - 12, 68, 8, 4);
                    }
                    if (banner == Banner.Cinema) Poly(g, soft, null, 31,4, 93,0, 99,10, 27,15);
                    break;
                case Banner.Floating:
                    Box(g, soft, null, 18, 19, 337, 60, 14);
                    Box(g, fill, pen, 5, 7, 337, 61, 14);
                    break;
                case Banner.Quote:
                    Box(g, fill, pen, 5, 9, 350, 65, 16);
                    Quote(g, strong, 14, 10, false); Quote(g, strong, 326, 55, true);
                    break;
                case Banner.Spotlight:
                case Banner.Medal:
                    Box(g, fill, pen, 6, 9, 349, 66, 33);
                    if (banner == Banner.Medal)
                    {
                        Star(g, strong, 42, 39, 35, 30, 12);
                        g.FillEllipse(white, 19, 16, 46, 46); Star(g, strong, 42, 39, 18, 8, 5);
                    }
                    else
                    {
                        Poly(g, soft, null, 41,17, 83,55, 26,57); Poly(g, strong, null, 17,18, 31,7, 52,17, 32,35);
                    }
                    break;
                case Banner.Circuit:
                case Banner.Hud:
                    Poly(g, fill, pen, 31,9, 328,9, 355,25, 331,66, 270,66, 253,77, 105,77, 88,66, 5,66);
                    g.DrawLines(pen, Points(13,62,32,21,75,21)); g.DrawLines(pen, Points(287,60,325,60,344,27));
                    for (int i = 0; i < 3; i++) Poly(g, strong, null, 289+i*12,6, 296+i*12,6, 286+i*12,18, 279+i*12,18);
                    break;
                case Banner.Racing:
                case Banner.Alert:
                    Poly(g, soft, null, 33,18, 357,18, 336,79, 11,79);
                    Poly(g, fill, pen, 28,9, 351,9, 326,71, 5,71);
                    for (int i = 0; i < 3; i++) Poly(g, strong, null, 28+i*16,8, 40+i*16,8, 34+i*16,19, 22+i*16,19);
                    if (banner == Banner.Alert && number < 61)
                    {
                        Poly(g, strong, null, 28,18, 74,18, 54,68, 10,68);
                        Poly(g, white, null, 44,27, 58,56, 29,56);
                        g.FillRectangle(strong, 42, 37, 4, 10); g.FillEllipse(strong, 42, 50, 4, 4);
                    }
                    break;
                case Banner.Laurel:
                case Banner.Gold:
                    Box(g, fill, pen, 8, 10, 344, 64, 27);
                    Box(g, null, pen, 13, 15, 334, 54, 22);
                    for (int i = 0; i < 5; i++)
                    {
                        g.FillEllipse(strong, 15 + Math.Abs(i-2)*4, 16+i*10, 12, 6);
                        g.FillEllipse(strong, 333 - Math.Abs(i-2)*4, 16+i*10, 12, 6);
                    }
                    break;
                case Banner.Speech:
                    Poly(g, fill, pen, 6,11, 353,11, 353,66, 79,66, 57,80, 58,66, 6,66);
                    Star(g, soft, 344, 13, 11, 4, 4);
                    break;
                case Banner.Wave:
                case Banner.Liquid:
                case Banner.Snow:
                    Box(g, fill, banner == Banner.Wave ? null : pen, 5, 10, 350, 65, 24);
                    using (var waves = new GraphicsPath())
                    {
                        waves.AddBezier(6,67, 88,31, 157,91, 241,65);
                        waves.AddBezier(241,65, 285,49, 318,78, 353,54);
                        waves.AddLines(Points(353,75, 6,75)); waves.CloseFigure();
                        g.FillPath(soft, waves);
                    }
                    g.DrawBezier(pen, 6,71, 88,37, 156,95, 250,69);
                    if (banner == Banner.Snow) Star(g, strong, 329, 24, 15, 5, 6);
                    break;
                case Banner.Ribbon:
                    Poly(g, strong, null, 306,22, 358,22, 344,42, 358,63, 306,63);
                    Box(g, fill, pen, 7, 10, 325, 63, 12);
                    break;
                case Banner.Cloud:
                    using (var cloud = new GraphicsPath())
                    {
                        cloud.AddBezier(7,48, 4,27, 28,12, 58,18);
                        cloud.AddBezier(58,18, 86,-3, 118,13, 139,17);
                        cloud.AddBezier(139,17, 162,5, 195,8, 221,17);
                        cloud.AddBezier(221,17, 258,4, 293,14, 310,18);
                        cloud.AddBezier(310,18, 355,9, 363,42, 351,62);
                        cloud.AddBezier(351,62, 348,77, 324,77, 300,74);
                        cloud.AddBezier(300,74, 272,80, 245,78, 222,75);
                        cloud.AddBezier(222,75, 160,79, 106,75, 53,75);
                        cloud.AddBezier(53,75, 22,76, 6,74, 7,48);
                        g.FillPath(fill, cloud); g.DrawPath(pen, cloud);
                    }
                    break;
                case Banner.Ticket:
                    Poly(g, fill, pen, 16,9, 343,9, 354,24, 354,60, 343,75, 16,75, 6,61, 6,23);
                    pen.DashStyle = DashStyle.Dash;
                    g.DrawLine(pen, 23, 15, 336, 15); g.DrawLine(pen, 23, 69, 336, 69);
                    break;
                default:
                    Box(g, fill, pen, 6, 9, 348, 66, banner == Banner.Scroll ? 8 : 25);
                    if (banner == Banner.AirMail || banner == Banner.Industrial || banner == Banner.Checkered)
                    {
                        for (int i = 0; i < 7; i++)
                            Poly(g, i % 2 == 0 ? strong : soft, null, 239+i*14,11, 247+i*14,11, 242+i*14,17, 234+i*14,17);
                        g.DrawLine(pen, 104, 70, 325, 70);
                    }
                    else if (banner == Banner.Sound)
                    {
                        for (int i = 0; i < 5; i++) g.FillRectangle(strong, 319+i*5, 69-(i+1)*4, 3, (i+1)*4);
                    }
                    else if (banner == Banner.Field) { g.DrawArc(pen, 312, 23, 32, 37, 90, 180); g.DrawLine(pen, 333, 22, 333, 62); }
                    else if (banner == Banner.Scroll) { g.DrawArc(pen, 327, 20, 20, 45, 75, 200); }
                    else if (banner == Banner.Modern) Poly(g, soft, null, 323,59, 348,59, 337,72, 312,72);
                    else { g.FillEllipse(soft, 329, 32, 13, 13); g.FillEllipse(strong, 333, 36, 5, 5); }
                    break;
            }
        }

        private static Bitmap[] LoadIllustrations()
        {
            var result = new Bitmap[60];
            using (Stream resource = typeof(HeadlineTemplate).Assembly.GetManifestResourceStream("ArcDraw.HeadlineIllustrations"))
            using (var archive = new ZipArchive(resource, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (!entry.Name.EndsWith(".png", StringComparison.Ordinal)) continue;
                    int index = int.Parse(Path.GetFileNameWithoutExtension(entry.Name)) - 61;
                    using (Stream file = entry.Open())
                    using (Image image = Image.FromStream(file)) result[index] = new Bitmap(image);
                }
            }
            return result;
        }

        private static void DrawIllustration(Graphics g, int number, int alpha)
        {
            Bitmap image = Illustrations.Value[number - 61];
            if (image == null) return;
            using (var attributes = new ImageAttributes())
            {
                attributes.SetColorMatrix(new ColorMatrix { Matrix33 = alpha / 255f });
                g.DrawImage(image, new Rectangle(5, 1, 83, 82), 0, 0, image.Width, image.Height, GraphicsUnit.Pixel, attributes);
            }
        }
    }
}
