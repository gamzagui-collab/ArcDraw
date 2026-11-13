// Note-local text editing. The existing full-screen Ink text path is unchanged.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Ink;
using Cursors = System.Windows.Forms.Cursors;

namespace gInk
{
    public sealed partial class WhiteboardLayerForm
    {
        // Positions and layout widths are paper-local pixels, never screen or
        // Microsoft.Ink coordinates. Font size is in points and is not resized.
        internal sealed class NoteTextItem
        {
            public Point Position;
            public string Content;
            public Color Color;
            public string FontFamily;
            public float FontSize;
            public FontStyle FontStyle;
            public int LayoutWidth;
            public bool IsMemo;

            public Font CreateFont()
            {
                return new Font(FontFamily, FontSize, FontStyle, GraphicsUnit.Point);
            }
        }

        private sealed class NotePaper : Panel
        {
            public NotePaper()
            {
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
                TabStop = true;
            }
        }

        private readonly List<NoteTextItem> textItems = new List<NoteTextItem>();
        private NoteTextEditor textEditor;
        private NoteTextItem editingText;
        private Font editorFont;
        private bool textMode;
        private bool completingEditor;
        private const TextFormatFlags TextFlags = TextFormatFlags.NoPadding |
            TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;

        public bool IsEditingText { get { return textEditor != null && !textEditor.IsDisposed; } }

        private void PaperMouseDown(object sender, MouseEventArgs e)
        {
            if(manager.CurrentRoot.DrawingSession!=null&&manager.CurrentRoot.DrawingSession.Running&&!manager.CurrentRoot.DrawingSession.DrawingEnabled)return;
            manager.SetActive(this);
            if(ResumeMemoFromBody(e))return;
            SyncInkTool(manager.CurrentRoot);
            if (textMode && e.Button == MouseButtons.Left) OpenMemoEditor();
        }

        private void QueuePaperRefresh()
        {
            if (closing || paper.IsDisposed || !paper.IsHandleCreated) return;
            try
            {
                paper.BeginInvoke(new Action(delegate
                {
                    if (!closing && !paper.IsDisposed) paper.Invalidate();
                }));
            }
            catch (InvalidOperationException) { /* Window is being disposed. */ }
        }

        private void PaperPaint(object sender, PaintEventArgs e)
        {
            InkOverlay ink;
            if (!BeginInkUse(out ink))
            {
                if (!closing && !IsDisposed) repaintPending = true;
                return;
            }
            try
            {
                // Ink COM getters may pump messages. Pin the collector and keep
                // its native resources alive until this complete paint returns.
                using (Strokes strokes = ink.Ink.Strokes)
                {
                    if (closing) return;
                    if (strokes.Count > 0)
                    {
                        if (closing) return;
                        DrawNoteStrokes(e.Graphics,ink,strokes);
                    }
                }
                if (closing) return;
                DrawNotePreview(e.Graphics);
                foreach (NoteTextItem item in textItems.ToArray())
                {
                    if (item == editingText) continue;
                    if(item.IsMemo)item.LayoutWidth=Math.Max(1,paper.ClientSize.Width-16-SystemInformation.VerticalScrollBarWidth);
                    using (Font font = item.CreateFont())
                        TextRenderer.DrawText(e.Graphics, item.Content, font,
                            GetTextBounds(item, font), item.Color, TextFlags);
                }
            }
            finally { EndInkUse(); }
        }

        private static Rectangle GetTextBounds(NoteTextItem item, Font font)
        {
            Size measured = TextRenderer.MeasureText(item.Content, font,
                new Size(Math.Max(1, item.LayoutWidth), int.MaxValue), TextFlags);
            return new Rectangle(item.Position, new Size(Math.Max(1, item.LayoutWidth), measured.Height));
        }

        private Color GetReadableTextColor()
        {
            Color selected = Color.FromArgb(24, 33, 46);
            Root root = manager.CurrentRoot;
            if (root != null && root.FormCollection != null && root.CurrentPen >= 0)
                selected = root.FormCollection.IC.DefaultDrawingAttributes.Color;
            double luminance = 0.2126 * LinearChannel(selected.R) +
                0.7152 * LinearChannel(selected.G) + 0.0722 * LinearChannel(selected.B);
            // Keep normal dark/red/blue pen colors, but reject pale yellow,
            // white, and other low-contrast colors against the white paper.
            if (1.05 / (luminance + 0.05) < 3.0) return Color.FromArgb(24, 33, 46);
            return Color.FromArgb(selected.R, selected.G, selected.B);
        }

        private static double LinearChannel(byte value)
        {
            double v = value / 255.0;
            return v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        private void EditTextAt(Point location)
        {
            FinishEditing(true);
            NoteTextItem item = null;
            // Match the visual stacking order when text boxes overlap.
            for (int index = textItems.Count - 1; index >= 0; index--)
            {
                NoteTextItem candidate = textItems[index];
                if(candidate.IsMemo)continue;
                using (Font font = candidate.CreateFont())
                {
                    Size measured = TextRenderer.MeasureText(candidate.Content, font,
                        new Size(Math.Max(1, candidate.LayoutWidth), int.MaxValue), TextFlags);
                    Rectangle hit = new Rectangle(candidate.Position, measured);
                    hit.Inflate(3, 3);
                    if (hit.Contains(location)) { item = candidate; break; }
                }
            }

            Point origin = item == null
                ? new Point(Math.Max(4, Math.Min(location.X, paper.ClientSize.Width - 8)),
                            Math.Max(4, Math.Min(location.Y, paper.ClientSize.Height - 8)))
                : item.Position;
            StartTextEditor(item,origin,false);
        }

        private void StartTextEditor(NoteTextItem item,Point origin,bool memo)
        {
            editingText=item;editingMemo=memo;
            editorFont = item == null ? (memo?CreateMemoFont():manager.CurrentRoot.Settings.CreateNoteFont()) : item.CreateFont();
            NoteTextEditor editor = new NoteTextEditor();
            editor.DocumentMode=memo;
            editor.Multiline = true;
            editor.AcceptsReturn = true;
            editor.WordWrap = true;
            editor.ScrollBars = ScrollBars.Vertical;
            editor.ImeMode = ImeMode.NoControl;
            editor.MaxLength = 0;
            editor.Font = editorFont;
            editor.ForeColor = item == null ? (memo?MemoColor:GetReadableTextColor()) : item.Color;
            editor.BackColor = memo?Color.White:Color.FromArgb(255, 254, 240);
            editor.BorderStyle = memo?BorderStyle.None:BorderStyle.FixedSingle;
            editor.Location = origin;
            int width = item == null ? Math.Min(640, Math.Max(90, paper.ClientSize.Width - origin.X - 8))
                : item.LayoutWidth + SystemInformation.VerticalScrollBarWidth + 4;
            editor.Size = new Size(width, Math.Min(140, Math.Max(44, paper.ClientSize.Height - origin.Y - 8)));
            if(memo)editor.Size=new Size(Math.Max(20,paper.ClientSize.Width-16),Math.Max(20,paper.ClientSize.Height-16));
            editor.Text = item == null ? String.Empty : item.Content;
            editor.TextChanged+=delegate {manager.NotesChanged();};
            editor.MouseDown+=delegate {manager.SetActive(this);};
            editor.CommitRequested += delegate { FinishEditing(true); };
            editor.CancelRequested += delegate { FinishEditing(false); };
            editor.Leave += delegate { if (!closing) FinishEditing(true); };
            textEditor = editor;
            paper.Controls.Add(editor);
            editor.BringToFront();
            editor.Focus();
            editor.SelectionStart = editor.TextLength;
            if(memo)editor.ScrollToCaret();
            paper.Invalidate();
            if (memo) manager.UpdateMemoColorButton();
        }

        internal void FinishEditing(bool save)
        {
            if (textEditor == null || completingEditor) return;
            completingEditor = true;
            NoteTextEditor old = textEditor;
            NoteTextItem item = editingText;
            bool hadFocus = old.Focused;
            bool wasMemo=editingMemo;
            bool contentChanged = false;
            try
            {
                // Flush the final Korean composition before reading Text.
                // Cancel also cancels an in-flight composition before disposal.
                old.FinishComposition(save);
                if (save && !old.IsDisposed)
                {
                    string content = old.Text;
                    if (wasMemo && memoFontPoints != old.Font.SizeInPoints)
                    {
                        memoFontPoints = old.Font.SizeInPoints;
                        manager.NotesChanged();
                    }
                    if (wasMemo && memoColor.ToArgb() != old.ForeColor.ToArgb())
                    {
                        memoColor = old.ForeColor;
                        manager.NotesChanged();
                    }
                    if (editingMemo?content.Length>0:!String.IsNullOrWhiteSpace(content))
                    {
                        if (item == null)
                        {
                            item = new NoteTextItem();
                            item.Position = old.Location;
                            item.Color = old.ForeColor;
                            item.FontFamily = old.Font.Name;
                            item.FontSize = old.Font.SizeInPoints;
                            item.FontStyle = old.Font.Style;
                            item.LayoutWidth = Math.Max(1, old.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 2);
                            item.IsMemo=editingMemo;
                            textItems.Add(item);
                            contentChanged=true;
                        }
                        if(item.Content!=content)contentChanged=true;
                        item.Content = content;
                        if (wasMemo && item.FontSize != old.Font.SizeInPoints)
                        {
                            item.FontSize = old.Font.SizeInPoints;
                            contentChanged = true;
                        }
                        if (wasMemo && item.Color.ToArgb() != old.ForeColor.ToArgb())
                        {
                            item.Color = old.ForeColor;
                            contentChanged = true;
                        }
                        if(item.IsMemo){item.Position=new Point(8,8);item.LayoutWidth=Math.Max(1,paper.ClientSize.Width-16-SystemInformation.VerticalScrollBarWidth);}
                    }
                    else if (item != null) { textItems.Remove(item);contentChanged=true; }
                }
            }
            finally
            {
                textEditor = null;
                pendingMemoPoints = 0;
                editingText = null;
                editingMemo=false;
                paper.Controls.Remove(old);
                old.Dispose();
                if (editorFont != null) { editorFont.Dispose(); editorFont = null; }
                completingEditor = false;
                manager.TextEditingEnded();
                if (wasMemo) manager.UpdateMemoColorButton();
                if(contentChanged&&!closing)QueueHistorySnapshot();
                if (!paper.IsDisposed)
                {
                    paper.Invalidate();
                    if (hadFocus && !closing&&!wasMemo) paper.Focus();
                }
            }
        }

        private bool SetTextMode(InkOverlay ink, bool enabled)
        {
            if (textMode == enabled) return true;
            // InkOverlay.Enabled/EditingMode cannot change mid-stroke; retry on
            // the manager's next synchronization tick after the stroke ends.
            if (ink.CollectingInk) return false;
            bool previous = textMode;
            textMode = enabled;
            try { ink.Enabled = !enabled; }
            catch { textMode = previous; throw; }
            if (!enabled) FinishEditing(true);
            paper.Cursor = enabled ? Cursors.IBeam : Cursors.Cross;
            if(enabled)QueueMemoEditor();
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                closing = true;
                if (inkUseDepth != 0)
                {
                    closePending = true;
                    return;
                }
                FinishEditing(false);
                textItems.Clear();
                CancelNoteGesture();
                history.Clear();
                if (layerInk != null)
                {
                    try { layerInk.Enabled = false; } catch (InvalidOperationException) { }
                    layerInk.Dispose();
                    layerInk = null;
                }
                noteHaloCursor?.Dispose();
                noteHaloCursor = null;
                appliedNoteInkCursor = null;
            }
            base.Dispose(disposing);
        }
    }

    internal sealed class NoteTextEditor : TextBox
    {
        internal bool DocumentMode;
        internal event EventHandler CommitRequested;
        internal event EventHandler CancelRequested;
        private bool composing;
        private bool completionQueued;

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                // Framework 4.8 otherwise retains its cached HFONT until the
                // disposed control is finalized. Release that cache on the UI
                // thread after the native EDIT window no longer uses it.
                Font = null;
            }
        }

        private void QueueCompletion(bool commit)
        {
            if (completionQueued || IsDisposed || !IsHandleCreated) return;
            completionQueued = true;
            // Finish the native key/IME message before destroying its EDIT HWND.
            BeginInvoke(new Action(delegate
            {
                if (IsDisposed) return;
                EventHandler handler = commit ? CommitRequested : CancelRequested;
                if (handler != null) handler(this, EventArgs.Empty);
            }));
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Enter))
            {
                QueueCompletion(true);
                return true;
            }
            if (keyData == Keys.Escape)
            {
                if(DocumentMode&&composing)return base.ProcessCmdKey(ref msg,keyData);
                QueueCompletion(DocumentMode);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void WndProc(ref Message m)
        {
            // An active IME may deliver these instead of ordinary WM_KEYDOWN.
            if (m.Msg == 0x0290 && (int)m.WParam == (int)Keys.Escape)
            {
                if(DocumentMode&&composing){base.WndProc(ref m);return;}
                QueueCompletion(DocumentMode);
                return;
            }
            if (m.Msg == 0x0290 && (int)m.WParam == (int)Keys.Enter &&
                (ModifierKeys & Keys.Control) != 0)
            {
                QueueCompletion(true);
                return;
            }
            if (m.Msg == 0x010D) composing = true; // WM_IME_STARTCOMPOSITION
            base.WndProc(ref m);
            if (m.Msg == 0x010E) composing = false; // WM_IME_ENDCOMPOSITION
        }

        internal void FinishComposition(bool commit)
        {
            if (!IsHandleCreated || IsDisposed) return;
            IntPtr context = ImmGetContext(Handle);
            if (context == IntPtr.Zero) return;
            try
            {
                if (composing || ImmGetCompositionString(context, 8, IntPtr.Zero, 0) > 0)
                    ImmNotifyIME(context, 0x0015, commit ? 1u : 4u, 0);
            }
            finally { ImmReleaseContext(Handle, context); }
            composing = false;
        }

        internal bool HasComposition()
        {
            if (!IsHandleCreated || IsDisposed) return false;
            IntPtr context = ImmGetContext(Handle);
            if (context == IntPtr.Zero) return false;
            try { return ImmGetCompositionString(context, 8, IntPtr.Zero, 0) > 0; }
            finally { ImmReleaseContext(Handle, context); }
        }

        [DllImport("imm32.dll")]
        private static extern IntPtr ImmGetContext(IntPtr window);
        [DllImport("imm32.dll")]
        private static extern bool ImmReleaseContext(IntPtr window, IntPtr context);
        [DllImport("imm32.dll")]
        private static extern bool ImmNotifyIME(IntPtr context, uint action, uint index, uint value);
        [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
        private static extern int ImmGetCompositionString(IntPtr context, uint index, IntPtr buffer, uint size);
    }
}
