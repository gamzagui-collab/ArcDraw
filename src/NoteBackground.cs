using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Ink;

namespace gInk
{
    public sealed partial class WhiteboardLayerForm
    {
        private static readonly Color PaperKey = Color.FromArgb(254, 253, 252);
        private int backgroundPercent = 100;
        private NoteBackgroundShape backgroundShape;
        private Color? headlineFrameColor, headlineFillColor;
        private NoteBackgroundSlider backgroundSlider;
        private Button backgroundShapeButton, headlineColorButton, headlineFillButton, headlinePaletteAnchor;
        private HeadlineTemplatePicker backgroundMenu;
        private ToolbarColorPalette headlineColorPalette;
        private NoteVisualWindow backgroundVisual;
        private Timer backgroundTimer;
        private Timer headlineHoverTimer;
        private bool headlineChromeVisible;
        private Bitmap noteSnapshot, notePixels, inkPixels;
        private bool renderingNote, noteVisualDirty;
        private Stroke previewInkStroke;
        private bool useBackgroundVisual => IsHeadline || backgroundPercent != 100;
        internal bool ShowDrawingFrame => !IsHeadline || headlineChromeVisible;
        private Rectangle HeadlineCaptionBounds => HeadlineTemplate.Get(backgroundShape).CaptionBounds(paper.ClientRectangle);

        private static NoteBackgroundShape DefaultBackgroundShape(bool headline) =>
            headline ? NoteBackgroundShape.Rounded : NoteBackgroundShape.Rectangle;

        private void InitializeNoteBackground()
        {
            backgroundShape = DefaultBackgroundShape(IsHeadline);
            backgroundSlider = new NoteBackgroundSlider();
            backgroundSlider.Changed += value => ChangeNoteBackground(value, backgroundShape);
            titleBar.Controls.Add(backgroundSlider);
            if (IsHeadline)
            {
                backgroundMenu = new HeadlineTemplatePicker();
                backgroundMenu.Selected += shape => ChangeNoteBackground(backgroundPercent, shape);
                backgroundMenu.Closed += delegate { RefreshHeadlineChrome(); };
                backgroundSlider.Width = 62;
                backgroundShapeButton = new Button
                {
                    Name = "HeadlineShape", Text = "모양", AccessibleName = "헤드라인 디자인 선택",
                    Size = new Size(38, 26), Top = 4, TabStop = false,
                    FlatStyle = FlatStyle.Flat, BackColor = titleBar.BackColor, ForeColor = title.ForeColor
                };
                backgroundShapeButton.FlatAppearance.BorderSize = 0;
                backgroundShapeButton.Click += delegate
                {
                    headlineColorPalette?.Hide();
                    backgroundMenu.ShowFor(backgroundShapeButton, backgroundShape, headlineFrameColor, headlineFillColor);
                };
                titleBar.Controls.Add(backgroundShapeButton);
                headlineColorButton = AddHeadlineColorButton("HeadlineFrameColor", "테두리", 56, false);
                headlineFillButton = AddHeadlineColorButton("HeadlineFillColor", "배경", 46, true);
                headlineHoverTimer = new Timer { Interval = 45 };
                headlineHoverTimer.Tick += delegate { RefreshHeadlineChrome(); };
            }
            PlaceNoteTitle(titleBar.ClientSize.Width - 71);
            backgroundTimer = new Timer { Interval = 25 };
            backgroundTimer.Tick += delegate
            {
                backgroundTimer.Stop();
                if (Visible && useBackgroundVisual && noteVisualDirty) RenderNoteVisual();
            };
            LocationChanged += delegate
            {
                if (backgroundVisual != null) backgroundVisual.Location = Location;
                if (headlineColorPalette?.Visible == true) headlineColorPalette.PlaceAt(headlinePaletteAnchor);
            };
            SizeChanged += delegate { QueueNoteVisual(); };
            VisibleChanged += delegate { UpdateNoteBackground(); };
            titleBar.Invalidated += delegate { QueueNoteVisual(); };
            paper.Invalidated += delegate { QueueNoteVisual(); };
            layerInk.CursorDown += (_, args) =>
            {
                if (!useBackgroundVisual || textMode || lastEraser) return;
                previewInkStroke = args.Stroke;
                QueueNoteVisual();
            };
            layerInk.NewPackets += delegate { if (previewInkStroke != null) QueueNoteVisual(); };
            layerInk.Stroke += delegate { previewInkStroke = null; QueueNoteVisual(); };
            paper.MouseUp += delegate { previewInkStroke = null; QueueNoteVisual(); };
            RestoreNoteBackground(backgroundPercent, backgroundShape);
        }

        private void RefreshHeadlineChrome()
        {
            if (!IsHeadline || closing || IsDisposed) return;
            bool hover = false;
            if (Visible)
            {
                Point pointer = System.Windows.Forms.Cursor.Position;
                if (headlineColorPalette?.Visible == true &&
                    (Control.MouseButtons & MouseButtons.Left) != 0 &&
                    !headlineColorPalette.Bounds.Contains(pointer) &&
                    !headlinePaletteAnchor.RectangleToScreen(headlinePaletteAnchor.ClientRectangle).Contains(pointer))
                    headlineColorPalette.Hide();
                bool interacting = backgroundSlider.Capture || grips.Values.Any(grip => grip.Capture) ||
                    (dragging && (titleBar.Capture || title.Capture));
                hover = backgroundMenu.Visible ||
                    (headlineColorPalette?.Visible == true && headlineColorPalette.Bounds.Contains(pointer)) ||
                    interacting || Bounds.Contains(pointer) && PointerHitsHeadline(pointer);
            }
            if (headlineChromeVisible == hover) return;
            headlineChromeVisible = hover;
            QueueNoteVisual();
            manager.CurrentRoot.DrawingSession?.RefreshDrawingTarget();
        }

        private bool PointerHitsHeadline(Point point)
        {
            IntPtr hit = WindowFromPoint(point);
            return hit == Handle || IsChild(Handle, hit);
        }

        private void PlaceNoteTitle(int renameLeft)
        {
            int right = renameLeft - 4;
            if (backgroundShapeButton != null)
            {
                backgroundShapeButton.Left = right - backgroundShapeButton.Width;
                right = backgroundShapeButton.Left - 4;
            }
            if (headlineFillButton != null)
            {
                headlineFillButton.Left = right - headlineFillButton.Width;
                right = headlineFillButton.Left - 4;
            }
            if (headlineColorButton != null)
            {
                headlineColorButton.Left = right - headlineColorButton.Width;
                right = headlineColorButton.Left - 4;
            }
            if (backgroundSlider != null)
            {
                backgroundSlider.Left = right - backgroundSlider.Width;
                right = backgroundSlider.Left - 4;
            }
            title.Width = Math.Max(16, right - title.Left);
        }

        private void ChangeNoteBackground(int percent, NoteBackgroundShape shape)
        {
            if (backgroundPercent == percent && backgroundShape == shape) return;
            RestoreNoteBackground(percent, shape);
            manager.NotesChanged();
        }

        private void RestoreNoteBackground(int percent, NoteBackgroundShape shape)
        {
            backgroundPercent = percent;
            backgroundShape = shape;
            backgroundSlider.Value = percent;
            headlineColorButton?.Invalidate();
            headlineFillButton?.Invalidate();
            ResizeMemoEditor();
            UpdateNoteBackground();
        }

        private Button AddHeadlineColorButton(string name, string label, int width, bool fill)
        {
            var button = new ToolbarColorPalette.PaletteButton
            {
                Name = name, AccessibleName = "헤드라인 " + label + " 색상", Text = label,
                TextAlign = ContentAlignment.MiddleRight, Size = new Size(width, 26), Top = 4,
                BackColor = titleBar.BackColor, ForeColor = title.ForeColor
            };
            button.FlatAppearance.BorderSize = 0;
            button.Paint += (_, e) =>
            {
                using (var brush = new SolidBrush(EffectiveHeadlineColor(fill)))
                    e.Graphics.FillEllipse(brush, 3, 8, 10, 10);
                using (var outline = new Pen(Color.FromArgb(100, 100, 116)))
                    e.Graphics.DrawEllipse(outline, 3, 8, 10, 10);
            };
            button.Click += delegate { ToggleHeadlineColorPalette(button, fill); };
            titleBar.Controls.Add(button);
            return button;
        }

        private Color EffectiveHeadlineColor(bool fill)
        {
            HeadlineTemplate template = HeadlineTemplate.Get(backgroundShape);
            return fill ? headlineFillColor ?? template.DefaultFill : headlineFrameColor ?? template.Accent;
        }

        private void ToggleHeadlineColorPalette(Button anchor, bool fill)
        {
            if (headlineColorPalette?.Visible == true)
            {
                headlineColorPalette.Hide();
                if (headlinePaletteAnchor == anchor) return;
            }
            headlinePaletteAnchor = anchor;
            if (headlineColorPalette == null)
            {
                headlineColorPalette = new ToolbarColorPalette(color => SetHeadlineColor(color),
                    () => SetHeadlineColor(null)) { Name = "HeadlineFramePalette", Owner = this };
                headlineColorPalette.VisibleChanged += delegate { RefreshHeadlineChrome(); };
                headlineColorPalette.SizeChanged += delegate
                {
                    if (headlineColorPalette.Visible) headlineColorPalette.PlaceAt(headlinePaletteAnchor);
                };
            }
            headlineColorPalette.SetCurrentColor(EffectiveHeadlineColor(fill));
            headlineColorPalette.PlaceAt(anchor);
            headlineColorPalette.Show();
            headlineColorPalette.RaiseWithoutActivation();
        }

        private void SetHeadlineColor(Color? color)
        {
            headlineColorPalette?.Hide();
            if (headlinePaletteAnchor == headlineFillButton)
            {
                if (headlineFillColor == color) return;
                headlineFillColor = color;
            }
            else
            {
                if (headlineFrameColor == color) return;
                headlineFrameColor = color;
            }
            headlinePaletteAnchor.Invalidate();
            QueueNoteVisual();
            manager.NotesChanged();
        }

        private void UpdateNoteBackground()
        {
            if (backgroundTimer == null || closing || IsDisposed) return;
            bool composite = useBackgroundVisual;
            Color paperColor = composite ? PaperKey : Color.White;
            paper.BackColor = paperColor;
            if (IsMemoEditor) textEditor.BackColor = paperColor;
            if (!composite || !Visible)
            {
                if (headlineHoverTimer != null)
                {
                    headlineHoverTimer.Stop();
                    if (backgroundMenu.Visible) backgroundMenu.Close();
                    headlineColorPalette?.Hide();
                    headlineChromeVisible = false;
                }
                backgroundTimer.Stop();
                backgroundVisual?.Hide();
                if (!composite) Opacity = 1;
                if (!renderingNote) DisposeNoteBitmaps();
                return;
            }
            if (headlineHoverTimer != null && !headlineHoverTimer.Enabled)
            {
                RefreshHeadlineChrome();
                headlineHoverTimer.Start();
            }
            if (backgroundVisual == null) backgroundVisual = new NoteVisualWindow { Owner = this };
            // The input window keeps native EDIT and InkOverlay hit testing.
            // Its click-through companion draws independently of input opacity.
            QueueNoteVisual();
            if (!backgroundVisual.Visible) RenderNoteVisual();
        }

        private void QueueNoteVisual()
        {
            if (closing || renderingNote || backgroundTimer == null || !useBackgroundVisual || !Visible) return;
            noteVisualDirty = true;
            backgroundTimer.Start();
        }

        private void WatchNoteEditor(NoteTextEditor editor)
        {
            editor.Invalidated += delegate { QueueNoteVisual(); };
            editor.TextChanged += delegate { if (IsHeadline) ResizeMemoEditor(); QueueNoteVisual(); };
            editor.FontChanged += delegate { if (IsHeadline) ResizeMemoEditor(); QueueNoteVisual(); };
            editor.KeyUp += delegate { QueueNoteVisual(); };
            editor.MouseUp += delegate { QueueNoteVisual(); };
            editor.MouseWheel += delegate { QueueNoteVisual(); };
            editor.GotFocus += delegate { QueueNoteVisual(); };
        }

        private void RenderNoteVisual()
        {
            if (renderingNote || closing || !Visible || !useBackgroundVisual || backgroundVisual == null) return;
            renderingNote = true;
            noteVisualDirty = false;
            try
            {
                if (noteSnapshot == null || noteSnapshot.Size != ClientSize)
                {
                    DisposeNoteBitmaps();
                    noteSnapshot = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppArgb);
                    notePixels = new Bitmap(ClientSize.Width, ClientSize.Height, PixelFormat.Format32bppPArgb);
                }
                Rectangle frame = ClientRectangle;
                bool showChrome = !IsHeadline || headlineChromeVisible;
                if (showChrome) DrawToBitmap(noteSnapshot, frame);
                Rectangle body = paper.Bounds;
                if (inkPixels == null || inkPixels.Size != body.Size)
                {
                    inkPixels?.Dispose();
                    inkPixels = new Bitmap(body.Width, body.Height, PixelFormat.Format32bppPArgb);
                }
                using (Graphics inkGraphics = Graphics.FromImage(inkPixels))
                {
                    inkGraphics.Clear(Color.Transparent);
                    PaintNoteContents(inkGraphics, true);
                    if (IsHeadline && IsMemoEditor && !showChrome)
                        DrawHeadlineCaption(inkGraphics, textEditor.Text, textEditor.Font, textEditor.ForeColor);
                }
                if (closing || !Visible || !useBackgroundVisual) return;
                using (Graphics graphics = Graphics.FromImage(notePixels))
                using (var key = new ImageAttributes())
                {
                    graphics.Clear(Color.Transparent);
                    DrawNoteBackground(graphics, body);
                    graphics.DrawImageUnscaled(inkPixels, body.Location);
                    if (showChrome)
                    {
                        key.SetColorKey(PaperKey, PaperKey);
                        if (!IsHeadline) graphics.DrawImage(noteSnapshot, frame, 0, 0, frame.Width, frame.Height, GraphicsUnit.Pixel, key);
                        else
                        {
                            CopyNoteChrome(graphics, key, titleBar.Bounds);
                            foreach (Panel grip in grips.Values) CopyNoteChrome(graphics, key, grip.Bounds);
                            if (IsEditingText)
                            {
                                Rectangle editor = textEditor.Bounds;
                                editor.Offset(body.Location);
                                CopyNoteChrome(graphics, key, editor);
                            }
                        }
                        if (IsEditingText && textEditor.Focused && GetCaretPos(out Point caret))
                        {
                            Point origin = PointToClient(textEditor.PointToScreen(Point.Empty));
                            Rectangle limit = new Rectangle(origin, textEditor.ClientSize);
                            caret.Offset(origin);
                            graphics.SetClip(limit);
                            using (var pen = new Pen(textEditor.ForeColor, 1))
                                graphics.DrawLine(pen, caret.X, caret.Y, caret.X, caret.Y + textEditor.Font.Height);
                        }
                    }
                }
                backgroundVisual.Present(notePixels, Location);
                if (!backgroundVisual.Visible) backgroundVisual.Show();
                Opacity = 1.0 / 255;
            }
            finally
            {
                renderingNote = false;
                if (!Visible || !useBackgroundVisual) DisposeNoteBitmaps();
            }
        }

        private void CopyNoteChrome(Graphics graphics, ImageAttributes key, Rectangle region)
        {
            graphics.DrawImage(noteSnapshot, region, region.X, region.Y, region.Width, region.Height, GraphicsUnit.Pixel, key);
        }

        private void DrawHeadlineCaption(Graphics graphics, string content, Font font, Color color)
        {
            if (String.IsNullOrEmpty(content)) return;
            Rectangle bounds = HeadlineCaptionBounds;
            if (bounds.Width < 1 || bounds.Height < 1) return;
            GraphicsState saved = graphics.Save();
            try
            {
                graphics.SetClip(bounds, CombineMode.Intersect);
                using (var brush = new SolidBrush(color))
                using (var format = new StringFormat(StringFormat.GenericDefault)
                {
                    Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                }) graphics.DrawString(content, font, brush, bounds, format);
            }
            finally { graphics.Restore(saved); }
        }

        private void DrawNoteBackground(Graphics graphics, Rectangle body)
        {
            if (backgroundPercent == 0 || body.Width < 2 || body.Height < 2) return;
            int alpha = (int)Math.Round(255 * backgroundPercent / 100.0);
            if (IsHeadline) HeadlineTemplate.Draw(graphics, body, backgroundShape, alpha, headlineFrameColor, headlineFillColor);
            else using (var brush = new SolidBrush(Color.FromArgb(alpha, Color.White))) graphics.FillRectangle(brush, body);
        }

        private void DisposeNoteBitmaps()
        {
            noteSnapshot?.Dispose();
            notePixels?.Dispose();
            inkPixels?.Dispose();
            noteSnapshot = notePixels = inkPixels = null;
        }

        private void DisposeNoteBackground()
        {
            backgroundTimer?.Dispose();
            headlineHoverTimer?.Dispose();
            backgroundVisual?.Dispose();
            backgroundMenu?.Dispose();
            headlineColorPalette?.Dispose();
            DisposeNoteBitmaps();
        }

        [DllImport("user32.dll")] private static extern bool GetCaretPos(out Point point);
        [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
        [DllImport("user32.dll")] private static extern bool IsChild(IntPtr parent, IntPtr child);
    }

    internal sealed class NoteBackgroundSlider : Control
    {
        private int value = 100;
        internal event Action<int> Changed;

        internal NoteBackgroundSlider()
        {
            Name = "NoteBackgroundOpacity";
            Size = new Size(90, 28);
            Top = 3;
            Cursor = System.Windows.Forms.Cursors.Hand;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.Selectable, false);
            Value = 100;
        }

        internal int Value
        {
            get => value;
            set
            {
                this.value = value;
                AccessibleName = "노트 배경 불투명도 " + value + "%";
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent.BackColor);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int thumb = 5 + (Width - 10) * Value / 100;
            using (var track = new Pen(Color.FromArgb(150, 167, 188), 3))
            using (var accent = new SolidBrush(Color.FromArgb(21, 102, 137)))
            {
                e.Graphics.DrawLine(track, 5, 8, Width - 5, 8);
                e.Graphics.FillEllipse(accent, thumb - 4, 4, 8, 8);
            }
            TextRenderer.DrawText(e.Graphics, "배경 " + Value + "%", SystemFonts.SmallCaptionFont,
                new Rectangle(0, 12, Width, 15), Color.FromArgb(46, 60, 78),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Capture = true;
            SlideTo(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (Capture && e.Button == MouseButtons.Left) SlideTo(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !Capture) return;
            SlideTo(e.X);
            Capture = false;
        }

        private void SlideTo(int x)
        {
            int next = Math.Max(0, Math.Min(100, (x - 5) * 100 / (Width - 10)));
            if (next != Value) Changed?.Invoke(next);
        }
    }

    internal sealed class NoteVisualWindow : NonActivatingToolbarWindow
    {
        internal NoteVisualWindow() { Name = "NoteBackgroundVisual"; }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= 0x80000 | 0x20; // Layered and click-through.
                return parameters;
            }
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x84) { message.Result = new IntPtr(-1); return; }
            base.WndProc(ref message);
        }

        internal void Present(Bitmap pixels, Point location)
        {
            IntPtr screen = GetDC(IntPtr.Zero);
            IntPtr memory = CreateCompatibleDC(screen);
            IntPtr bitmap = IntPtr.Zero, previous = IntPtr.Zero;
            try
            {
                var info = new BitmapInfo
                {
                    HeaderSize = 40, Width = pixels.Width, Height = -pixels.Height, Planes = 1, BitCount = 32
                };
                bitmap = CreateDIBSection(screen, ref info, 0, out IntPtr targetBits, IntPtr.Zero, 0);
                if (bitmap == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                BitmapData sourceBits = pixels.LockBits(new Rectangle(Point.Empty, pixels.Size),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
                try
                {
                    int rowBytes = pixels.Width * 4;
                    for (int row = 0; row < pixels.Height; row++)
                        CopyMemory(IntPtr.Add(targetBits, row * rowBytes), IntPtr.Add(sourceBits.Scan0, row * sourceBits.Stride),
                            new UIntPtr((uint)rowBytes));
                }
                finally { pixels.UnlockBits(sourceBits); }
                previous = SelectObject(memory, bitmap);
                Point source = Point.Empty;
                Size size = pixels.Size;
                var blend = new Blend { Alpha = 255, Format = 1 };
                if (!UpdateLayeredWindow(Handle, screen, ref location, ref size, memory, ref source, 0, ref blend, 2))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
            finally
            {
                if (previous != IntPtr.Zero && previous != new IntPtr(-1)) SelectObject(memory, previous);
                if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
                if (memory != IntPtr.Zero) DeleteDC(memory);
                if (screen != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screen);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Blend { public byte Operation, Flags, Alpha, Format; }
        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfo
        {
            public int HeaderSize, Width, Height;
            public ushort Planes, BitCount;
            public uint Compression, ImageSize;
            public int XPixelsPerMeter, YPixelsPerMeter;
            public uint ColorsUsed, ColorsImportant;
        }
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateDIBSection(
            IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")] private static extern void CopyMemory(
            IntPtr destination, IntPtr source, UIntPtr size);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool UpdateLayeredWindow(
            IntPtr window, IntPtr screen, ref Point target, ref Size size, IntPtr source, ref Point origin, int key, ref Blend blend, int flags);
    }
}
