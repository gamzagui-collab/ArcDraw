// Whiteboard layers extension for ppInk (https://github.com/pubpub-zz/ppInk)
// Source addition only; the original ppInk license and notices are retained.
// Target: .NET Framework 4.8 / Windows Forms / Microsoft.Ink.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Ink;
using Cursors = System.Windows.Forms.Cursors;

namespace gInk
{
    // This manager deliberately does not modify ppInk's full-screen Ink collection.
    // Each note owns its independent InkOverlay, so moving and erasing a note
    // cannot accidentally change the underlying screen annotations.
    public sealed partial class WhiteboardLayerManager : IDisposable
    {
        private readonly Root root;
        private readonly List<WhiteboardLayerForm> notes = new List<WhiteboardLayerForm>();
        private readonly WhiteboardLayersBar bar;
        private readonly Timer followTimer;
        private WhiteboardLayerForm active;
        private int nextId = 1;
        private bool open;
        private bool disposed;
        private bool waitForTextKeysReleased;
        private sealed class ContentAction { internal WhiteboardLayerForm Note; internal bool Clear, Redo; }
        private readonly Queue<ContentAction> contentActions = new Queue<ContentAction>();
        private bool processingContentActions;
        private Control followedToolbar;
        private bool positioningBar;
        private bool updatingNoteVisibility;
        private bool selectingScreen;
        private int screenDrawingTool=Tools.Hand,screenDrawingFill=Filling.Empty,pendingScreenTool=-1;
        private bool CanShowNotes {get{return open&&(root.DrawingSession==null||!root.DrawingSession.Running||root.DrawingSession.CapsOn);}}

        public WhiteboardLayerManager(Root root)
        {
            this.root = root;
            InitializeNoteContextMenu();
            bar = new WhiteboardLayersBar(this);
            followTimer = new Timer();
            followTimer.Interval = 350;
            followTimer.Tick += delegate
            {
                if (open && !disposed)
                {
                    PositionBar();
                    CompleteScreenToolRestore();
                    RefreshInkTools();
                    bar.UpdateSessionDisplay();
                    foreach (WhiteboardLayerForm note in notes.ToArray()) note.FlushHistorySnapshot();
                    ProcessContentActions();
                }
            };
            InitializeNotePersistence();
        }

        public IList<WhiteboardLayerForm> Notes { get { return notes.AsReadOnly(); } }
        public WhiteboardLayerForm ActiveNote { get { return active != null && !active.UserHidden ? active : null; } }
        public bool HasBusyInk
        {
            get { foreach (WhiteboardLayerForm note in notes.ToArray()) if (note.HasBusyInk) return true; return false; }
        }
        private static bool IsTextTool(int tool){return tool==Tools.txtLeftAligned||tool==Tools.txtRightAligned;}
        internal void DrawingToolSelected(int tool,int fill)
        {
            pendingScreenTool=-1;
            if(active!=null)active.MemoTargetMode=IsTextTool(tool);
            else if(Array.IndexOf(DrawingSettings.PresetTools,tool)>=0||tool==Tools.ClipArt||tool==Tools.PatternLine)
            {screenDrawingTool=tool;screenDrawingFill=fill;}
        }
        public void SelectScreen()
        {
            if(disposed||selectingScreen)return;
            selectingScreen=true;
            try
            {
                WhiteboardLayerForm previous=active;active=null;
                if(previous!=null)
                {
                    bool restore=previous.MemoTargetMode&&IsTextTool(root.ToolSelected)&&!root.EraserMode;
                    // Drop the note target before flushing IME/focus events so
                    // an owned popup cannot reactivate the old memo mid-switch.
                    previous.FinishEditing(true);
                    if(restore&&root.FormCollection!=null)
                    {
                        // CursorDown already owns a native stroke. Set its tool
                        // now, and defer toolbar/Ink synchronization until idle.
                        root.ToolSelected=screenDrawingTool;root.FilledSelected=screenDrawingFill;pendingScreenTool=screenDrawingTool;
                        CompleteScreenToolRestore();
                    }
                    bar.UpdateSelection();NotesChanged();
                }
                NotifyDrawingTarget();
            }
            finally {selectingScreen=false;}
        }
        private void CompleteScreenToolRestore()
        {
            FormCollection canvas=root.FormCollection;
            if(pendingScreenTool<0||canvas==null||canvas.IsDisposed)return;
            if(root.FingerInAction||canvas.ScreenContentBusy||canvas.IC.CollectingInk||HasBusyInk)return;
            int tool=pendingScreenTool;pendingScreenTool=-1;
            if(active==null&&root.ToolSelected==tool)canvas.SelectTool(tool,screenDrawingFill);
        }
        private void NotifyDrawingTarget(){if(root.DrawingSession!=null)root.DrawingSession.DrawingTargetChanged();}
        public bool HandleSelectedContent(bool clear, bool redo = false)
        {
            WhiteboardLayerForm note = ActiveNote;
            if (note == null) return false;
            QueueContentAction(note, clear, redo);
            return true;
        }
        public void QueueContentAction(WhiteboardLayerForm note, bool clear, bool redo = false)
        {
            if (disposed || !notes.Contains(note)) return;
            contentActions.Enqueue(new ContentAction { Note=note, Clear=clear, Redo=redo });
            ProcessContentActions();
        }
        private void ProcessContentActions()
        {
            if(processingContentActions)return;
            processingContentActions=true;
            try
            {
            while(contentActions.Count>0)
            {
                ContentAction action=contentActions.Peek();
                if(!notes.Contains(action.Note)||action.Note.IsDisposed) { contentActions.Dequeue(); continue; }
                if(!action.Note.ExecuteContentAction(action.Clear,action.Redo))break;
                contentActions.Dequeue();
            }
            }
            finally { processingContentActions=false; }
        }
        public void SetInputPassThrough(bool value)
        {
            // CAPS visibility is temporary; never overwrite each note's own toggle.
            // Showing/hiding owned windows can activate another note synchronously.
            updatingNoteVisibility=true;
            try
            {
                foreach(WhiteboardLayerForm note in notes.ToArray())
                {
                    note.SetInputPassThrough(value);
                    if(value){note.FinishEditing(true);note.SuspendForSession();note.Hide();}
                    else if(CanShowNotes&&!note.UserHidden)
                    {
                        if(note.Owner!=root.FormCollection)note.Owner=root.FormCollection;
                        note.Show();
                    }
                }
            }
            finally {updatingNoteVisibility=false;}
            bar.UpdateSelection();
            if(!value){RefreshInkTools();KeepWindowsOnTop();}
            if(!value&&ActiveNote!=null)ActiveNote.QueueMemoEditor();
        }
        public void UpdateSessionDisplay() { bar.UpdateSessionDisplay(); }
        public void SetToolbarVisible(bool visible) { if(open) { if(visible)bar.Show();else bar.Hide(); } }
        public void ApplyToolbarAppearance(ToolbarTheme theme,int percent) { bar.ApplyAppearance(theme,percent); }
        internal void ToolbarGeometryChanged() { PositionBar(); }
        internal Control ToolbarSurface {get{return bar;}}
        internal Root CurrentRoot { get { return root; } }
        public bool IsEditingText
        {
            get
            {
                foreach (WhiteboardLayerForm note in notes)
                    if (note.IsEditingText) return true;
                return false;
            }
        }

        public bool SuppressKeyboardShortcuts
        {
            get
            {
                if (IsEditingText) return true;
                if (!waitForTextKeysReleased) return false;
                // The key that ended an edit must not immediately trigger ppInk's
                // polled Escape/tool shortcut on the following timer tick.
                for (int key = 8; key < 256; key++)
                    if (((GetAsyncKeyState(key) | GetKeyState(key)) & 0x8000) != 0) return true;
                waitForTextKeysReleased = false;
                return false;
            }
        }

        internal void TextEditingEnded() { waitForTextKeysReleased = true; }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")]
        private static extern short GetKeyState(int key);

        public void Show()
        {
            if (disposed || root.FormCollection == null || !root.FormCollection.Visible) return;
            open = true;
            Control toolbar=root.FormCollection.gpButtons;
            if(followedToolbar!=toolbar)
            {
                if(followedToolbar!=null){followedToolbar.LocationChanged-=ToolbarPositionChanged;followedToolbar.SizeChanged-=ToolbarPositionChanged;}
                followedToolbar=toolbar;toolbar.LocationChanged+=ToolbarPositionChanged;toolbar.SizeChanged+=ToolbarPositionChanged;
            }
            // Keep the card above its canvas when Ink restores drawing focus.
            // Rebind as well when StopInk/ReinitForms replaced the host window.
            if(bar.Owner!=root.FormCollection)bar.Owner=root.FormCollection;
            bar.Show();
            updatingNoteVisibility=true;
            try
            {
            foreach (WhiteboardLayerForm note in notes)
            {
                if (CanShowNotes&&!note.UserHidden)
                {
                    if(note.Owner!=root.FormCollection)note.Owner=root.FormCollection;
                    if (note.Owner == null) note.Show(root.FormCollection);
                    else note.Show();
                }
            }
            }
            finally {updatingNoteVisibility=false;}
            PositionBar();
            RefreshInkTools();
            KeepWindowsOnTop();
            followTimer.Start();
        }

        public void Hide()
        {
            if (disposed) return;
            open = false;
            followTimer.Stop();
            ProcessContentActions();
            foreach (WhiteboardLayerForm note in notes)
            {
                note.FinishEditing(true);
                note.SuspendForSession();
                note.Hide();
            }
            bar.Hide();
        }

        public void AddNote()
        {
            if (disposed) return;
            if (!open) Show();
            if (!open) return;
            WhiteboardLayerForm note;
            try
            {
                note = new WhiteboardLayerForm(this, nextId++, DefaultNoteBounds());
            }
            catch (Exception ex)
            {
                MessageBox.Show("노트를 만들 수 없습니다.\n" + ex.Message, "ppInk Whiteboard",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            notes.Add(note);
            note.MemoTargetMode=IsTextTool(root.ToolSelected);
            if(root.DrawingSession!=null)note.SetInputPassThrough(!root.DrawingSession.DrawingEnabled);
            if (CanShowNotes&&note.Owner == null) note.Show(root.FormCollection);
            active = note;
            note.SyncInkTool(root);
            bar.QueueRebuild();
            KeepWindowsOnTop();
            if(note.Visible)note.Activate();
            NotifyDrawingTarget();
            NotesChanged();
        }

        private Rectangle DefaultNoteBounds()
        {
            Rectangle area = Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
            // One third of the previous default width/height, retaining the
            // existing minimum needed by the title, close button and paper.
            int width = Math.Min(area.Width, Math.Max(270, Math.Max(300, area.Width / 2) / 3));
            int height = Math.Min(area.Height, Math.Max(185, Math.Max(210, area.Height / 2) / 3));
            int titleInset = SystemInformation.CaptionHeight + SystemInformation.FrameBorderSize.Height;
            int offset = (notes.Count % 5) * 22;
            int left = Math.Min(area.Right - width, area.Left + offset);
            int top = Math.Min(area.Bottom - height, area.Top + titleInset + offset);
            return new Rectangle(left, top, width, height);
        }

        public void SetActive(WhiteboardLayerForm note)
        {
            if (disposed || selectingScreen || updatingNoteVisibility || note == null || !notes.Contains(note)) return;
            bool selectionChanged = active != note || note.UserHidden;
            if(active!=null&&active!=note)active.FinishEditing(true);
            if(selectionChanged&&root.FormCollection!=null)root.FormCollection.ResetScreenSelectionForNote();
            active = note;
            pendingScreenTool=-1;
            if(IsTextTool(root.ToolSelected))note.MemoTargetMode=true;
            if (note.UserHidden)
            {
                note.UserHidden = false;
                if (CanShowNotes)
                {
                    if (note.Owner == null) note.Show(root.FormCollection);
                    else note.Show();
                }
            }
            if (note.Visible)
            {
                if(note.MemoTargetMode&&!IsTextTool(root.ToolSelected)&&!root.EraserMode)
                    root.FormCollection.SelectTool(Tools.txtLeftAligned,Filling.Empty);
                note.SyncInkTool(root);
                note.BringToFront();
                note.Activate();
            }
            bar.UpdateSelection();
            note.QueueMemoEditor();
            NotifyDrawingTarget();
            if(selectionChanged)NotesChanged();
        }

        public void ToggleVisibility(WhiteboardLayerForm note)
        {
            if (disposed || note == null || !notes.Contains(note)) return;
            note.UserHidden = !note.UserHidden;
            if (note.UserHidden)
            {
                note.FinishEditing(true);
                note.Hide();
            }
            else if (CanShowNotes)
            {
                if (note.Owner == null) note.Show(root.FormCollection);
                else note.Show();
            }
            if (!note.UserHidden) active = note;
            bar.UpdateSelection();
            KeepWindowsOnTop();
            NotifyDrawingTarget();
            NotesChanged();
        }

        public void ToggleActive()
        {
            if (disposed || !open) return;
            if (active == null) AddNote();
            else ToggleVisibility(active);
        }

        public void Remove(WhiteboardLayerForm note)
        {
            if (disposed || note == null || !notes.Remove(note)) return;
            note.CloseAndDispose();
            if (active == note) active = null;
            bar.QueueRebuild();
            KeepWindowsOnTop();
            NotifyDrawingTarget();
            NotesChanged();
        }

        public void RefreshInkTools()
        {
            if (!open || root.FormCollection == null || !root.FormCollection.Visible) return;
            foreach (WhiteboardLayerForm note in notes)
                if (note.Visible) note.SyncInkTool(root);
        }

        private void PositionBar()
        {
            if(positioningBar||disposed)return;
            positioningBar=true;try{PositionBarCore();}finally{positioningBar=false;}
        }
        private void ToolbarPositionChanged(object sender,EventArgs e){PositionBar();}
        private void PositionBarCore()
        {
            if (!open || root.FormCollection == null || !root.FormCollection.Visible) return;
            FormCollection host = root.FormCollection;
            if(root.Docked||(root.DrawingSession!=null&&root.DrawingSession.ToolbarHidden)) {bar.Hide();return;}
            if(!bar.Visible)bar.Show();
            bool vertical=root.DrawingSession!=null&&root.DrawingSession.Appearance.Vertical;
            Rectangle main=host.gpButtons.RectangleToScreen(host.gpButtons.ClientRectangle);
            Rectangle work=Screen.FromRectangle(main).WorkingArea;
            Point[] candidates=vertical
                ?new[]{new Point(main.Left,main.Bottom+4),new Point(main.Left,main.Top-bar.Height-4),new Point(main.Right+4,main.Top),new Point(main.Left-bar.Width-4,main.Top)}
                :new[]{new Point(main.Left-bar.Width-4,main.Top),new Point(main.Right+4,main.Top),new Point(main.Left,main.Bottom+4),new Point(main.Left,main.Top-bar.Height-4)};
            Point position=candidates[0];bool found=false;
            foreach(Point candidate in candidates)if(work.Contains(new Rectangle(candidate,bar.Size))){position=candidate;found=true;break;}
            if(!found)position=new Point(Math.Max(work.Left,Math.Min(position.X,work.Right-bar.Width)),Math.Max(work.Top,Math.Min(position.Y,work.Bottom-bar.Height)));
            if(bar.Location!=position)bar.Location=position;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X,
            int Y, int cx, int cy, uint uFlags);

        private static void RaiseWithoutFocus(Form form)
        {
            if (form != null && form.Visible && form.IsHandleCreated)
            {
                // HWND_TOPMOST + SWP_NOSIZE | SWP_NOMOVE | SWP_NOACTIVATE.
                SetWindowPos(form.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0001u | 0x0002u | 0x0010u);
            }
        }

        private void KeepWindowsOnTop()
        {
            if (!open) return;
            // Do not lift an owned note over the foreground settings dialog.
            if ((root.DrawingSession != null && root.DrawingSession.OptionsOpen) ||
                (root.FormOptions != null && root.FormOptions.Visible)) return;
            foreach (WhiteboardLayerForm note in notes)
                if (note != active) RaiseWithoutFocus(note);
            RaiseWithoutFocus(active);
            RaiseWithoutFocus(bar);
        }

        public void Dispose()
        {
            if (disposed) return;
            noteActions.Close();
            Hide();
            SaveNotes();
            noteSaveTimer.Dispose();Application.ApplicationExit-=SaveNotesOnExit;
            disposed = true;
            followTimer.Dispose();
            if(followedToolbar!=null){followedToolbar.LocationChanged-=ToolbarPositionChanged;followedToolbar.SizeChanged-=ToolbarPositionChanged;}
            foreach (WhiteboardLayerForm note in notes) note.CloseAndDispose();
            notes.Clear();
            bar.Dispose();
            noteActions.Dispose();
        }
    }

    public sealed class WhiteboardLayersBar : Form
    {
        private readonly WhiteboardLayerManager manager;
        private readonly FlowLayoutPanel items;
        private readonly FlowLayoutPanel noteItems;
        private readonly Dictionary<WhiteboardLayerForm,Panel> noteRows=new Dictionary<WhiteboardLayerForm,Panel>();
        private readonly ToolTip tips;
        private bool rebuildPending;
        private Button screenButton;
        private ToolbarTheme theme=new ToolbarTheme();
        private int scalePercent=100;

        public void ApplyAppearance(ToolbarTheme value,int percent) { theme=value;scalePercent=percent;QueueRebuild(); }
        public void UpdateSelection()
        {
            foreach(Control control in noteItems.Controls)
            {
                WhiteboardLayerForm note=control.Tag as WhiteboardLayerForm;
                if(note==null)continue;
                bool selected=note==manager.ActiveNote;
                Color color=selected?theme.Accent:theme.Surface;
                control.BackColor=color;
                foreach(Control child in control.Controls)
                {
                    child.BackColor=color;child.ForeColor=selected?Color.White:theme.Text;
                    if(child.Name=="NoteLayerTitle")
                    {
                        child.Text=(note.UserHidden?"○ ":(selected?"✓ ":""))+note.DisplayName;
                        child.AccessibleName=note.DisplayName+" · 노트 선택";
                    }
                    if(child.Name=="ToggleNoteVisibility")child.Text=note.UserHidden?"□":"●";
                }
            }
            UpdateSessionDisplay();
        }
        public void UpdateSessionDisplay()
        {
            if(screenButton==null||screenButton.IsDisposed)return;
            bool screen=manager.ActiveNote==null;
            string text=screen?"✓ 화면":"화면";
            Color background=screen?theme.Accent:theme.Surface,foreground=screen?Color.White:theme.Text;
            if(screenButton.Text==text&&screenButton.BackColor==background&&screenButton.ForeColor==foreground)return;
            screenButton.Text=text;screenButton.BackColor=background;screenButton.ForeColor=foreground;
        }

        public WhiteboardLayersBar(WhiteboardLayerManager manager)
        {
            this.manager = manager;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.FromArgb(244, 247, 251);
            Height = 46;
            Width = 165;
            Padding = new Padding(4);
            tips = new ToolTip {Active=false};
            tips.Popup+=delegate(object sender,PopupEventArgs e){e.Cancel=true;};
            items = new FlowLayoutPanel();
            items.Dock = DockStyle.Fill;
            items.WrapContents = false;
            items.AutoScroll = false;
            items.FlowDirection = FlowDirection.LeftToRight;
            items.Margin = new Padding(0);
            items.Padding = new Padding(0);
            items.BackColor = BackColor;
            noteItems=new FlowLayoutPanel {Name="WhiteboardNoteList",FlowDirection=FlowDirection.TopDown,WrapContents=true,
                Margin=new Padding(0),Padding=new Padding(0),AutoScroll=false};
            Controls.Add(items);
            Rebuild();
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080 | 0x08000000; // TOOLWINDOW | NOACTIVATE
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if(m.Msg==0x21){m.Result=new IntPtr(3);return;} // Click without taking Ink focus.
            base.WndProc(ref m);
        }

        // Defer replacing toolbar buttons until their Click handlers have returned.
        public void QueueRebuild()
        {
            if (IsDisposed || rebuildPending) return;
            if (!IsHandleCreated)
            {
                Rebuild();
                return;
            }
            rebuildPending = true;
            BeginInvoke(new Action(delegate
            {
                rebuildPending = false;
                if (!IsDisposed) Rebuild();
            }));
        }

        public void Rebuild()
        {
            FormCollection host=manager.CurrentRoot.FormCollection;
            RebuildForWorkArea(host==null?Screen.PrimaryScreen.WorkingArea:Screen.FromControl(host).WorkingArea);
        }

        private void RebuildForWorkArea(Rectangle work)
        {
            BackColor=theme.Background;items.BackColor=BackColor;noteItems.BackColor=BackColor;
            FormCollection host=manager.CurrentRoot.FormCollection;
            int cell=host!=null&&host.btDock.Height>0?host.btDock.Height:46;
            float scale=cell/34f;
            int inner=Math.Max(120,(int)Math.Round(150*scale));
            int chipWidth=inner;
            int hintHeight=Math.Max(18,(int)Math.Round(20*scale));
            // The card can sit beside a tall toolbar. Use the whole work area,
            // and wrap persistent note rows into columns instead of a popup menu.
            int rows=Math.Min(manager.Notes.Count,Math.Max(1,(work.Height-24-hintHeight)/cell-5));
            int neededColumns=rows==0?1:(manager.Notes.Count+rows-1)/rows;
            int columns=Math.Min(neededColumns,Math.Max(1,(work.Width-4)/(inner+4)));
            bool scroll=columns<neededColumns;
            int listWidth=columns*(inner+4)-4;
            int listHeight=rows*cell+(scroll?SystemInformation.HorizontalScrollBarHeight:0);
            items.WrapContents=false;
            items.FlowDirection=FlowDirection.TopDown;
            items.SuspendLayout();
            while (items.Controls.Count > 0)
            {
                Control old = items.Controls[0];
                items.Controls.Remove(old);
                if(old!=noteItems)old.Dispose();
            }

            Button appearance=NewButton("⚙ 설정",listWidth);appearance.Name="DrawingSettings";
            appearance.Click+=delegate { if(manager.CurrentRoot.DrawingSession!=null)manager.CurrentRoot.DrawingSession.ShowAppearanceOptions(); };
            items.Controls.Add(appearance);
            Button add = NewButton("＋ 노트", listWidth);add.Name="AddWhiteboardNote";
            add.Font = new Font("Malgun Gothic", 9f, FontStyle.Bold);
            add.ForeColor = Color.White;
            add.BackColor = theme.Accent;
            add.FlatAppearance.MouseOverBackColor = Color.FromArgb(29, 78, 216);
            add.Click += delegate { manager.AddNote(); };
            tips.SetToolTip(add, "새 화이트보드 레이어 (Ctrl+Shift+B)");
            items.Controls.Add(add);

            Panel arrange=new Panel {Name="ArrangeWhiteboardNotes",Size=new Size(listWidth,cell)};
            Button vertical=NewButton("세로 정렬",listWidth/2);vertical.Name="ArrangeNotesVertical";
            Button horizontal=NewButton("가로 정렬",listWidth-listWidth/2);horizontal.Name="ArrangeNotesHorizontal";horizontal.Left=listWidth/2;
            vertical.Click+=delegate {manager.ArrangeNotes(true);};horizontal.Click+=delegate {manager.ArrangeNotes(false);};
            arrange.Controls.Add(vertical);arrange.Controls.Add(horizontal);items.Controls.Add(arrange);

            screenButton=NewButton("화면",listWidth);screenButton.Name="SelectScreenTarget";
            screenButton.Click+=delegate { manager.SelectScreen(); };
            tips.SetToolTip(screenButton,"노트 선택 해제 · 화면에만 삭제/실행 취소 적용");
            items.Controls.Add(screenButton);

            noteItems.SuspendLayout();
            foreach(WhiteboardLayerForm previous in new List<WhiteboardLayerForm>(noteRows.Keys))if(!manager.Notes.Contains(previous))
            {
                Panel old=noteRows[previous];
                if(manager.NoteContextMenu.Visible&&old.Contains(manager.NoteContextMenu.SourceControl))manager.NoteContextMenu.Close();
                noteItems.Controls.Remove(old);old.Dispose();noteRows.Remove(previous);
            }
            int noteIndex=0;
            foreach (WhiteboardLayerForm note in manager.Notes)
            {
                Panel chip;
                if(!noteRows.TryGetValue(note,out chip)){chip=CreateNoteRow(note);noteRows.Add(note,chip);noteItems.Controls.Add(chip);}
                noteItems.Controls.SetChildIndex(chip,noteIndex++);chip.Size=new Size(chipWidth,cell);chip.Margin=new Padding(0,0,4,0);
                int small=Math.Max(16,(int)(25*scale));
                chip.Controls[0].Bounds=new Rectangle(0,0,chipWidth-2*small,cell);
                chip.Controls[1].Bounds=new Rectangle(chipWidth-2*small,0,small,cell);
                chip.Controls[2].Bounds=new Rectangle(chipWidth-small,0,small,cell);
                foreach(Control child in chip.Controls)ToolbarDesign.SetTheme((Button)child,theme);
            }
            noteItems.AutoScroll=scroll;noteItems.Size=new Size(listWidth,listHeight);noteItems.Visible=rows>0;
            noteItems.ResumeLayout(true);items.Controls.Add(noteItems);
            Button info=NewButton(ArcDrawBranding.ToolbarCredit,listWidth);info.Name="ArcDrawAbout";
            info.Font=new Font("Malgun Gothic",7.5f);
            info.Click+=delegate {ArcDrawBranding.ShowInfo(manager.CurrentRoot.FormCollection);};
            items.Controls.Add(info);
            foreach(Control control in items.Controls)
            {
                if(control==noteItems)continue;
                control.Height=cell;
                foreach(Control child in control.Controls)child.Height=cell;
            }
            Label altHint=new Label {Name="ToolbarAltHint",Text="단축키: Alt",AccessibleName="단축키: Alt 키를 누르고 있으세요",
                Width=listWidth,Height=hintHeight,TextAlign=ContentAlignment.MiddleCenter,ForeColor=theme.Text,BackColor=theme.Background,
                Font=new Font("Malgun Gothic",8f),AutoSize=false};
            altHint.Disposed+=delegate {altHint.Font.Dispose();};
            items.Controls.Add(altHint);
            Width=listWidth+8;Height=5*cell+listHeight+8+hintHeight;
            foreach(Control control in items.Controls)control.Margin=new Padding(0);
            items.ResumeLayout(true);
            UpdateSelection();
            UpdateSessionDisplay();
            manager.ToolbarGeometryChanged();
        }

        private Panel CreateNoteRow(WhiteboardLayerForm note)
        {
            var row=new Panel {Tag=note};
            Button title=NewButton(note.DisplayName,1);title.Name="NoteLayerTitle";title.AutoEllipsis=true;title.Tag=note;
            title.ContextMenuStrip=manager.NoteContextMenu;title.Click+=delegate {manager.SetActive(note);};
            Button toggle=NewButton("●",1);toggle.Name="ToggleNoteVisibility";toggle.AccessibleName="노트 표시/숨김";toggle.Click+=delegate {manager.ToggleVisibility(note);};
            Button close=NewButton("×",1);close.Name="DeleteWhiteboardNote";close.AccessibleName="노트 삭제";close.Click+=delegate {manager.Remove(note);};
            row.Controls.AddRange(new Control[]{title,toggle,close});return row;
        }

        private Button NewButton(string text, int width)
        {
            Button b = new Button();
            b.Text = text;
            b.UseMnemonic=false;
            b.Size = new Size(width, 32);
            b.Margin = new Padding(1, 0, 1, 0);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.Font = new Font("Malgun Gothic", 8.5f, FontStyle.Regular);
            b.Cursor = Cursors.Hand;
            b.BackColor=theme.Surface;b.ForeColor=theme.Text;
            ToolbarDesign.SetTheme(b,theme);
            ToolbarDesign.SetHints(b,()=>b.Name=="CapsDrawingState"?"CAPS":(b.Text=="＋ 노트"?"Ctrl+Shift+B":""));
            return b;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) tips.Dispose();
            base.Dispose(disposing);
        }
    }

    public sealed partial class WhiteboardLayerForm : Form
    {
        private readonly WhiteboardLayerManager manager;
        private readonly Panel titleBar;
        private readonly Label title;
        private readonly Panel paper;
        private readonly Dictionary<ResizeCorner, Panel> grips = new Dictionary<ResizeCorner, Panel>();
        private InkOverlay layerInk;
        private bool closing;
        private bool dragging;
        private ResizeCorner resizing = ResizeCorner.None;
        private Point mouseStart;
        private Rectangle boundsStart;
        private int lastPen = int.MinValue;
        private bool lastEraser;
        private float lastWidth = float.MinValue;
        private Color lastColor = Color.Empty;
        private int lastTransparency = -1;

        private enum ResizeCorner { None, TopLeft, TopRight, BottomLeft, BottomRight }
        public int Number { get; private set; }
        public bool UserHidden { get; set; }
        internal bool IsDrawingSurfaceAt(Point screenPoint)
        {
            return Visible&&!UserHidden&&!IsEditingText&&paper.RectangleToScreen(paper.ClientRectangle).Contains(screenPoint);
        }

        public WhiteboardLayerForm(WhiteboardLayerManager manager, int number, Rectangle bounds)
        {
            this.manager = manager;
            Number = number;
            Text = "노트 " + number;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            MinimumSize = new Size(270, 185);
            BackColor = Color.White;
            Bounds = bounds;

            paper = new NotePaper();
            paper.BackColor = Color.White;
            paper.Dock = DockStyle.Fill;
            paper.SizeChanged+=delegate {ResizeMemoEditor();};
            paper.Cursor = Cursors.Cross;
            InitializeNoteShapes();
            paper.MouseDown += PaperMouseDown;
            paper.Paint += PaperPaint;

            titleBar = new Panel();
            titleBar.Dock = DockStyle.Top;
            titleBar.Height = 34;
            titleBar.BackColor = Color.FromArgb(235, 240, 247);
            titleBar.Cursor = Cursors.SizeAll;
            titleBar.MouseDown += HeaderMouseDown;
            titleBar.MouseMove += HeaderMouseMove;
            titleBar.MouseUp += HeaderMouseUp;

            title = new Label();
            title.AutoSize = false;
            title.Text = "⠿  노트 " + number + "   ·   이동";
            title.Font = new Font("Malgun Gothic", 9f, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(46, 60, 78);
            title.Location = new Point(13, 7);
            title.Size = new Size(172, 23);
            title.Cursor = Cursors.SizeAll;
            title.MouseDown += HeaderMouseDown;
            title.MouseMove += HeaderMouseMove;
            title.MouseUp += HeaderMouseUp;
            titleBar.Controls.Add(title);

            Button close = new Button();
            close.Text = "×";
            close.Size = new Size(32, 27);
            close.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            close.Location = new Point(Width - 39, 3);
            close.FlatStyle = FlatStyle.Flat;
            close.FlatAppearance.BorderSize = 0;
            close.Font = new Font("Segoe UI", 12f);
            close.ForeColor = Color.FromArgb(95, 111, 135);
            close.BackColor = titleBar.BackColor;
            close.Cursor = Cursors.Hand;
            close.Click += delegate { manager.Remove(this); };
            titleBar.Controls.Add(close);
            titleBar.Resize += delegate { close.Left=titleBar.ClientSize.Width-close.Width-7; };

            Controls.Add(paper);
            Controls.Add(titleBar);
            InitializeNoteIdentity();
            AddResizeGrips();
            SizeChanged += delegate { PositionGrips(); };
            // Focus/owned-popup activation is not a target selection. Header,
            // paper, resize grips, list buttons and the switcher select explicitly.
            PositionGrips();

            // Independent ink collector attached only to the paper, never to the title bar.
            layerInk = new InkOverlay(paper.Handle);
            layerInk.CollectionMode = CollectionMode.InkOnly;
            layerInk.EraserMode = InkOverlayEraserMode.StrokeErase;
            // Paint ink first and note text last on the same paper surface.
            // Independent automatic ink repainting could otherwise cover text.
            layerInk.AutoRedraw = false;
            layerInk.DynamicRendering = true;
            layerInk.Stroke += delegate { QueuePaperRefresh(); QueueHistorySnapshot(); };
            layerInk.StrokesDeleted += delegate { QueuePaperRefresh(); QueueHistorySnapshot(); };
            layerInk.Enabled = true;
            snapshotPending=true;
            FlushHistorySnapshot();
            LocationChanged+=delegate {manager.NotesChanged();};
            SizeChanged+=delegate {manager.NotesChanged();};
        }

        private void AddResizeGrips()
        {
            CreateGrip(ResizeCorner.TopLeft, Cursors.SizeNWSE);
            CreateGrip(ResizeCorner.TopRight, Cursors.SizeNESW);
            CreateGrip(ResizeCorner.BottomLeft, Cursors.SizeNESW);
            CreateGrip(ResizeCorner.BottomRight, Cursors.SizeNWSE);
        }

        private void CreateGrip(ResizeCorner corner, System.Windows.Forms.Cursor cursor)
        {
            Panel handle = new Panel();
            handle.Size = new Size(15, 15);
            handle.Cursor = cursor;
            handle.BackColor = Color.FromArgb(184, 196, 214);
            handle.MouseDown += delegate(object sender, MouseEventArgs args)
            {
                if (args.Button != MouseButtons.Left) return;
                manager.SetActive(this);
                resizing = corner;
                mouseStart = System.Windows.Forms.Cursor.Position;
                boundsStart = Bounds;
                handle.Capture = true;
            };
            handle.MouseMove += delegate(object sender, MouseEventArgs args)
            {
                if (resizing != ResizeCorner.None && args.Button == MouseButtons.Left)
                    ResizeFromCorner();
            };
            handle.MouseUp += delegate
            {
                resizing = ResizeCorner.None;
                handle.Capture = false;
            };
            Controls.Add(handle);
            handle.BringToFront();
            grips.Add(corner, handle);
        }

        private void PositionGrips()
        {
            foreach (KeyValuePair<ResizeCorner, Panel> pair in grips)
            {
                int x = (pair.Key == ResizeCorner.TopRight || pair.Key == ResizeCorner.BottomRight)
                    ? ClientSize.Width - pair.Value.Width : 0;
                int y = (pair.Key == ResizeCorner.BottomLeft || pair.Key == ResizeCorner.BottomRight)
                    ? ClientSize.Height - pair.Value.Height : 0;
                pair.Value.Location = new Point(x, y);
                pair.Value.BringToFront();
            }
        }

        private void HeaderMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            manager.SetActive(this);
            dragging = true;
            mouseStart = System.Windows.Forms.Cursor.Position;
            boundsStart = Bounds;
            ((Control)sender).Capture = true;
        }
        private void HeaderMouseMove(object sender, MouseEventArgs e)
        {
            if (!dragging || e.Button != MouseButtons.Left) return;
            Point current = System.Windows.Forms.Cursor.Position;
            Rectangle virtualScreen = SystemInformation.VirtualScreen;
            int x = Math.Max(virtualScreen.Left - Width + 110,
                Math.Min(virtualScreen.Right - 110, boundsStart.Left + current.X - mouseStart.X));
            int y = Math.Max(virtualScreen.Top,
                Math.Min(virtualScreen.Bottom - 34, boundsStart.Top + current.Y - mouseStart.Y));
            Location = new Point(x, y);
        }
        private void HeaderMouseUp(object sender, MouseEventArgs e)
        {
            dragging = false;
            ((Control)sender).Capture = false;
        }

        private void ResizeFromCorner()
        {
            int dx = System.Windows.Forms.Cursor.Position.X - mouseStart.X;
            int dy = System.Windows.Forms.Cursor.Position.Y - mouseStart.Y;
            int left = boundsStart.Left;
            int top = boundsStart.Top;
            int right = boundsStart.Right;
            int bottom = boundsStart.Bottom;
            if (resizing == ResizeCorner.TopLeft || resizing == ResizeCorner.BottomLeft) left += dx;
            else right += dx;
            if (resizing == ResizeCorner.TopLeft || resizing == ResizeCorner.TopRight) top += dy;
            else bottom += dy;
            if (right - left < MinimumSize.Width)
            {
                if (resizing == ResizeCorner.TopLeft || resizing == ResizeCorner.BottomLeft)
                    left = right - MinimumSize.Width;
                else right = left + MinimumSize.Width;
            }
            if (bottom - top < MinimumSize.Height)
            {
                if (resizing == ResizeCorner.TopLeft || resizing == ResizeCorner.TopRight)
                    top = bottom - MinimumSize.Height;
                else bottom = top + MinimumSize.Height;
            }
            Bounds = Rectangle.FromLTRB(left, top, right, bottom);
            paper.Invalidate();
        }

        public void SyncInkTool(Root root)
        {
            if (closing || sessionSuspended || root == null || root.FormCollection == null) return;
            InkOverlay ink;
            if (!BeginInkUse(out ink))
            {
                if (!closing && !IsDisposed) syncPending = true;
                return;
            }
            try
            {
                if(HasCustomNoteWork)
                {
                    // Retry a busy native collector on the existing manager tick,
                    // never in a self-posting loop that can starve mouse-up.
                    if(pendingNotePath!=null&&noteCommitBlocked)noteCommitBlocked=false;
                    return;
                }
                bool canDraw=root.DrawingSession==null||!root.DrawingSession.Running||root.DrawingSession.DrawingEnabled||IsEditingText;
                if(!canDraw)
                {
                    if(ink.CollectingInk) { syncPending=true; return; }
                    if(ink.Enabled)ink.Enabled=false;
                    paper.Cursor=Cursors.Default;
                    return;
                }
                bool wantText = !root.EraserMode &&
                    (MemoTargetMode||root.ToolSelected == Tools.txtLeftAligned || root.ToolSelected == Tools.txtRightAligned);
                if (!SetTextMode(ink, wantText) || textMode || closing) return;
                DrawingAttributes drawing = root.FormCollection.IC.DefaultDrawingAttributes;
                if(!SyncNoteShapeTool(ink,root,drawing))return;
                bool eraser = root.EraserMode;
                if (eraser != lastEraser)
                {
                    ink.EditingMode = eraser ? InkOverlayEditingMode.Delete : InkOverlayEditingMode.Ink;
                    lastEraser = eraser;
                }
                if (root.CurrentPen != lastPen || drawing.Width != lastWidth ||
                    drawing.Color != lastColor || drawing.Transparency != lastTransparency ||
                    drawing.PenTip!=ink.DefaultDrawingAttributes.PenTip || drawing.Height!=ink.DefaultDrawingAttributes.Height ||
                    root.LineStyleToString(drawing.ExtendedProperties)!=root.LineStyleToString(ink.DefaultDrawingAttributes.ExtendedProperties))
                {
                    if (!ink.CollectingInk && !closing)
                    {
                        ink.DefaultDrawingAttributes = drawing.Clone();
                        CacheNotePen(drawing,root);
                        lastPen = root.CurrentPen;
                        lastWidth = drawing.Width;
                        lastColor = drawing.Color;
                        lastTransparency = drawing.Transparency;
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // A stroke can still be finishing; retry on the next sync tick.
            }
            finally { EndInkUse(); }
        }

        public void CloseAndDispose()
        {
            RequestClose();
        }

        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080 | 0x08000000; // TOOLWINDOW | NOACTIVATE
                return cp;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!closing && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                manager.Remove(this);
                return;
            }
            base.OnFormClosing(e);
        }
    }
}
