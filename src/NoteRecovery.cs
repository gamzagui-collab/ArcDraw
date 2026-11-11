using System;
using System.Collections.Generic;
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
                List<XElement> entries = deletedNotes.Elements("Note").Reverse().ToList();
                int selected = ChooseDeletedNote(entries);
                if (selected >= 0 && !disposed) RestoreDeletedNote(entries[selected]);
            }
            finally
            {
                changingNoteArchive = false;
                TextEditingEnded();
            }
            if (!disposed)
            {
                bar.QueueRebuild();
                KeepWindowsOnTop();
            }
        }

        private int ChooseDeletedNote(List<XElement> entries)
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
                var purge = new Button
                {
                    Name = "PurgeDeletedNote", Text = "영구 삭제",
                    Bounds = new Rectangle(16, 256, 96, 30)
                };
                purge.Click += delegate
                {
                    int index = list.SelectedIndex;
                    if (index < 0 || index >= entries.Count) return;
                    if (!PurgeDeletedNote(entries[index], dialog)) return;

                    entries.RemoveAt(index);
                    list.Items.RemoveAt(index);
                    if (entries.Count > 0)
                    {
                        list.SelectedIndex = Math.Min(index, entries.Count - 1);
                        list.Focus();
                    }
                    else
                    {
                        list.Items.Add("복구할 노트가 없습니다.");
                        list.Enabled = false;
                        purge.Enabled = false;
                        restore.Enabled = false;
                        cancel.Focus();
                    }
                };
                dialog.Controls.AddRange(new Control[] { list, purge, restore, cancel });
                dialog.AcceptButton = restore;
                dialog.CancelButton = cancel;
                dialog.ActiveControl = list;
                return dialog.ShowDialog(root.FormCollection) == DialogResult.OK ? list.SelectedIndex : -1;
            }
        }

        private bool PurgeDeletedNote(XElement entry, Form dialog)
        {
            string name = (string)entry.Attribute("name");
            if (!DeletionSafety.Confirm(root, "노트 영구 삭제 확인",
                "“" + name + "” 노트를 영구 삭제할까요?\r\n영구 삭제하면 ArcDraw에서 이 노트를 복구할 수 없습니다.",
                owner: dialog, action: "영구 삭제")) return false;

            bool saved;
            dialog.Enabled = false;
            try { saved = SaveNotes(noteToPurge: (int)entry.Attribute("id")); }
            finally { dialog.Enabled = true; }
            if (!saved)
                ShowNoteArchiveWarning("노트를 영구 삭제하지 못했습니다. 저장 폴더를 확인한 뒤 다시 시도해 주세요.", dialog);
            return saved;
        }

        private void RestoreDeletedNote(XElement entry)
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
                return;
            }
            finally
            {
                if (!saved && note != null) note.CloseAndDispose();
            }
            if (!saved)
            {
                ShowNoteArchiveWarning("노트를 복구하지 못했습니다. 필기를 마치고 저장 폴더를 확인한 뒤 다시 시도해 주세요.");
                return;
            }
            notes.Add(note);
            if (root.DrawingSession != null) note.SetInputPassThrough(!root.DrawingSession.DrawingEnabled);
            if (CanShowNotes && !note.UserHidden)
            {
                note.Show(root.FormCollection);
                note.SyncInkTool(root);
            }
        }

        private void ShowNoteArchiveWarning(string message, IWin32Window owner = null)
        {
            MessageBox.Show(owner ?? root.FormCollection, message, "ArcDraw", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            TextEditingEnded();
        }
    }
}
