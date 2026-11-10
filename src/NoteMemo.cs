using System;
using System.Drawing;
using System.Windows.Forms;

namespace gInk
{
    public sealed partial class WhiteboardLayerForm
    {
        private bool editingMemo,memoOpenQueued;
        internal bool MemoTargetMode {get;set;}
        private string memoFontFamily="Malgun Gothic";
        private float memoFontPoints=17f;
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
        private bool CanOpenMemo()
        {
            DrawingSession session=manager.CurrentRoot.DrawingSession;
            if(closing||sessionSuspended||!Visible||!textMode||manager.ActiveNote!=this||session==null||!session.DrawingEnabled||
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
