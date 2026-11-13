using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows.Forms;

namespace gInk
{
    internal static partial class ScreenFrameStyle
    {
        internal static readonly string[] Categories =
        {
            "기본", "게임", "푸드·요리", "바다·여행", "캠핑·아웃도어", "스포츠·피트니스",
            "밀리터리·전략", "댄스·퍼포먼스", "음악·노래", "리뷰·교육", "힐링·일상"
        };

        internal const int CategorySize = 10;
        private static readonly string[] ArtworkNames =
        {
            "고어·호러 게임", "좀비 서바이벌", "미스터리 공포", "FPS 전투 게임", "레트로 게임",
            "판타지 RPG", "오픈월드 모험", "추리·탈출게임", "배틀로얄", "스피드런",
            "고기굽기 방송", "바비큐·훈연", "먹방", "홈쿡·레시피", "카페·디저트",
            "술먹방·주류 리뷰", "칵테일·와인", "길거리음식", "베이킹", "라면·야식",
            "해루질", "조개잡이", "갯벌 체험", "낚시", "바다 브이로그",
            "서핑·해양스포츠", "섬 여행", "수중 탐험", "항구 감성", "일출·바다 힐링",
            "캠핑", "차박", "불멍", "백패킹", "산행·등산",
            "아웃도어 쿠킹", "캠프 장비 리뷰", "겨울 캠핑", "우중 캠핑", "숲속 감성",
            "헬스", "홈트", "크로스핏", "복싱", "러닝",
            "자전거", "요가·필라테스", "축구·풋살", "농구", "격투기 분석",
            "밀리터리", "전술 장비 리뷰", "서바이벌", "에어소프트", "군장·백팩",
            "전략 브리핑", "역사 전쟁 해설", "기갑·탱크", "항공·전투기", "해군·특수부대",
            "K-pop 댄스", "스트릿 댄스", "안무 커버", "배틀 댄스", "재즈·현대무용",
            "발레", "치어댄스", "DJ·클럽", "파티·축제", "퍼포먼스 아트",
            "노래 커버", "버스킹", "밴드 공연", "피아노·악기", "기타 연주",
            "노래방", "ASMR 보컬", "라디오·토크", "작곡·프로듀싱", "감성 음악",
            "테크 리뷰", "언박싱", "자동차 리뷰", "영화·드라마 리뷰", "게임 리뷰",
            "코스메틱·뷰티", "장난감·피규어", "스튜디오 토크", "뉴스·시사 해설", "교육·강의",
            "힐링", "ASMR", "공부방송", "브이로그", "자연명상",
            "반려동물", "인테리어·정리", "독서·필사", "수면·백색소음", "슬로우 라이프"
        };
        internal static readonly string[] Names = GetNames();
        private static int cachedArtworkNumber;
        private static Bitmap cachedArtwork;

        // Keep only the most recently drawn source so browsing doesn't retain all 100 bitmaps.
        private static Bitmap GetArtwork(int number)
        {
            if (cachedArtworkNumber == number && cachedArtwork != null) return cachedArtwork;
            using (Stream resource = typeof(ScreenFrameStyle).Assembly.GetManifestResourceStream("ArcDraw.ScreenFrameArtwork"))
            using (var archive = new ZipArchive(resource, ZipArchiveMode.Read))
            using (Stream entry = archive.GetEntry(number.ToString("D3") + ".jpg").Open())
            using (var decoded = new Bitmap(entry))
            {
                Bitmap copy = new Bitmap(decoded);
                cachedArtwork?.Dispose();
                cachedArtwork = copy;
                cachedArtworkNumber = number;
                return copy;
            }
        }

        internal static void ReleaseArtwork()
        {
            cachedArtwork?.Dispose();
            cachedArtwork = null;
        }

        private static string[] GetNames()
        {
            // These are the existing theme indices, also stored in settings.
            string[] originals =
            {
                "엔틱 클래식", "모던 미니멀", "사이버펑크", "달빛 무협", "파스텔 구름",
                "심플 라인", "금빛 물결", "벚꽃", "초록 잎", "판타지"
            };
            return originals.Concat(ArtworkNames.Select((name, index) => (index + 1).ToString("D2") + "  " + name)).ToArray();
        }

        private static void PaintArtwork(Graphics g, Size size, int width, int number)
        {
            Bitmap source = GetArtwork(number);
            int sourceBand = Math.Min(source.Height / 3, Math.Max(18, source.Height / 5));
            int sourceCorner = Math.Min(source.Width / 3, sourceBand * 3);
            int corner = Math.Min(size.Width / 3, width * 3);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            int[] srcX = { 0, sourceCorner, source.Width - sourceCorner, source.Width };
            int[] srcY = { 0, sourceBand, source.Height - sourceBand, source.Height };
            int[] dstX = { 0, corner, size.Width - corner, size.Width };
            int[] dstY = { 0, width, size.Height - width, size.Height };
            // The four sides and corners scale independently; the center remains clear.
            for (int row = 0; row < 3; row++)
            for (int col = 0; col < 3; col++)
            {
                if (row == 1 && col == 1) continue;
                if (row == 1)
                {
                    int sx = col == 0 ? 0 : source.Width - sourceBand;
                    int dx = col == 0 ? 0 : size.Width - width;
                    g.DrawImage(source, new Rectangle(dx, width, width, size.Height - width * 2),
                        sx, sourceBand, sourceBand, source.Height - sourceBand * 2, GraphicsUnit.Pixel);
                }
                else
                {
                    Rectangle destination = Rectangle.FromLTRB(dstX[col], dstY[row], dstX[col + 1], dstY[row + 1]);
                    g.DrawImage(source, destination, srcX[col], srcY[row], srcX[col + 1] - srcX[col],
                        srcY[row + 1] - srcY[row], GraphicsUnit.Pixel);
                }
            }
        }
    }

    internal sealed class ScreenFrameThemePicker : Form
    {
        private readonly Button[] tiles = new Button[ScreenFrameStyle.CategorySize];
        private readonly int frameWidth;
        private readonly ListBox categories;
        private readonly int selectedTheme;
        internal event Action<int> ThemeChosen;

        internal ScreenFrameThemePicker(int selectedTheme, int frameWidth)
        {
            this.selectedTheme = selectedTheme;
            this.frameWidth = frameWidth;
            Name = "ScreenFrameThemePicker";
            Text = "화면 액자 테마";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(752, 451);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(238, 243, 249);
            categories = new ListBox { Name = "ScreenFrameCategories", Bounds = new Rectangle(12, 12, 142, 390), IntegralHeight = false };
            categories.Items.AddRange(ScreenFrameStyle.Categories);
            categories.SelectedIndexChanged += delegate { ShowCategory(); };
            Controls.Add(categories);
            for (int index = 0; index < tiles.Length; index++)
            {
                var tile = new Button
                {
                    Bounds = new Rectangle(164 + index % 3 * 193, 12 + index / 3 * 99, 187, 93),
                    FlatStyle = FlatStyle.Flat, BackColor = Color.White, Cursor = Cursors.Hand
                };
                tile.Paint += (_, args) => PaintPreview(tile, args.Graphics);
                tile.Click += delegate { ThemeChosen?.Invoke((int)tile.Tag); Close(); };
                Controls.Add(tile);
                tiles[index] = tile;
            }
            var cancel = new Button { Text = "닫기", Bounds = new Rectangle(654, 415, 82, 26) };
            cancel.Click += delegate { Close(); };
            Controls.Add(cancel);
            CancelButton = cancel;
            categories.SelectedIndex = selectedTheme / ScreenFrameStyle.CategorySize;
        }

        private void ShowCategory()
        {
            if (categories.SelectedIndex < 0) return;
            for (int i = 0; i < tiles.Length; i++)
            {
                int theme = categories.SelectedIndex * ScreenFrameStyle.CategorySize + i;
                Button tile = tiles[i];
                tile.Tag = theme;
                tile.Name = "ScreenFrameTheme" + theme;
                tile.AccessibleName = ScreenFrameStyle.Names[theme];
                tile.FlatAppearance.BorderColor = theme == selectedTheme ? Color.RoyalBlue : Color.LightSlateGray;
                tile.FlatAppearance.BorderSize = theme == selectedTheme ? 2 : 1;
                tile.Invalidate();
            }
        }

        private void PaintPreview(Button tile, Graphics g)
        {
            if (!(tile.Tag is int theme)) return;
            var preview = new Rectangle(27, 5, 133, 74);
            using (var background = new SolidBrush(Color.FromArgb(19, 29, 43)))
            using (var checker = new SolidBrush(Color.FromArgb(34, 46, 64)))
            {
                g.FillRectangle(background, preview);
                for (int y = 0; y < preview.Height; y += 8)
                for (int x = y % 16; x < preview.Width; x += 16)
                    g.FillRectangle(checker, preview.X + x, preview.Y + y,
                        Math.Min(8, preview.Width - x), Math.Min(8, preview.Height - y));
            }
            GraphicsState saved = g.Save();
            g.TranslateTransform(preview.X, preview.Y);
            g.SetClip(new Rectangle(Point.Empty, preview.Size));
            ScreenFrameStyle.Paint(g, preview.Size, Math.Max(3, preview.Height * frameWidth / 100), theme);
            g.Restore(saved);
            TextRenderer.DrawText(g, tile.AccessibleName, SystemFonts.MessageBoxFont,
                new Rectangle(4, 77, tile.Width - 8, 14), Color.FromArgb(28, 46, 65),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }
}
