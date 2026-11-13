using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Ink;

namespace gInk
{
    public sealed partial class WhiteboardLayerManager
    {
        private const long MaxNotesFileBytes = 128L * 1024 * 1024;
        private XElement deletedNotes = new XElement("DeletedNotes");
        private Timer noteSaveTimer;
        private bool persistenceReady,loadingNotes,savingNotes,notesDirty,storeNotice,badPrimary,badBackup,persistenceBlocked;
        private long notesRevision;
        private DateTime saveAfter,dirtySince;
        private string NotesFile {get{return Path.Combine(Program.RunningFolder,"ArcDraw.notes.xml");}}

        internal void NotesChanged()
        {
            if(!persistenceReady||loadingNotes||disposed)return;
            DateTime now=DateTime.UtcNow;if(!notesDirty)dirtySince=now;
            notesDirty=true;notesRevision++;saveAfter=now.AddMilliseconds(700);
        }

        private void InitializeNotePersistence()
        {
            loadingNotes=true;
            try {LoadNotes();}
            finally {loadingNotes=false;persistenceReady=true;}
            noteSaveTimer=new Timer {Interval=1000};
            noteSaveTimer.Tick+=delegate {if(notesDirty&&(DateTime.UtcNow>=saveAfter||DateTime.UtcNow>=dirtySince.AddSeconds(3)))SaveNotes();};
            noteSaveTimer.Start();Application.ApplicationExit+=SaveNotesOnExit;
        }

        private void SaveNotesOnExit(object sender,EventArgs e)
        {
            if(disposed)return;
            FinishTextEditors();SaveNotes();
        }

        private void StoreNotice(string message)
        {
            if(storeNotice)return;storeNotice=true;
            Control dispatcher=root.FormCollection;
            if(dispatcher!=null&&!dispatcher.IsDisposed&&dispatcher.IsHandleCreated)
                dispatcher.BeginInvoke(new Action(delegate {if(root.trayIcon!=null)root.trayIcon.ShowBalloonTip(6000,"ArcDraw 노트 자동저장",message,ToolTipIcon.Warning);}));
        }

        private void LoadNotes()
        {
            List<WhiteboardLayerForm.StoredNote> stored=null;int savedNext=1,savedActive=0;
            foreach(string path in new[]{NotesFile,NotesFile+".bak"})
            {
                if(!File.Exists(path))continue;
                try
                {
                    if(new FileInfo(path).Length>MaxNotesFileBytes)throw new InvalidDataException("노트 파일 크기를 확인해 주세요.");
                    XDocument document;
                    using(var reader=XmlReader.Create(path,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))
                        document=XDocument.Load(reader,LoadOptions.PreserveWhitespace);
                    XElement data=document.Root;if(data==null||data.Name!="ArcDrawNotes"||(int?)data.Attribute("version")!=1)throw new InvalidDataException("지원하지 않는 노트 파일입니다.");
                    var parsed=new List<WhiteboardLayerForm.StoredNote>();var ids=new HashSet<int>();
                    bool hasHeadline = false;
                    foreach(XElement element in data.Elements("Note"))
                    {
                        var item=WhiteboardLayerForm.ReadStoredNote(element);
                        if (item.IsHeadline && hasHeadline) throw new InvalidDataException("헤드라인이 중복되었습니다.");
                        hasHeadline |= item.IsHeadline;
                        if(!ids.Add(item.Number))throw new InvalidDataException("노트 번호가 중복되었습니다.");
                        parsed.Add(item);
                    }
                    var archive = new XElement(data.Element("DeletedNotes") ?? new XElement("DeletedNotes"));
                    foreach (XElement element in archive.Elements("Note"))
                    {
                        int number = (int?)element.Attribute("id") ?? 0;
                        if (number <= 0 || number == int.MaxValue || !ids.Add(number) ||
                            string.IsNullOrEmpty((string)element.Attribute("name")))
                            throw new InvalidDataException("삭제한 노트의 이름이나 번호를 확인해 주세요.");
                    }
                    savedNext=Math.Max(1,(int?)data.Attribute("nextId")??1);savedActive=(int?)data.Attribute("active")??0;
                    foreach (int number in ids) savedNext = Math.Max(savedNext, number + 1);
                    deletedNotes = archive;
                    stored=parsed;
                    if(path!=NotesFile)StoreNotice("이전 백업에서 노트를 복원했습니다. 읽지 못한 저장본은 보존합니다.");
                    break;
                }
                catch(Exception error) when(error is InvalidDataException||error is IOException||error is UnauthorizedAccessException||error is System.Security.SecurityException||error is XmlException||error is ArgumentException||error is FormatException||error is OverflowException||error is System.Runtime.InteropServices.COMException)
                {
                    if(path==NotesFile)badPrimary=true;else badBackup=true;
                }
            }
            if(stored==null)
            {
                if(badPrimary||badBackup)StoreNotice("노트 파일을 읽지 못했습니다. 원본은 보존했으며 새 노트를 만들기 전에는 덮어쓰지 않습니다.");
                return;
            }
            var created=new List<WhiteboardLayerForm>();
            try
            {
                foreach(var item in stored) created.Add(CreateStoredNote(item));
                notes.AddRange(created);nextId=savedNext;
                foreach(var note in notes){nextId=Math.Max(nextId,note.Number+1);if(note.Number==savedActive&&!note.UserHidden)active=note;}
                bar.QueueRebuild();
            }
            catch
            {
                foreach(var note in created)note.CloseAndDispose();
                notes.Clear();active=null;persistenceBlocked=true;
                StoreNotice("노트 창을 복원하지 못해 자동저장을 중지했습니다. 원본 파일은 보존했습니다. 앱을 다시 시작해 주세요.");
            }
        }

        private WhiteboardLayerForm CreateStoredNote(WhiteboardLayerForm.StoredNote item)
        {
            Rectangle bounds = item.Bounds;
            Rectangle work = Screen.FromRectangle(bounds).WorkingArea;
            if (!work.Contains(new Rectangle(bounds.Left, bounds.Top, Math.Min(80, bounds.Width), 34)))
            {
                bounds.X = Math.Max(work.Left, Math.Min(bounds.X, work.Right - bounds.Width));
                bounds.Y = Math.Max(work.Top, Math.Min(bounds.Y, work.Bottom - 34));
            }

            var note = new WhiteboardLayerForm(this, item.Number, bounds, item.IsHeadline);
            try
            {
                note.RestoreStoredNote(item);
                return note;
            }
            catch
            {
                note.CloseAndDispose();
                throw;
            }
        }

        private bool SaveNotes(WhiteboardLayerForm noteToDelete = null, WhiteboardLayerForm noteToRestore = null,
            int? noteToPurge = null)
        {
            bool archiveChanged = noteToDelete != null || noteToRestore != null || noteToPurge.HasValue;
            if(!persistenceReady||loadingNotes||savingNotes||(!notesDirty&&!archiveChanged)||disposed||persistenceBlocked)return false;
            savingNotes=true;string temporary=NotesFile+".tmp."+System.Diagnostics.Process.GetCurrentProcess().Id;
            string backupTemporary = temporary + ".bak";
            try
            {
                // Committing an editor can leave an Ink snapshot queued. Finish
                // it before capturing the revision for a synchronous move.
                if (archiveChanged)
                    foreach (var note in notes.ToArray())
                        if (!note.FlushHistorySnapshot()) return false;
                long revision=notesRevision;
                int activeNumber = ActiveNote == null || ActiveNote == noteToDelete ? 0 : ActiveNote.Number;
                var data=new XElement("ArcDrawNotes",new XAttribute("version",1),new XAttribute("nextId",nextId),new XAttribute("active",activeNumber));
                var archive = new XElement(deletedNotes);
                foreach(var note in notes.ToArray())
                {
                    XElement element;if(!note.TryStoreNote(out element))return false;
                    if (note == noteToDelete)
                    {
                        element.SetAttributeValue("deletedAt", DateTime.UtcNow);
                        archive.Add(element);
                    }
                    else data.Add(element);
                }
                int? archivedNumber = noteToRestore?.Number ?? noteToPurge;
                if (archivedNumber.HasValue)
                {
                    XElement archived = archive.Elements("Note")
                        .FirstOrDefault(element => (int)element.Attribute("id") == archivedNumber.Value);
                    if (archived == null) return false;
                    if (noteToRestore != null)
                    {
                        if (!noteToRestore.TryStoreNote(out XElement restored)) return false;
                        data.Add(restored);
                    }
                    archived.Remove();
                }
                if(disposed||revision!=notesRevision)return false;
                if (archive.HasElements) data.Add(archive);
                using(var stream=new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None))
                {
                    using(var writer=XmlWriter.Create(stream,new XmlWriterSettings {Encoding=new UTF8Encoding(false),Indent=true,NewLineHandling=NewLineHandling.Entitize,CloseOutput=false}))
                        new XDocument(data).Save(writer);
                    stream.Flush(true);
                    if (stream.Length > MaxNotesFileBytes) throw new InvalidDataException("노트 파일의 저장 한도를 초과했습니다.");
                }
                string suffix=".unreadable_"+DateTime.UtcNow.Ticks;
                if(badBackup&&File.Exists(NotesFile+".bak")){File.Move(NotesFile+".bak",NotesFile+".bak"+suffix);badBackup=false;}
                if(badPrimary&&File.Exists(NotesFile)){File.Move(NotesFile,NotesFile+suffix);badPrimary=false;}
                if (noteToPurge.HasValue) SavePurgedNotes(temporary, backupTemporary);
                else if(File.Exists(NotesFile))File.Replace(temporary,NotesFile,NotesFile+".bak",true);
                else File.Move(temporary,NotesFile);
                if (archive.Parent != null) archive.Remove();
                deletedNotes = archive;
                if(revision==notesRevision)notesDirty=false;storeNotice=false;return true;
            }
            catch(Exception error) when(error is InvalidDataException||error is IOException||error is UnauthorizedAccessException||error is System.Security.SecurityException||error is XmlException||error is ArgumentException||error is System.Runtime.InteropServices.COMException)
            {
                saveAfter=DateTime.UtcNow.AddSeconds(4);dirtySince=saveAfter;
                StoreNotice("노트를 저장하지 못했습니다. 저장 폴더의 쓰기 권한과 여유 공간을 확인해 주세요.");return false;
            }
            finally
            {
                savingNotes=false;
                foreach (string path in new[] { temporary, backupTemporary })
                    try { if (File.Exists(path)) File.Delete(path); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
            }
        }

        private void SavePurgedNotes(string temporary, string backupTemporary)
        {
            string backup = NotesFile + ".bak";
            // Preserve a readable pre-purge snapshot if the store was loaded
            // from its fallback and has no valid primary file.
            if (!File.Exists(NotesFile) && File.Exists(backup))
            {
                CopyNoteFile(backup, backupTemporary);
                File.Move(backupTemporary, NotesFile);
            }

            // Update the backup first so fallback cannot undo a successful purge.
            // If the primary update fails, the old primary still has the note.
            CopyNoteFile(temporary, backupTemporary);
            if (File.Exists(backup)) File.Replace(backupTemporary, backup, null, true);
            else File.Move(backupTemporary, backup);
            if (File.Exists(NotesFile)) File.Replace(temporary, NotesFile, null, true);
            else File.Move(temporary, NotesFile);
        }

        private static void CopyNoteFile(string source, string destination)
        {
            using (FileStream input = File.OpenRead(source))
            using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                input.CopyTo(output);
                output.Flush(true);
            }
        }
    }

    public sealed partial class WhiteboardLayerForm
    {
        internal sealed class StoredNote
        {
            internal int Number;internal string Name,Family;internal float Points;internal FontStyle Style;internal Color Color;
            internal Rectangle Bounds;internal bool Hidden;internal byte[] Ink;internal List<NoteTextItem> Text;
            internal bool IsHeadline;
            internal int BackgroundPercent;
            internal NoteBackgroundShape BackgroundShape;
        }
        private static string Required(XElement value,string name){XAttribute item=value.Attribute(name);if(item==null)throw new InvalidDataException("누락된 노트 속성: "+name);return item.Value;}
        private static int StoredInt(XElement value,string name){return int.Parse(Required(value,name),CultureInfo.InvariantCulture);}
        private static float StoredPoints(XElement value,string name)
        {float result=float.Parse(Required(value,name),CultureInfo.InvariantCulture);if(float.IsNaN(result)||float.IsInfinity(result)||result<6||result>144)throw new InvalidDataException("노트 글자 크기를 확인해 주세요.");return result;}
        private static FontStyle StoredStyle(XElement value,string name)
        {int result=StoredInt(value,name);if((result&~15)!=0)throw new InvalidDataException("노트 글꼴 스타일을 확인해 주세요.");return (FontStyle)result;}
        internal static StoredNote ReadStoredNote(XElement element)
        {
            var result=new StoredNote {Number=StoredInt(element,"id"),Name=Required(element,"name"),Hidden=bool.Parse(Required(element,"hidden")),
                Bounds=new Rectangle(StoredInt(element,"x"),StoredInt(element,"y"),StoredInt(element,"width"),StoredInt(element,"height")),Text=new List<NoteTextItem>()};
            result.IsHeadline = (bool?)element.Attribute("headline") ?? false;
            result.BackgroundPercent = Math.Max(0, Math.Min(100, (int?)element.Attribute("background") ?? 100));
            result.BackgroundShape = DefaultBackgroundShape(result.IsHeadline);
            if (result.IsHeadline && Enum.TryParse((string)element.Attribute("shape"), out NoteBackgroundShape shape) &&
                Enum.IsDefined(typeof(NoteBackgroundShape), shape)) result.BackgroundShape = shape;
            if(result.Number<=0||result.Number==int.MaxValue||result.Name.Length==0||result.Name.Length>40||result.Bounds.Width<1||result.Bounds.Height<1||result.Bounds.Width>32767||result.Bounds.Height>32767||
                Math.Abs((long)result.Bounds.X)>100000||Math.Abs((long)result.Bounds.Y)>100000)throw new InvalidDataException("노트 위치/번호를 확인해 주세요.");
            XElement memo=element.Element("MemoStyle");if(memo==null)throw new InvalidDataException("노트 메모 속성이 없습니다.");
            result.Family=Required(memo,"family");result.Points=StoredPoints(memo,"points");result.Style=StoredStyle(memo,"style");result.Color=Color.FromArgb(StoredInt(memo,"color"));
            using(var font=new Font(result.Family,result.Points,result.Style)){}
            XElement ink=element.Element("Ink");if(ink==null)throw new InvalidDataException("노트 필기 데이터가 없습니다.");
            result.Ink=Convert.FromBase64String(ink.Value);if(result.Ink.Length>0)using(var validated=new Ink())validated.Load(result.Ink);
            foreach(XElement text in element.Elements("Text"))
            {
                XElement content=text.Element("Content");if(content==null)throw new InvalidDataException("노트 텍스트가 없습니다.");
                var item=new NoteTextItem {Position=new Point(StoredInt(text,"x"),StoredInt(text,"y")),LayoutWidth=StoredInt(text,"width"),
                    Color=Color.FromArgb(StoredInt(text,"color")),FontFamily=Required(text,"family"),FontSize=StoredPoints(text,"points"),FontStyle=StoredStyle(text,"style"),
                    IsMemo=bool.Parse(Required(text,"memo")),Content=content.Value};
                if(item.LayoutWidth<1)throw new InvalidDataException("노트 텍스트 폭을 확인해 주세요.");using(var font=item.CreateFont()){}result.Text.Add(item);
            }
            if(result.Text.FindAll(t=>t.IsMemo).Count>1)throw new InvalidDataException("노트 메모가 중복되었습니다.");
            return result;
        }

        internal void RestoreStoredNote(StoredNote data)
        {
            InkOverlay ink;if(!BeginInkUse(out ink))throw new InvalidOperationException("노트가 사용 중입니다.");
            try
            {
                ink.Enabled=false;
                if(data.Ink.Length>0)using(var restored=new Ink())
                {restored.Load(data.Ink);using(Strokes strokes=restored.Strokes)ink.Ink.AddStrokesAtRectangle(strokes,strokes.GetBoundingBox());}
                textItems.Clear();textItems.AddRange(CopyText(data.Text));SetDisplayName(data.Name);UserHidden=data.Hidden;
                RestoreNoteBackground(data.BackgroundPercent, data.BackgroundShape);
                memoFontFamily=data.Family;memoFontPoints=data.Points;memoFontStyle=data.Style;memoColor=data.Color;
                history.Clear();history.Add(new NoteState {Ink=data.Ink,Text=CopyText(textItems)});historyPosition=0;snapshotPending=false;
                retainedSession=history[0];retainedInkCount=ink.Ink.Strokes.Count;sessionSuspended=true;
            }
            finally {EndInkUse();}
        }

        internal bool TryStoreNote(out XElement result)
        {
            result=null;if(closing||IsDisposed||HasBusyInk||!FlushHistorySnapshot()||historyPosition<0)return false;
            var text=CopyText(textItems);
            // Copy a live draft without ending its editor or interrupting IME.
            if(IsEditingText)
            {
                int index=editingText==null?-1:textItems.IndexOf(editingText);NoteTextItem item=index<0?null:text[index];
                string content=textEditor.Text;bool keep=editingMemo?content.Length>0:!string.IsNullOrWhiteSpace(content);
                if(keep)
                {
                    if(item==null){item=new NoteTextItem();text.Add(item);}
                    item.Content=content;item.Position=textEditor.Location;item.IsMemo=editingMemo;item.Color=textEditor.ForeColor;
                    item.FontFamily=textEditor.Font.Name;item.FontSize=textEditor.Font.SizeInPoints;item.FontStyle=textEditor.Font.Style;
                    item.LayoutWidth=Math.Max(1,textEditor.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-2);
                }
                else if(item!=null)text.Remove(item);
            }
            Font liveFont = IsMemoEditor ? editorFont : null;
            float points = liveFont?.SizeInPoints ?? memoFontPoints;
            Color color = LiveMemoColor ?? memoColor;
            result=new XElement("Note",new XAttribute("id",Number),new XAttribute("name",DisplayName),new XAttribute("hidden",UserHidden),
                new XAttribute("x",Left),new XAttribute("y",Top),new XAttribute("width",Width),new XAttribute("height",Height),
                new XElement("MemoStyle",new XAttribute("family",liveFont?.Name ?? memoFontFamily),new XAttribute("points",points.ToString(CultureInfo.InvariantCulture)),new XAttribute("style",(int)(liveFont?.Style ?? memoFontStyle)),new XAttribute("color",color.ToArgb())),
                new XElement("Ink",Convert.ToBase64String(history[historyPosition].Ink)));
            result.SetAttributeValue("background", backgroundPercent);
            if (IsHeadline)
            {
                result.SetAttributeValue("headline", true);
                result.SetAttributeValue("shape", backgroundShape.ToString());
            }
            foreach(var item in text)result.Add(new XElement("Text",new XAttribute("x",item.Position.X),new XAttribute("y",item.Position.Y),new XAttribute("width",item.LayoutWidth),new XAttribute("color",item.Color.ToArgb()),
                new XAttribute("family",item.FontFamily),new XAttribute("points",item.FontSize.ToString(CultureInfo.InvariantCulture)),new XAttribute("style",(int)item.FontStyle),new XAttribute("memo",item.IsMemo),new XElement("Content",item.Content??"")));
            return true;
        }
    }
}
