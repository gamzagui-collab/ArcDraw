using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
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

        // Insets up to the solid artwork, as percentages of source width and height.
        // The opening starts farther in so the soft edge has its own space.
        private static readonly byte[] ArtworkOpenings =
        {
            18,16,23,22, 20,13,23,26, 23,16,24,24, 16,11,15,20, 12,11,13,19,
            23,15,22,22, 24,18,22,21, 16,14,23,17, 18,15,24,23, 10,12,22,21,
            23,13,27,26, 21,24,24,22, 17,12,32,23, 22,22,23,22, 24,14,31,21,
            15,15,20,28, 21,15,23,23, 18,19,26,22, 23,15,25,20, 21,19,35,24,
            25,20,30,27, 24,17,31,26, 24,19,24,28, 24,14,22,29, 19,15,22,27,
            22,20,24,28, 25,18,21,29, 24,22,26,26, 23,20,22,33, 23,20,21,37,
            29,17,18,22, 31,13,21,32, 20,16,21,28, 24,13,22,23, 19,13,22,24,
            30,15,32,23, 23,14,26,21, 21,18,32,27, 24,22,23,25, 23,15,22,20,
            27,12,17,24, 24,16,23,22, 19,16,24,19, 19,14,14,26, 18,12,20,28,
            21,13,24,22, 21,17,24,25, 15,16,21,21, 18,16,17,22, 19,14,23,21,
            24,17,25,23, 27,14,24,20, 23,17,25,21, 28,14,22,21, 18,14,24,18,
            22,15,24,21, 23,16,20,25, 15,14,28,24, 26,13,20,26, 20,17,25,24,
            18,18,19,22, 16,14,23,22, 17,15,15,20, 18,15,18,23, 15,17,17,22,
            19,17,19,19, 21,22,24,24, 20,19,20,22, 22,20,23,23, 20,18,22,21,
            19,19,17,22, 24,18,21,32, 22,19,16,22, 21,14,30,20, 25,17,20,19,
            20,20,24,27, 20,15,23,17, 22,14,27,23, 27,14,24,23, 23,19,20,27,
            24,13,22,23, 24,13,27,22, 19,13,23,20, 24,14,24,19, 23,13,22,20,
            22,13,24,20, 23,13,23,24, 22,14,20,23, 22,15,18,23, 23,17,26,20,
            22,18,17,19, 20,16,24,20, 23,16,18,22, 20,17,21,25, 21,19,20,24,
            24,14,23,28, 25,18,25,24, 24,15,25,23, 19,18,25,26, 25,18,22,34
        };

        // Keep only the most recently drawn source so browsing doesn't retain all 100 bitmaps.
        private static Bitmap GetArtwork(int number)
        {
            if (cachedArtworkNumber == number && cachedArtwork != null) return cachedArtwork;
            using (Stream resource = typeof(ScreenFrameStyle).Assembly.GetManifestResourceStream("ArcDraw.ScreenFrameArtwork"))
            using (var archive = new ZipArchive(resource, ZipArchiveMode.Read))
            using (Stream entry = archive.GetEntry(number.ToString("D3") + ".jpg").Open())
            using (var decoded = new Bitmap(entry))
            {
                Bitmap copy = decoded.Clone(new Rectangle(Point.Empty, decoded.Size), PixelFormat.Format32bppArgb);
                try { FeatherOpening(copy, SourceInsets(copy.Size, number)); }
                catch { copy.Dispose(); throw; }
                cachedArtwork?.Dispose();
                cachedArtwork = copy;
                cachedArtworkNumber = number;
                return copy;
            }
        }

        private static Padding SourceInsets(Size size, int number)
        {
            int index = (number - 1) * 4;
            int transition = Math.Max(4, size.Height / 9);
            return new Padding(size.Width * ArtworkOpenings[index] / 100 + transition,
                size.Height * ArtworkOpenings[index + 1] / 100 + transition,
                size.Width * ArtworkOpenings[index + 2] / 100 + transition,
                size.Height * ArtworkOpenings[index + 3] / 100 + transition);
        }

        private static void FeatherOpening(Bitmap artwork, Padding inset)
        {
            // Fade toward the open rectangle. The corner art stays opaque and each
            // inner side reaches zero alpha before the desktop opening starts.
            const int samplingGuard = 3;
            int feather = Math.Max(4, artwork.Height / 9) - samplingGuard;
            int right = artwork.Width - inset.Right, bottom = artwork.Height - inset.Bottom;
            Rectangle bounds = new Rectangle(Point.Empty, artwork.Size);
            BitmapData data = artwork.LockBits(bounds, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[artwork.Width * 4];
                for (int y = 0; y < artwork.Height; y++)
                {
                    IntPtr address = IntPtr.Add(data.Scan0, y * data.Stride);
                    Marshal.Copy(address, row, 0, row.Length);
                    int dy = Math.Max(0, Math.Max(inset.Top - y, y - bottom + 1) - samplingGuard);
                    for (int x = 0; x < artwork.Width; x++)
                    {
                        int dx = Math.Max(0, Math.Max(inset.Left - x, x - right + 1) - samplingGuard);
                        if (dx >= feather || dy >= feather) continue;
                        float distance = (float)Math.Sqrt(dx * dx + dy * dy) / feather;
                        if (distance < 1)
                            row[x * 4 + 3] = (byte)Math.Round(row[x * 4 + 3] * distance * distance * (3 - 2 * distance));
                    }
                    Marshal.Copy(row, 0, address, row.Length);
                }
            }
            finally { artwork.UnlockBits(data); }
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

        internal static Padding GetInsets(Size size, int width, int theme)
        {
            if (theme < CategorySize) return new Padding(width);
            int number = theme - CategorySize + 1;
            Bitmap source = GetArtwork(number);
            Padding inset = SourceInsets(source.Size, number);
            float scale = Math.Min(width / (source.Height * .21f),
                Math.Min(size.Width * .58f / inset.Horizontal, size.Height * .58f / inset.Vertical));
            return new Padding(Math.Max(1, (int)Math.Round(inset.Left * scale)),
                Math.Max(1, (int)Math.Round(inset.Top * scale)),
                Math.Max(1, (int)Math.Round(inset.Right * scale)),
                Math.Max(1, (int)Math.Round(inset.Bottom * scale)));
        }

        private static void PaintArtwork(Graphics g, Size size, Padding inset, int number)
        {
            Bitmap source = GetArtwork(number);
            Padding original = SourceInsets(source.Size, number);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            int[] srcX = { 0, original.Left, source.Width - original.Right, source.Width };
            int[] srcY = { 0, original.Top, source.Height - original.Bottom, source.Height };
            int[] dstX = { 0, inset.Left, size.Width - inset.Right, size.Width };
            int[] dstY = { 0, inset.Top, size.Height - inset.Bottom, size.Height };
            using (var sampling = new ImageAttributes())
            {
                sampling.SetWrapMode(WrapMode.TileFlipXY);
                for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                {
                    if (row == 1 && col == 1) continue;
                    Rectangle destination = Rectangle.FromLTRB(dstX[col], dstY[row], dstX[col + 1], dstY[row + 1]);
                    g.DrawImage(source, destination, srcX[col], srcY[row], srcX[col + 1] - srcX[col],
                        srcY[row + 1] - srcY[row], GraphicsUnit.Pixel, sampling);
                }
            }
        }
    }

    internal sealed class ScreenFrameThemePicker : Form
    {
        private readonly Button[] tiles = new Button[ScreenFrameStyle.CategorySize];
        private readonly Button[] stars = new Button[ScreenFrameStyle.CategorySize];
        private readonly DrawingSettings settings;
        private readonly ListBox categories;
        private readonly Button previous, next;
        private readonly Label page, empty;
        private int favoritePage;
        internal event Action<int> ThemeChosen;

        internal ScreenFrameThemePicker(DrawingSettings settings)
        {
            this.settings = settings;
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
            categories.Items.Add("★ 즐겨찾기");
            categories.Items.AddRange(ScreenFrameStyle.Categories);
            categories.SelectedIndexChanged += delegate { favoritePage = 0; ShowCategory(); };
            Controls.Add(categories);
            for (int index = 0; index < tiles.Length; index++)
            {
                var tile = new Button
                {
                    Bounds = new Rectangle(164 + index % 3 * 193, 12 + index / 3 * 99, 187, 93),
                    FlatStyle = FlatStyle.Flat, BackColor = Color.White, Cursor = Cursors.Hand
                };
                tile.Paint += (_, args) => PaintPreview(tile, args.Graphics);
                tile.Click += delegate
                {
                    ThemeChosen?.Invoke((int)tile.Tag);
                    ShowCategory();
                };
                Controls.Add(tile);
                tiles[index] = tile;
                var star = new Button
                {
                    Bounds = new Rectangle(tile.Right - 27, tile.Top + 4, 24, 24),
                    FlatStyle = FlatStyle.Flat, BackColor = Color.White, Cursor = Cursors.Hand
                };
                star.FlatAppearance.BorderSize = 0;
                star.Click += delegate
                {
                    settings.ToggleFavoriteScreenFrame((int)tile.Tag);
                    ShowCategory();
                };
                Controls.Add(star);
                star.BringToFront();
                stars[index] = star;
            }
            empty = new Label { Text = "다른 테마 목록에서 ☆를 눌러 추가하세요.",
                Bounds = new Rectangle(164, 12, 573, 93), TextAlign = ContentAlignment.MiddleCenter };
            previous = new Button { Name = "PreviousScreenFrameFavorites", Text = "이전", Bounds = new Rectangle(330, 415, 65, 26) };
            next = new Button { Name = "NextScreenFrameFavorites", Text = "다음", Bounds = new Rectangle(466, 415, 65, 26) };
            page = new Label { Bounds = new Rectangle(397, 415, 67, 26), TextAlign = ContentAlignment.MiddleCenter };
            previous.Click += delegate { favoritePage--; ShowCategory(); };
            next.Click += delegate { favoritePage++; ShowCategory(); };
            Controls.AddRange(new Control[] { empty, previous, page, next });
            var cancel = new Button { Text = "닫기", Bounds = new Rectangle(654, 415, 82, 26) };
            cancel.Click += delegate { Close(); };
            Controls.Add(cancel);
            CancelButton = cancel;
            categories.SelectedIndex = settings.FavoriteScreenFrames.Count > 0 ? 0 : settings.ScreenFrameTheme / tiles.Length + 1;
        }

        private void ShowCategory()
        {
            if (categories.SelectedIndex < 0) return;
            bool showingFavorites = categories.SelectedIndex == 0;
            var favorites = settings.FavoriteScreenFrames;
            int pages = Math.Max(1, (favorites.Count + tiles.Length - 1) / tiles.Length);
            favoritePage = Math.Max(0, Math.Min(pages - 1, favoritePage));
            empty.Visible = showingFavorites && favorites.Count == 0;
            previous.Visible = page.Visible = next.Visible = showingFavorites && pages > 1;
            previous.Enabled = favoritePage > 0;
            next.Enabled = favoritePage + 1 < pages;
            page.Text = (favoritePage + 1) + " / " + pages;
            int offset = (showingFavorites ? favoritePage : categories.SelectedIndex - 1) * tiles.Length;
            for (int i = 0; i < tiles.Length; i++)
            {
                Button tile = tiles[i];
                Button star = stars[i];
                tile.Visible = star.Visible = !showingFavorites || offset + i < favorites.Count;
                if (showingFavorites && offset + i >= favorites.Count) continue;
                int theme = showingFavorites ? favorites[offset + i] : offset + i;
                tile.Tag = theme;
                tile.Name = "ScreenFrameTheme" + theme;
                tile.AccessibleName = ScreenFrameStyle.Names[theme];
                bool selected = theme == settings.ScreenFrameTheme;
                tile.FlatAppearance.BorderColor = selected ? Color.RoyalBlue : Color.LightSlateGray;
                tile.FlatAppearance.BorderSize = selected ? 2 : 1;
                bool favorite = favorites.Contains(theme);
                star.Name = "FavoriteScreenFrame" + theme;
                star.AccessibleName = tile.AccessibleName + (favorite ? " 즐겨찾기 해제" : " 즐겨찾기 추가");
                star.Text = favorite ? "★" : "☆";
                star.ForeColor = favorite ? Color.FromArgb(151, 91, 0) : Color.FromArgb(68, 86, 108);
                tile.Invalidate();
            }
        }

        internal void RefreshPreviews()
        {
            foreach (Button tile in tiles) tile.Invalidate();
        }

        private void PaintPreview(Button tile, Graphics g)
        {
            if (!(tile.Tag is int theme)) return;
            var preview = new Rectangle(19, 5, 133, 74);
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
            int width = Math.Max(1, (int)Math.Round(preview.Height * settings.ScreenFrameWidth / 100.0));
            ScreenFrameStyle.Paint(g, preview.Size, width, theme, ScreenFrameStyle.GetInsets(preview.Size, width, theme));
            g.Restore(saved);
            TextRenderer.DrawText(g, tile.AccessibleName, SystemFonts.MessageBoxFont,
                new Rectangle(4, 77, tile.Width - 8, 14), Color.FromArgb(28, 46, 65),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }
}
