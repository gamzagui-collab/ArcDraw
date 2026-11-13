using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
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
        private static Size cachedArtworkSize;
        private static byte[] cachedArtwork;

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
        private static byte[] GetArtwork(int number)
        {
            if (cachedArtworkNumber == number && cachedArtwork != null) return cachedArtwork;
            using (Stream resource = typeof(ScreenFrameStyle).Assembly.GetManifestResourceStream("ArcDraw.ScreenFrameArtwork"))
            using (var archive = new ZipArchive(resource, ZipArchiveMode.Read))
            using (Stream entry = archive.GetEntry(number.ToString("D3") + ".jpg").Open())
            using (var decoded = new Bitmap(entry))
            using (Bitmap artwork = decoded.Clone(new Rectangle(Point.Empty, decoded.Size), PixelFormat.Format32bppArgb))
            {
                byte[] pixels = ReadArtwork(artwork, SourceInsets(artwork.Size, number));
                cachedArtworkSize = artwork.Size;
                cachedArtwork = pixels;
                cachedArtworkNumber = number;
                return pixels;
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

        private static byte[] ReadArtwork(Bitmap artwork, Padding inset)
        {
            // Fade toward the open rectangle. The corner art stays opaque and each
            // inner side reaches zero alpha before the desktop opening starts.
            const int samplingGuard = 3;
            int feather = Math.Max(4, artwork.Height / 9) - samplingGuard;
            int right = artwork.Width - inset.Right, bottom = artwork.Height - inset.Bottom;
            Rectangle bounds = new Rectangle(Point.Empty, artwork.Size);
            int rowBytes = artwork.Width * 4;
            var pixels = new byte[rowBytes * artwork.Height];
            BitmapData data = artwork.LockBits(bounds, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < artwork.Height; y++)
                {
                    IntPtr address = IntPtr.Add(data.Scan0, y * data.Stride);
                    Marshal.Copy(address, pixels, y * rowBytes, rowBytes);
                    int dy = Math.Max(0, Math.Max(inset.Top - y, y - bottom + 1) - samplingGuard);
                    for (int x = 0; x < artwork.Width; x++)
                    {
                        int dx = Math.Max(0, Math.Max(inset.Left - x, x - right + 1) - samplingGuard);
                        int offset = y * rowBytes + x * 4;
                        if (dx >= feather || dy >= feather) continue;
                        float distance = (float)Math.Sqrt(dx * dx + dy * dy) / feather;
                        if (distance >= 1) continue;
                        int alpha = (int)(255 * distance * distance * (3 - 2 * distance) + .5f);
                        for (int c = 0; c < 3; c++) pixels[offset + c] = (byte)((pixels[offset + c] * alpha + 127) / 255);
                        pixels[offset + 3] = (byte)alpha;
                    }
                }
            }
            finally { artwork.UnlockBits(data); }
            return pixels;
        }

        internal static void ReleaseArtwork()
        {
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
            GetArtwork(number);
            Padding inset = SourceInsets(cachedArtworkSize, number);
            // Anchor width to the visible ornament, including the soft edge, and
            // leave most of the display usable even at the largest slider value.
            int longest = Math.Max(Math.Max(inset.Left, inset.Right), Math.Max(inset.Top, inset.Bottom));
            float scale = width / (float)longest;
            return new Padding(Math.Max(1, (int)Math.Round(inset.Left * scale)),
                Math.Max(1, (int)Math.Round(inset.Top * scale)),
                Math.Max(1, (int)Math.Round(inset.Right * scale)),
                Math.Max(1, (int)Math.Round(inset.Bottom * scale)));
        }

        private static void PaintArtwork(Graphics g, Size size, Padding inset, int number)
        {
            using (var preview = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppPArgb))
            {
                RenderArtworkStrip(preview, size, new Rectangle(Point.Empty, size), inset, number + CategorySize - 1);
                g.DrawImageUnscaled(preview, Point.Empty);
            }
        }

        internal static void RenderArtworkStrip(Bitmap target, Size size, Rectangle strip, Padding inset, int theme)
        {
            int number = theme - CategorySize + 1;
            byte[] source = GetArtwork(number);
            Size sourceSize = cachedArtworkSize;
            Padding original = SourceInsets(sourceSize, number);
            float[] columns = MapAxis(strip.Left, strip.Width, size.Width, sourceSize.Width, inset.Left, inset.Right, original.Left, original.Right);
            float[] rows = MapAxis(strip.Top, strip.Height, size.Height, sourceSize.Height, inset.Top, inset.Bottom, original.Top, original.Bottom);
            var x0 = new int[columns.Length];
            var x1 = new int[columns.Length];
            var xWeight = new int[columns.Length];
            for (int x = 0; x < columns.Length; x++)
            {
                int pixel = (int)columns[x];
                x0[x] = pixel * 4;
                x1[x] = Math.Min(pixel + 1, sourceSize.Width - 1) * 4;
                xWeight[x] = (int)((columns[x] - pixel) * 256);
            }
            BitmapData data = target.LockBits(new Rectangle(Point.Empty, target.Size), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
            try
            {
                var scanline = new byte[strip.Width * 4];
                for (int y = 0; y < rows.Length; y++)
                {
                    int sourceY = (int)rows[y];
                    int row0 = sourceY * sourceSize.Width * 4;
                    int row1 = Math.Min(sourceY + 1, sourceSize.Height - 1) * sourceSize.Width * 4;
                    int below = (int)((rows[y] - sourceY) * 256), above = 256 - below;
                    for (int x = 0; x < columns.Length; x++)
                    {
                        int right = xWeight[x], left = 256 - right;
                        for (int c = 0; c < 4; c++)
                        {
                            int a = source[row0 + x0[x] + c] * left + source[row0 + x1[x] + c] * right;
                            int b = source[row1 + x0[x] + c] * left + source[row1 + x1[x] + c] * right;
                            scanline[x * 4 + c] = (byte)((a * above + b * below + 32768) >> 16);
                        }
                    }
                    Marshal.Copy(scanline, 0, IntPtr.Add(data.Scan0, y * data.Stride), scanline.Length);
                }
            }
            finally { target.UnlockBits(data); }
        }

        private static float[] MapAxis(int start, int count, int targetLength, int sourceLength,
            int near, int far, int sourceNear, int sourceFar)
        {
            var coordinates = new float[count];
            float middle = (sourceLength - sourceNear - sourceFar) / (float)(targetLength - near - far);
            float nearRate = sourceNear / (float)near, farRate = sourceFar / (float)far;
            // Smooth the change of scale at the two joins. Every strip samples
            // the same continuous image, including across corner boundaries.
            float blend = Math.Min(Math.Min(near, far) * .06f, (targetLength - near - far) * .15f);
            for (int i = 0; i < count; i++)
            {
                float x = start + i + .5f;
                float position = x * nearRate + (middle - nearRate) * SmoothStepDistance(x - near, blend) +
                    (farRate - middle) * SmoothStepDistance(x - (targetLength - far), blend);
                coordinates[i] = Math.Max(0, Math.Min(sourceLength - 1, position - .5f));
            }
            return coordinates;
        }

        private static float SmoothStepDistance(float distance, float radius)
        {
            if (distance <= -radius) return 0;
            if (distance >= radius) return distance;
            float t = (distance + radius) / (2 * radius);
            return radius * t * t * t * (2 - t);
        }
    }

    internal sealed class ScreenFrameThemePicker : Form
    {
        private const int FavoritesPerPage = 6;
        private const int ThemesPerPage = ScreenFrameStyle.CategorySize;
        private const int RecentSlots = DrawingSettings.RecentScreenFrameLimit;
        private readonly Button[] tiles = new Button[FavoritesPerPage + RecentSlots];
        private readonly Button[] stars = new Button[FavoritesPerPage + RecentSlots];
        private readonly Bitmap[] previews = new Bitmap[FavoritesPerPage + RecentSlots];
        private readonly int columns, rowHeight;
        private readonly Rectangle previewBounds;
        private readonly DrawingSettings settings;
        private readonly ListBox categories;
        private readonly TextBox search;
        private readonly Button clearSearch;
        private readonly Panel grid;
        private readonly Button previous, next;
        private readonly Label page, empty, recentHeading, recentEmpty, resultCount;
        private int pageIndex;
        internal event Action<int> ThemeChosen;

        internal ScreenFrameThemePicker(DrawingSettings settings, Size workArea)
        {
            this.settings = settings;
            Name = "ScreenFrameThemePicker";
            Text = "화면 액자 테마";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            ClientSize = new Size(Math.Min(948, workArea.Width - 40), Math.Min(682, workArea.Height - 68));
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(238, 243, 249);
            int footerY = ClientSize.Height - 38;
            int gridLeft = 164, gridWidth = ClientSize.Width - gridLeft - 12;
            categories = new ListBox { Name = "ScreenFrameCategories", Bounds = new Rectangle(12, 12, 142, footerY - 20), IntegralHeight = false };
            categories.Items.Add("★ 즐겨찾기");
            categories.Items.AddRange(ScreenFrameStyle.Categories);
            var searchLabel = new Label { Text = "테마 검색", Bounds = new Rectangle(gridLeft, 14, 65, 22) };
            search = new TextBox { Name = "ScreenFrameSearch", AccessibleName = "테마 이름 또는 번호",
                Bounds = new Rectangle(gridLeft + 68, 12, gridWidth - 142, 24) };
            clearSearch = new Button { Name = "ClearScreenFrameSearch", Text = "지우기",
                Bounds = new Rectangle(ClientSize.Width - 80, 10, 68, 26) };
            clearSearch.Click += delegate { search.Clear(); search.Focus(); };
            resultCount = new Label { Name = "ScreenFrameSearchCount", Bounds = new Rectangle(gridLeft, 44, gridWidth, 18) };
            grid = new Panel { Name = "ScreenFramePreviewGrid", AutoScroll = true,
                Bounds = new Rectangle(gridLeft, 66, gridWidth, footerY - 74) };
            Controls.AddRange(new Control[] { searchLabel, search, clearSearch, categories, resultCount, grid });

            int available = gridWidth - SystemInformation.VerticalScrollBarWidth - 4;
            columns = available < 390 ? 1 : 2;
            int cardWidth = (available - (columns - 1) * 8) / columns;
            int imageWidth = cardWidth - 42;
            previewBounds = new Rectangle(8, 7, imageWidth, imageWidth * 9 / 16);
            int cardHeight = previewBounds.Bottom + 26;
            rowHeight = cardHeight + 8;
            for (int index = 0; index < tiles.Length; index++)
            {
                int slot = index;
                var tile = new Button
                {
                    Size = new Size(cardWidth, cardHeight),
                    FlatStyle = FlatStyle.Flat, BackColor = Color.White, Cursor = Cursors.Hand
                };
                tile.Paint += (_, args) => PaintPreview(slot, args.Graphics);
                tile.Click += delegate
                {
                    ThemeChosen?.Invoke((int)tile.Tag);
                    ShowCategory();
                };
                grid.Controls.Add(tile);
                tiles[index] = tile;
                var star = new Button
                {
                    Size = new Size(24, 24),
                    FlatStyle = FlatStyle.Flat, BackColor = Color.White, Cursor = Cursors.Hand
                };
                star.FlatAppearance.BorderSize = 0;
                star.Click += delegate
                {
                    settings.ToggleFavoriteScreenFrame((int)tile.Tag);
                    ShowCategory();
                };
                grid.Controls.Add(star);
                star.BringToFront();
                stars[index] = star;
            }
            empty = new Label { Size = new Size(available, 56), TextAlign = ContentAlignment.MiddleCenter };
            recentHeading = new Label { Name = "ScreenFrameRecentHeading", Text = "최근 사용", Size = new Size(available, 22) };
            recentEmpty = new Label { Text = "테마를 선택하면 여기에 표시됩니다.",
                Size = new Size(available, 40), TextAlign = ContentAlignment.MiddleCenter };
            grid.Controls.AddRange(new Control[] { empty, recentHeading, recentEmpty });
            previous = new Button { Name = "PreviousScreenFramePage", Text = "이전", Bounds = new Rectangle(gridLeft, footerY, 65, 26) };
            page = new Label { Bounds = new Rectangle(gridLeft + 67, footerY, 67, 26), TextAlign = ContentAlignment.MiddleCenter };
            next = new Button { Name = "NextScreenFramePage", Text = "다음", Bounds = new Rectangle(gridLeft + 136, footerY, 65, 26) };
            previous.Click += delegate { SetPage(pageIndex - 1); };
            next.Click += delegate { SetPage(pageIndex + 1); };
            Controls.AddRange(new Control[] { previous, page, next });
            var cancel = new Button { Text = "닫기", Bounds = new Rectangle(ClientSize.Width - 94, footerY, 82, 26) };
            cancel.Click += delegate { Close(); };
            Controls.Add(cancel);
            CancelButton = cancel;
            search.TextChanged += delegate { SetPage(0); };
            categories.SelectedIndexChanged += delegate
            {
                if (search.TextLength == 0) SetPage(0);
                else search.Clear();
            };
            categories.SelectedIndex = settings.FavoriteScreenFrames.Count > 0 || settings.RecentScreenFrames.Count > 0
                ? 0 : settings.ScreenFrameTheme / ScreenFrameStyle.CategorySize + 1;
            ActiveControl = search;
        }

        private static int ThemeNumber(int theme)
        {
            int firstArtwork = ScreenFrameStyle.CategorySize;
            return theme < firstArtwork ? ScreenFrameStyle.Names.Length - firstArtwork + theme + 1 : theme - firstArtwork + 1;
        }

        private static bool MatchesTheme(string[] words, int theme)
        {
            string name = ScreenFrameStyle.Names[theme];
            foreach (string word in words)
            {
                string number = word.TrimStart('#');
                if (number.EndsWith("번", StringComparison.Ordinal)) number = number.Substring(0, number.Length - 1);
                if (int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
                {
                    if (ThemeNumber(theme) != value) return false;
                }
                else if (name.IndexOf(word, StringComparison.OrdinalIgnoreCase) < 0) return false;
            }
            return true;
        }

        private void SetPage(int index)
        {
            pageIndex = index;
            grid.AutoScrollPosition = Point.Empty;
            ShowCategory();
        }

        private int Rows(int count) => (count + columns - 1) / columns;

        private void ShowCategory()
        {
            string query = search.Text.Trim();
            bool searching = query.Length != 0;
            bool showingFavorites = !searching && categories.SelectedIndex == 0;
            int pageSize = showingFavorites ? FavoritesPerPage : ThemesPerPage;
            IReadOnlyList<int> list;
            if (searching)
            {
                string[] words = query.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                list = Enumerable.Range(0, ScreenFrameStyle.Names.Length).Where(theme => MatchesTheme(words, theme))
                    .OrderBy(ThemeNumber).ToArray();
            }
            else if (showingFavorites) list = settings.FavoriteScreenFrames;
            else list = Enumerable.Range((categories.SelectedIndex - 1) * ThemesPerPage, ThemesPerPage).ToArray();

            int pages = Math.Max(1, (list.Count + pageSize - 1) / pageSize);
            pageIndex = Math.Max(0, Math.Min(pages - 1, pageIndex));
            int offset = pageIndex * pageSize;
            int count = Math.Min(pageSize, list.Count - offset);
            int recentCount = showingFavorites ? settings.RecentScreenFrames.Count : 0;
            int recentY = Math.Max(64, Rows(count) * rowHeight) + 26;
            Point scroll = grid.AutoScrollPosition;
            grid.SuspendLayout();
            empty.Location = scroll;
            empty.Text = searching ? "검색 결과가 없습니다." : "다른 테마 목록에서 ☆를 눌러 추가하세요.";
            empty.Visible = count == 0;
            recentHeading.Location = new Point(scroll.X, scroll.Y + recentY - 24);
            recentHeading.Visible = showingFavorites;
            recentEmpty.Location = new Point(scroll.X, scroll.Y + recentY);
            recentEmpty.Visible = showingFavorites && recentCount == 0;
            SetTiles(0, showingFavorites ? FavoritesPerPage : tiles.Length, list, offset, count, scroll, "");
            if (showingFavorites)
                SetTiles(FavoritesPerPage, RecentSlots, settings.RecentScreenFrames, 0, recentCount,
                    new Point(scroll.X, scroll.Y + recentY), "Recent");
            grid.AutoScrollMinSize = new Size(0, showingFavorites ? recentY + Math.Max(40, Rows(recentCount) * rowHeight)
                : Math.Max(56, Rows(count) * rowHeight));
            grid.ResumeLayout();
            clearSearch.Enabled = search.TextLength != 0;
            resultCount.Text = searching ? "검색 결과 " + list.Count + "개" : "";
            previous.Visible = page.Visible = next.Visible = pages > 1;
            previous.Enabled = pageIndex > 0;
            next.Enabled = pageIndex + 1 < pages;
            page.Text = (pageIndex + 1) + " / " + pages;
        }

        private void SetTiles(int first, int slots, IReadOnlyList<int> themes, int offset, int count, Point origin, string prefix)
        {
            for (int slot = 0; slot < slots; slot++)
            {
                int index = first + slot;
                Button tile = tiles[index];
                Button star = stars[index];
                if (slot >= count)
                {
                    tile.Visible = star.Visible = false;
                    ClearPreview(index);
                    tile.Tag = null;
                    continue;
                }
                int theme = themes[offset + slot];
                if (!Equals(tile.Tag, theme)) ClearPreview(index);
                tile.Location = new Point(origin.X + slot % columns * (tile.Width + 8), origin.Y + slot / columns * rowHeight);
                star.Location = new Point(tile.Right - 27, tile.Top + 4);
                tile.Tag = theme;
                tile.Name = prefix + "ScreenFrameTheme" + theme;
                string name = ScreenFrameStyle.Names[theme];
                tile.AccessibleName = theme < ScreenFrameStyle.CategorySize ? ThemeNumber(theme) + "  " + name : name;
                bool selected = theme == settings.ScreenFrameTheme;
                tile.FlatAppearance.BorderColor = selected ? Color.RoyalBlue : Color.LightSlateGray;
                tile.FlatAppearance.BorderSize = selected ? 2 : 1;
                bool favorite = settings.FavoriteScreenFrames.Contains(theme);
                star.Name = prefix + "FavoriteScreenFrame" + theme;
                star.AccessibleName = tile.AccessibleName + (favorite ? " 즐겨찾기 해제" : " 즐겨찾기 추가");
                star.Text = favorite ? "★" : "☆";
                star.ForeColor = favorite ? Color.FromArgb(151, 91, 0) : Color.FromArgb(68, 86, 108);
                tile.Visible = star.Visible = true;
                tile.Invalidate();
            }
        }

        internal void RefreshPreviews()
        {
            for (int i = 0; i < previews.Length; i++)
            {
                ClearPreview(i);
                tiles[i].Invalidate();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                for (int i = 0; i < previews.Length; i++) ClearPreview(i);
            base.Dispose(disposing);
        }

        private void ClearPreview(int index)
        {
            previews[index]?.Dispose();
            previews[index] = null;
        }

        private void PaintPreview(int index, Graphics g)
        {
            Button tile = tiles[index];
            if (!(tile.Tag is int theme)) return;
            if (previews[index] == null)
            {
                var image = new Bitmap(previewBounds.Width, previewBounds.Height, PixelFormat.Format32bppPArgb);
                try
                {
                    using (Graphics canvas = Graphics.FromImage(image))
                    using (var checker = new SolidBrush(Color.FromArgb(34, 46, 64)))
                    {
                        canvas.Clear(Color.FromArgb(19, 29, 43));
                        for (int y = 0; y < image.Height; y += 8)
                        for (int x = y % 16; x < image.Width; x += 16)
                            canvas.FillRectangle(checker, x, y, Math.Min(8, image.Width - x), Math.Min(8, image.Height - y));
                        // Keep decorations legible even when the actual desktop border is very thin.
                        int width = Math.Max(1, (int)Math.Round(image.Height * Math.Max(8, settings.ScreenFrameWidth) / 100.0));
                        ScreenFrameStyle.Paint(canvas, image.Size, width, theme, ScreenFrameStyle.GetInsets(image.Size, width, theme));
                    }
                    previews[index] = image;
                }
                catch
                {
                    image.Dispose();
                    throw;
                }
            }
            g.DrawImageUnscaled(previews[index], previewBounds.Location);
            TextRenderer.DrawText(g, tile.AccessibleName, SystemFonts.MessageBoxFont,
                new Rectangle(8, previewBounds.Bottom + 4, tile.Width - 16, 18), Color.FromArgb(28, 46, 65),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }
}
