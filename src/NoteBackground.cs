using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Ink;

namespace gInk
{
    internal enum NoteBackgroundShape { Rectangle, Rounded, Oval, Underline }

    public sealed partial class WhiteboardLayerForm
    {
        private static readonly Color PaperKey = Color.FromArgb(254, 253, 252);
        private int backgroundPercent = 100;
        private NoteBackgroundShape backgroundShape;
        private NoteBackgroundSlider backgroundSlider;
        private Button backgroundShapeButton;
        private ContextMenuStrip backgroundMenu;
        private NoteVisualWindow backgroundVisual;
        private Timer backgroundTimer;
        private Bitmap noteSnapshot, notePixels, inkPixels;
        private bool renderingNote, noteVisualDirty;
        private Stroke previewInkStroke;
        private bool useBackgroundVisual => IsHeadline || backgroundPercent != 100;

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
                backgroundMenu = new ContextMenuStrip { Name = "HeadlineBackgroundShapes" };
                AddBackgroundShape("사각형", NoteBackgroundShape.Rectangle);
                AddBackgroundShape("둥근 사각형", NoteBackgroundShape.Rounded);
                AddBackgroundShape("타원형", NoteBackgroundShape.Oval);
                AddBackgroundShape("밑줄형", NoteBackgroundShape.Underline);
                backgroundShapeButton = new Button
                {
                    Name = "HeadlineShape", Text = "모양", AccessibleName = "헤드라인 배경 모양",
                    Size = new Size(44, 26), Top = 4, TabStop = false,
                    FlatStyle = FlatStyle.Flat, BackColor = titleBar.BackColor, ForeColor = title.ForeColor
                };
                backgroundShapeButton.FlatAppearance.BorderSize = 0;
                backgroundShapeButton.Click += delegate
                {
                    foreach (ToolStripMenuItem item in backgroundMenu.Items)
                        item.Checked = (NoteBackgroundShape)item.Tag == backgroundShape;
                    backgroundMenu.Show(backgroundShapeButton, new Point(0, backgroundShapeButton.Height));
                };
                titleBar.Controls.Add(backgroundShapeButton);
            }
            PlaceNoteTitle(titleBar.ClientSize.Width - 71);
            backgroundTimer = new Timer { Interval = 25 };
            backgroundTimer.Tick += delegate
            {
                backgroundTimer.Stop();
                if (Visible && useBackgroundVisual && noteVisualDirty) RenderNoteVisual();
            };
            LocationChanged += delegate { if (backgroundVisual != null) backgroundVisual.Location = Location; };
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

        private void AddBackgroundShape(string label, NoteBackgroundShape shape)
        {
            var item = new ToolStripMenuItem(label) { Tag = shape };
            item.Click += delegate { ChangeNoteBackground(backgroundPercent, shape); };
            backgroundMenu.Items.Add(item);
        }

        private void PlaceNoteTitle(int renameLeft)
        {
            int right = renameLeft - 4;
            if (backgroundShapeButton != null)
            {
                backgroundShapeButton.Left = right - backgroundShapeButton.Width;
                right = backgroundShapeButton.Left - 4;
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
            UpdateNoteBackground();
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
                backgroundTimer.Stop();
                backgroundVisual?.Hide();
                if (!composite) Opacity = 1;
                if (!renderingNote) DisposeNoteBitmaps();
                return;
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
            editor.TextChanged += delegate { QueueNoteVisual(); };
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
                DrawToBitmap(noteSnapshot, frame);
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
                }
                if (closing || !Visible || !useBackgroundVisual) return;
                using (Graphics graphics = Graphics.FromImage(notePixels))
                using (var key = new ImageAttributes())
                {
                    graphics.Clear(Color.Transparent);
                    DrawNoteBackground(graphics, body);
                    graphics.DrawImageUnscaled(inkPixels, body.Location);
                    key.SetColorKey(PaperKey, PaperKey);
                    graphics.DrawImage(noteSnapshot, frame, 0, 0, frame.Width, frame.Height, GraphicsUnit.Pixel, key);
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

        private void DrawNoteBackground(Graphics graphics, Rectangle body)
        {
            if (backgroundPercent == 0 || body.Width < 2 || body.Height < 2) return;
            int alpha = (int)Math.Round(255 * backgroundPercent / 100.0);
            if (backgroundShape == NoteBackgroundShape.Rectangle)
            {
                using (var brush = new SolidBrush(Color.FromArgb(alpha, Color.White)))
                    graphics.FillRectangle(brush, body);
                return;
            }
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color accent = Color.FromArgb(alpha, 37, 124, 157);
            if (backgroundShape == NoteBackgroundShape.Underline)
            {
                using (var pen = new Pen(accent, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    graphics.DrawLine(pen, body.Left + 12, body.Bottom - 7, body.Right - 12, body.Bottom - 7);
                return;
            }
            body.Inflate(-2, -2);
            using (var path = new GraphicsPath())
            using (var brush = new SolidBrush(Color.FromArgb(alpha, 247, 252, 255)))
            using (var outline = new Pen(accent, 2))
            {
                if (backgroundShape == NoteBackgroundShape.Oval) path.AddEllipse(body);
                else
                {
                    int diameter = Math.Min(28, Math.Min(body.Width, body.Height));
                    if (diameter < 2) return;
                    path.AddArc(body.Left, body.Top, diameter, diameter, 180, 90);
                    path.AddArc(body.Right - diameter, body.Top, diameter, diameter, 270, 90);
                    path.AddArc(body.Right - diameter, body.Bottom - diameter, diameter, diameter, 0, 90);
                    path.AddArc(body.Left, body.Bottom - diameter, diameter, diameter, 90, 90);
                    path.CloseFigure();
                }
                graphics.FillPath(brush, path);
                graphics.DrawPath(outline, path);
            }
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
            backgroundVisual?.Dispose();
            backgroundMenu?.Dispose();
            DisposeNoteBitmaps();
        }

        [DllImport("user32.dll")] private static extern bool GetCaretPos(out Point point);
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
