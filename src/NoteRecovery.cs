using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml.Linq;

namespace gInk
{
    public sealed partial class WhiteboardLayerManager
    {
        private bool changingNoteArchive;

        internal bool HasDeletedNotes { get { return deletedNotes.HasElements; } }

        internal void ShowDeletedNotes()
        {
            if (disposed || changingNoteArchive || !HasDeletedNotes) return;
            changingNoteArchive = true;
            try
            {
                FinishTextEditors();
                XElement[] entries = deletedNotes.Elements("Note").Reverse().ToArray();
                int selected = ChooseDeletedNote(entries);
                if (selected < 0 || disposed) return;
                if (!RestoreDeletedNote(entries[selected])) return;
            }
            finally
            {
                changingNoteArchive = false;
                TextEditingEnded();
            }
            bar.QueueRebuild();
            KeepWindowsOnTop();
        }

        private int ChooseDeletedNote(XElement[] entries)
        {
            using (var font = new Font("Malgun Gothic", 9f))
            using (var dialog = new Form
            {
                Name = "ArcDrawDeletedNotes", Text = "삭제한 노트 복구", ClientSize = new Size(440, 300),
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                ShowInTaskbar = false, TopMost = true, StartPosition = FormStartPosition.CenterScreen, Font = font
            })
            {
                var list = new ListBox
                {
                    Name = "DeletedNotesList", AccessibleName = "삭제한 노트 목록",
                    Bounds = new Rectangle(16, 16, 408, 228), IntegralHeight = false, HorizontalScrollbar = true
                };
                foreach (XElement entry in entries)
                {
                    string name = (string)entry.Attribute("name");
                    if (DateTimeOffset.TryParse((string)entry.Attribute("deletedAt"), CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out DateTimeOffset deletedAt))
                        name += "  ·  " + deletedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                    list.Items.Add(name);
                }
                list.SelectedIndex = 0;

                var restore = new Button
                {
                    Name = "ConfirmNoteRestore", Text = "복구", DialogResult = DialogResult.OK,
                    Bounds = new Rectangle(228, 256, 92, 30)
                };
                var cancel = new Button
                {
                    Text = "닫기", DialogResult = DialogResult.Cancel, Bounds = new Rectangle(332, 256, 92, 30)
                };
                dialog.Controls.AddRange(new Control[] { list, restore, cancel });
                dialog.AcceptButton = restore;
                dialog.CancelButton = cancel;
                dialog.ActiveControl = list;
                return dialog.ShowDialog(root.FormCollection) == DialogResult.OK ? list.SelectedIndex : -1;
            }
        }

        private bool RestoreDeletedNote(XElement entry)
        {
            WhiteboardLayerForm note = null;
            bool saved = false;
            try
            {
                note = CreateStoredNote(WhiteboardLayerForm.ReadStoredNote(entry));
                saved = SaveNotes(noteToRestore: note);
            }
            catch (Exception error) when (error is InvalidDataException || error is FormatException ||
                error is OverflowException || error is ArgumentException || error is InvalidOperationException ||
                error is COMException || error is System.ComponentModel.Win32Exception)
            {
                ShowNoteArchiveWarning("저장된 노트를 열 수 없습니다. 복구 목록에 보존했습니다.");
                return false;
            }
            finally
            {
                if (!saved && note != null) note.CloseAndDispose();
            }
            if (!saved)
            {
                ShowNoteArchiveWarning("노트를 복구하지 못했습니다. 필기를 마치고 저장 폴더를 확인한 뒤 다시 시도해 주세요.");
                return false;
            }
            notes.Add(note);
            if (root.DrawingSession != null) note.SetInputPassThrough(!root.DrawingSession.DrawingEnabled);
            if (CanShowNotes && !note.UserHidden)
            {
                note.Show(root.FormCollection);
                note.SyncInkTool(root);
            }
            return true;
        }

        private void ShowNoteArchiveWarning(string message)
        {
            MessageBox.Show(root.FormCollection, message, "ArcDraw", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            TextEditingEnded();
        }
    }
}
