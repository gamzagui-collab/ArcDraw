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
                noteActions.Items.Add("이름 변경…",null,delegate {ShowRenameNote(target);});target.FillLegacyTextActions(noteActions);e.Cancel=false;
            };
        }
        public bool RenameNote(WhiteboardLayerForm note,string name)
        {
            if(disposed||note==null||!notes.Contains(note)||note.IsDisposed)return false;
            var clean=new StringBuilder();foreach(char c in (name??""))if(!char.IsControl(c))clean.Append(c);
            string value=clean.ToString().Trim();if(value.Length==0||value.Length>40)return false;
            note.SetDisplayName(value);bar.UpdateSelection();if(root.DrawingSession!=null)root.DrawingSession.NoteLabelChanged();return true;
        }
        internal void ShowRenameNote(WhiteboardLayerForm note)
        {
            if(disposed||note==null||note.IsDisposed||!notes.Contains(note))return;
            note.FinishEditing(true);
            using(var dialog=new Form {Name="ArcDrawRenameNote",Text="노트 이름 변경",ClientSize=new Size(370,132),FormBorderStyle=FormBorderStyle.FixedDialog,
                MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=false,TopMost=true,StartPosition=FormStartPosition.CenterScreen,Font=new Font("Malgun Gothic",9f)})
            {
                var field=new TextBox {Name="NoteNameInput",Text=note.DisplayName,MaxLength=40,Bounds=new Rectangle(18,20,334,28)};
                var save=new Button {Name="SaveNoteName",Text="변경",DialogResult=DialogResult.OK,Bounds=new Rectangle(174,76,82,30)};
                var cancel=new Button {Text="취소",DialogResult=DialogResult.Cancel,Bounds=new Rectangle(270,76,82,30)};
                field.TextChanged+=delegate {save.Enabled=!string.IsNullOrWhiteSpace(field.Text);};
                dialog.Controls.AddRange(new Control[]{field,save,cancel});dialog.AcceptButton=save;dialog.CancelButton=cancel;
                dialog.Shown+=delegate {field.Focus();field.SelectAll();};
                if(dialog.ShowDialog(root.FormCollection)==DialogResult.OK)RenameNote(note,field.Text);
            }
            TextEditingEnded();
        }
        internal void FinishMemoEditors(){foreach(var note in notes.ToArray())if(note.IsMemoEditor)note.FinishEditing(true);}
    }
    public sealed partial class WhiteboardLayerForm
    {
        private string noteName;
        public string DisplayName {get{return noteName??"노트 "+Number;}}
        internal void SetDisplayName(string value){noteName=value;Text=value;title.Text="⠿  "+value;title.AccessibleName=value;}
        private void InitializeNoteIdentity()
        {
            title.UseMnemonic=false;title.AutoEllipsis=true;SetDisplayName(DisplayName);
            var rename=new Button {Name="RenameNote",Text="✎",AccessibleName="노트 이름 변경",Size=new Size(27,27),Top=3,
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
