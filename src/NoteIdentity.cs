using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace gInk
{
    public sealed partial class WhiteboardLayerManager
    {
        private readonly ContextMenuStrip noteActions=new ContextMenuStrip {Name="ArcDrawNoteActions"};
        internal ContextMenuStrip NoteContextMenu {get{return noteActions;}}
        private void InitializeNoteContextMenu()
        {
            noteActions.Opening+=delegate(object sender,System.ComponentModel.CancelEventArgs e)
            {
                Control source=noteActions.SourceControl;
                WhiteboardLayerForm note=source==null?null:source.Tag as WhiteboardLayerForm;
                if(note==null&&source!=null)note=source.FindForm() as WhiteboardLayerForm;
                if(note==null||note.IsDisposed||!notes.Contains(note)){e.Cancel=true;return;}
                while(noteActions.Items.Count>0){ToolStripItem old=noteActions.Items[0];noteActions.Items.RemoveAt(0);old.Dispose();}
                WhiteboardLayerForm target=note;
                noteActions.Items.Add("이름·메모 글꼴 설정…",null,delegate {ShowRenameNote(target);});target.FillLegacyTextActions(noteActions);e.Cancel=false;
            };
        }
        public bool RenameNote(WhiteboardLayerForm note,string name)
        {
            if(disposed||note==null||!notes.Contains(note)||note.IsDisposed)return false;
            string value=CleanNoteName(name);if(value==null)return false;
            note.SetDisplayName(value);bar.UpdateSelection();NotesChanged();if(root.DrawingSession!=null)root.DrawingSession.NoteLabelChanged();return true;
        }
        private static string CleanNoteName(string name)
        {
            var clean=new StringBuilder();foreach(char c in (name??""))if(!char.IsControl(c))clean.Append(c);
            string value=clean.ToString().Trim();return value.Length==0||value.Length>40?null:value;
        }
        internal void ShowRenameNote(WhiteboardLayerForm note)
        {
            if(disposed||note==null||note.IsDisposed||!notes.Contains(note))return;
            note.FinishEditing(true);
            Font previewFont=note.CreateMemoFont();
            try
            {
            using(var dialog=new Form {Name="ArcDrawRenameNote",Text="노트 이름 · 메모 글꼴",ClientSize=new Size(460,350),FormBorderStyle=FormBorderStyle.FixedDialog,
                MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=false,TopMost=true,StartPosition=FormStartPosition.CenterScreen,Font=new Font("Malgun Gothic",9f)})
            {
                var field=new TextBox {Name="NoteNameInput",Text=note.DisplayName,MaxLength=40,Bounds=new Rectangle(100,18,340,28)};
                var family=new ComboBox {Name="NoteMemoFontFamily",DropDownStyle=ComboBoxStyle.DropDownList,Sorted=true,Bounds=new Rectangle(100,62,225,28)};
                foreach(FontFamily item in FontFamily.Families){family.Items.Add(item.Name);item.Dispose();}
                if(!family.Items.Contains(previewFont.Name))family.Items.Add(previewFont.Name);family.SelectedItem=previewFont.Name;
                var size=new NumericUpDown {Name="NoteMemoFontSize",Minimum=6,Maximum=144,DecimalPlaces=1,Value=Math.Max(6,Math.Min(144,(decimal)previewFont.SizeInPoints)),Bounds=new Rectangle(335,62,105,28)};
                var bold=new CheckBox {Name="NoteMemoBold",Text="굵게",Checked=previewFont.Bold,Bounds=new Rectangle(100,103,90,26)};
                var italic=new CheckBox {Name="NoteMemoItalic",Text="기울임",Checked=previewFont.Italic,Bounds=new Rectangle(190,103,100,26)};
                var color=new Button {Name="NoteMemoColor",Text="색상…",BackColor=note.MemoColor,UseVisualStyleBackColor=false,Bounds=new Rectangle(335,100,105,30)};
                var preview=new Label {Name="NoteMemoPreview",Text="한글 메모  Aa 123",Font=previewFont,ForeColor=note.MemoColor,BackColor=Color.White,BorderStyle=BorderStyle.FixedSingle,TextAlign=ContentAlignment.MiddleLeft,Bounds=new Rectangle(20,146,420,106)};
                var status=new Label {Name="NotePropertiesStatus",Text="이 노트 메모에만 적용",Bounds=new Rectangle(20,263,420,35)};
                var save=new Button {Name="SaveNoteName",Text="적용",Bounds=new Rectangle(254,307,88,30)};
                var cancel=new Button {Text="취소",DialogResult=DialogResult.Cancel,Bounds=new Rectangle(352,307,88,30)};
                Action updatePreview=delegate
                {
                    try
                    {
                    Font next=new Font((string)family.SelectedItem,(float)size.Value,(bold.Checked?FontStyle.Bold:FontStyle.Regular)|(italic.Checked?FontStyle.Italic:FontStyle.Regular),GraphicsUnit.Point);
                    Font old=previewFont;previewFont=next;preview.Font=next;old.Dispose();preview.ForeColor=color.BackColor;
                    color.ForeColor=color.BackColor.GetBrightness()<.5f?Color.White:Color.Black;
                    save.Enabled=CleanNoteName(field.Text)!=null;status.Text="이 노트 메모에만 적용";
                    }
                    catch(ArgumentException){save.Enabled=false;status.Text="이 글꼴에서 지원하는 스타일을 선택해 주세요.";}
                };
                family.SelectedIndexChanged+=delegate {updatePreview();};size.ValueChanged+=delegate {updatePreview();};
                bold.CheckedChanged+=delegate {updatePreview();};italic.CheckedChanged+=delegate {updatePreview();};
                color.BackColorChanged+=delegate {preview.ForeColor=color.BackColor;color.ForeColor=color.BackColor.GetBrightness()<.5f?Color.White:Color.Black;};
                color.Click+=delegate {using(var picker=new ColorDialog {Color=color.BackColor,FullOpen=true})if(picker.ShowDialog(dialog)==DialogResult.OK)color.BackColor=picker.Color;};
                field.TextChanged+=delegate {updatePreview();};
                save.Click+=delegate
                {
                    string name=CleanNoteName(field.Text);if(name==null)return;
                    if(!note.ApplyMemoStyle(previewFont,color.BackColor)){status.Text="필기가 끝난 뒤 다시 적용해 주세요.";return;}
                    RenameNote(note,name);dialog.DialogResult=DialogResult.OK;
                };
                dialog.Controls.AddRange(new Control[]{new Label {Text="이름",Bounds=new Rectangle(20,22,80,24)},new Label {Text="글꼴",Bounds=new Rectangle(20,66,80,24)},field,family,size,bold,italic,color,preview,status,save,cancel});
                dialog.AcceptButton=save;dialog.CancelButton=cancel;updatePreview();
                dialog.Shown+=delegate {field.Focus();field.SelectAll();};
                dialog.ShowDialog(root.FormCollection);
            }
            }
            finally {previewFont.Dispose();}
            TextEditingEnded();
            note.QueueMemoEditor();
        }
        internal void FinishMemoEditors(){foreach(var note in notes.ToArray())if(note.IsMemoEditor)note.FinishEditing(true);}
        internal void FinishTextEditors(){foreach(var note in notes.ToArray())note.FinishEditing(true);}
    }
    public sealed partial class WhiteboardLayerForm
    {
        private string noteName;
        public string DisplayName {get{return noteName??"노트 "+Number;}}
        internal void SetDisplayName(string value){noteName=value;Text=value;title.Text="⠿  "+value;title.AccessibleName=value;}
        private void InitializeNoteIdentity()
        {
            title.UseMnemonic=false;title.AutoEllipsis=true;SetDisplayName(DisplayName);
            var rename=new Button {Name="RenameNote",Text="✎",AccessibleName="노트 이름·메모 글꼴 설정",Size=new Size(27,27),Top=3,
                FlatStyle=FlatStyle.Flat,BackColor=titleBar.BackColor,ForeColor=title.ForeColor,Cursor=Cursors.Hand,TabStop=false};
            rename.FlatAppearance.BorderSize=0;rename.Click+=delegate {manager.ShowRenameNote(this);};titleBar.Controls.Add(rename);
            Action place=delegate {rename.Left=titleBar.ClientSize.Width-71;title.Width=Math.Max(40,rename.Left-title.Left-4);};
            titleBar.Resize+=delegate {place();};place();
            // Shared by headers and list rows. Its lifetime belongs to the manager,
            // never to a Closed event or a rebuilt/disposed source button.
            title.ContextMenuStrip=manager.NoteContextMenu;titleBar.ContextMenuStrip=manager.NoteContextMenu;
        }
    }
}
