using System;
using System.Drawing;
using System.Windows.Forms;

namespace gInk
{
    public sealed partial class WhiteboardLayerForm
    {
        private bool editingMemo,memoOpenQueued,memoPaused,endingMemoFromHeader;
        internal bool MemoTargetMode {get;set;}
        internal void MemoToolSelected(bool text)
        {
            MemoTargetMode=text;
            if(!endingMemoFromHeader)memoPaused=false;
        }
        internal void CancelHeaderMemoResume(){if(!endingMemoFromHeader)memoPaused=false;}
        private bool pendingPenFocus;
        internal void QueuePenFocus()
        {
            if(closing||memoPaused||MemoTargetMode||IsEditingText||endingMemoFromHeader)return;
            pendingPenFocus=true;QueueInkWork();
        }
        private void ApplyPenFocus()
        {
            pendingPenFocus=false;
            DrawingSession session=manager.CurrentRoot.DrawingSession;
            if(!closing&&Visible&&manager.ActiveNote==this&&!memoPaused&&!MemoTargetMode&&!IsEditingText&&
                session!=null&&session.DrawingEnabled&&!session.Editing)FocusDrawingSurface();
        }
        private string memoFontFamily="Malgun Gothic";
        private float memoFontPoints=17f;
        internal const int MinimumMemoPoints = 6;
        private FontStyle memoFontStyle=FontStyle.Regular;
        private Color memoColor=Color.FromArgb(24,33,46);
        private NoteTextItem MemoItem {get{return textItems.Find(item=>item.IsMemo);}}
        internal Font CreateMemoFont(){NoteTextItem item=MemoItem;return item==null?new Font(memoFontFamily,memoFontPoints,memoFontStyle,GraphicsUnit.Point):item.CreateFont();}
        internal Color MemoColor {get{NoteTextItem item=MemoItem;return item==null?memoColor:item.Color;}}
        internal bool ApplyMemoStyle(Font font,Color color)
        {
            FinishEditing(true);
            if(!FlushHistorySnapshot()||HasBusyInk)return false;
            memoFontFamily=font.Name;memoFontPoints=font.SizeInPoints;memoFontStyle=font.Style;memoColor=Color.FromArgb(color.R,color.G,color.B);
            foreach(NoteTextItem item in textItems)if(item.IsMemo)
            {item.FontFamily=memoFontFamily;item.FontSize=memoFontPoints;item.FontStyle=memoFontStyle;item.Color=memoColor;}
            if(MemoItem!=null)QueueHistorySnapshot();manager.NotesChanged();paper.Invalidate();return true;
        }
        internal bool IsMemoEditor {get{return editingMemo&&IsEditingText;}}
        internal int? LiveMemoSize => IsMemoEditor ? (int?)Math.Round(editorFont.SizeInPoints) : null;
        internal Color? LiveMemoColor => IsMemoEditor ? (Color?)textEditor.ForeColor : null;
        internal string LiveMemoFamily => IsMemoEditor ? editorFont.Name : null;
        private int pendingMemoPoints;
        private string pendingMemoFamily;
        internal void SetLiveMemoColor(Color color)
        {
            if (!IsMemoEditor || completingEditor || textEditor.ForeColor.ToArgb() == color.ToArgb()) return;
            NoteTextEditor editor = textEditor;
            if (editor.HasComposition())
            {
                editor.FinishComposition(true);
                // Apply the repaint after EDIT receives the composed text.
                editor.BeginInvoke(new Action(delegate
                {
                    if (textEditor == editor && manager.ActiveNote == this) ApplyLiveMemoColor(editor, color);
                }));
                return;
            }
            ApplyLiveMemoColor(editor, color);
        }

        private void ApplyLiveMemoColor(NoteTextEditor editor, Color color)
        {
            editor.ForeColor = color;
            manager.NotesChanged();
        }



        internal void SetLiveMemoSize(int points)
        {
            SetLiveMemoFont(null, points);
        }

        internal void SetLiveMemoFamily(string family)
        {
            SetLiveMemoFont(family, 0);
        }

        private void SetLiveMemoFont(string family, int points)
        {
            if (!IsMemoEditor || completingEditor) return;
            bool pending = pendingMemoFamily != null || pendingMemoPoints != 0;
            if (family != null) pendingMemoFamily = family;
            if (points != 0) pendingMemoPoints = points;
            if (pending) return;
            NoteTextEditor editor = textEditor;
            if (editor.HasComposition())
            {
                editor.FinishComposition(true);
                // Let EDIT receive the Korean result before WM_SETFONT.
                editor.BeginInvoke(new Action(delegate
                {
                    if (textEditor != editor) return;
                    ApplyPendingMemoFont();
                }));
                return;
            }
            ApplyPendingMemoFont();
        }

        private void ApplyPendingMemoFont()
        {
            Font previous = editorFont;
            string family = pendingMemoFamily ?? previous.Name;
            float points = pendingMemoPoints == 0 ? previous.SizeInPoints : pendingMemoPoints;
            pendingMemoFamily = null;
            pendingMemoPoints = 0;
            if (family == previous.Name && points == previous.SizeInPoints) return;
            try { editorFont = new Font(family, points, previous.Style, GraphicsUnit.Point); }
            catch (ArgumentException) { editorFont = new Font(family, points, FontStyle.Regular, GraphicsUnit.Point); }
            textEditor.Font = editorFont;
            previous.Dispose();
            manager.NotesChanged();
        }
        private void EndTextInputFromHeader()
        {
            bool resume=memoPaused||MemoTargetMode||IsMemoEditor;
            MemoTargetMode=false;
            memoPaused=resume;
            FinishEditing(true);
            Root root=manager.CurrentRoot;
            endingMemoFromHeader=true;
            try
            {
            if(root.ToolSelected==Tools.txtLeftAligned||root.ToolSelected==Tools.txtRightAligned)
                root.FormCollection.SelectTool(Tools.Hand,Filling.Empty);
            else SyncInkTool(root);
            }
            finally {endingMemoFromHeader=false;}
            // The next key belongs to the drawing shortcuts, not the old EDIT.
            if(!paper.IsDisposed)paper.Focus();
        }
        private bool ResumeMemoFromBody(MouseEventArgs e)
        {
            if(!memoPaused||e.Button!=MouseButtons.Left)return false;
            Root root=manager.CurrentRoot;
            if(root.EraserMode){memoPaused=false;return false;}
            memoPaused=false;MemoTargetMode=true;
            if(root.ToolSelected!=Tools.txtLeftAligned&&root.ToolSelected!=Tools.txtRightAligned)
                root.FormCollection.SelectTool(Tools.txtLeftAligned,Filling.Empty);
            else SyncInkTool(root);
            OpenMemoEditor();return true;
        }
        private bool CanOpenMemo()
        {
            DrawingSession session=manager.CurrentRoot.DrawingSession;
            if(closing||memoPaused||sessionSuspended||!Visible||!textMode||manager.ActiveNote!=this||session==null||!session.DrawingEnabled||
                session.OptionsOpen||session.SelectingTarget||session.LeavingDrawing)return false;
            foreach(Form form in Application.OpenForms)if(form.Visible&&form.Modal)return false;
            return true;
        }
        internal void QueueMemoEditor()
        {
            if(memoOpenQueued||textEditor!=null||!CanOpenMemo()||!paper.IsHandleCreated)return;
            memoOpenQueued=true;
            paper.BeginInvoke(new Action(delegate {memoOpenQueued=false;if(!IsDisposed&&textEditor==null&&CanOpenMemo())OpenMemoEditor();}));
        }
        private void OpenMemoEditor()
        {
            if(!CanOpenMemo())return;
            if(IsMemoEditor){textEditor.Focus();return;}
            FinishEditing(true);NoteTextItem memo=null;
            foreach(NoteTextItem item in textItems)if(item.IsMemo){memo=item;break;}
            StartTextEditor(memo,new Point(8,8),true);
        }
        private void ResizeMemoEditor()
        {
            if(IsMemoEditor)textEditor.Bounds=new Rectangle(8,8,Math.Max(20,paper.ClientSize.Width-16),Math.Max(20,paper.ClientSize.Height-16));
        }
        internal void FillLegacyTextActions(ContextMenuStrip menu)
        {
            ToolStripMenuItem group=null;
            foreach(NoteTextItem item in textItems)
            {
                if(item.IsMemo)continue;
                if(group==null){group=new ToolStripMenuItem("기존 배치 글자 편집");menu.Items.Add(group);}
                NoteTextItem current=item;string label=(item.Content??"").Replace("\r"," ").Replace("\n"," ");
                if(label.Length>24)label=label.Substring(0,24)+"…";
                group.DropDownItems.Add(label.Replace("&","&&"),null,delegate {manager.SetActive(this);FinishEditing(true);EditTextAt(current.Position);});
            }
        }
    }
}
