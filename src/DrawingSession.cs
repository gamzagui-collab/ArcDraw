using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace gInk
{
    // Owns only the additional Caps/Tab/Delete interaction policy. The original
    // collector, its tools, and its screen undo implementation remain in use.
    public sealed class DrawingSession : IDisposable
    {
        private readonly Root root;
        private readonly Timer timer;
        private readonly HookProc hookCallback;
        private readonly HashSet<int> consumed = new HashSet<int>();
        private readonly HashSet<int> heldReservedKeys = new HashSet<int>();
        private readonly Queue<int> pendingScreenActions = new Queue<int>();
        private readonly ToolbarKeyTips keyTips;
        private readonly DrawingModeBorder modeBorder;
        private readonly DrawingTargetSwitcher targetSwitcher;
        private DrawingSizeSidebar sizeSidebar;
        private FormInput sizeEditor;
        private WhiteboardLayerForm sizeNote;
        private bool sizingText;
        private bool targetSwitching;
        private int targetSwitchEpoch;
        private sealed class TargetCommand {internal int Epoch;internal bool Begin,Reverse,Commit;}
        private readonly Queue<TargetCommand> targetCommands=new Queue<TargetCommand>();
        private bool processingTargets,targetCommitQueued,repairTargetHook,targetCancelPending;
        private int modifiers;
        private IntPtr hook;
        private bool capsDown, applying, disposed, escapeRequested;
        private bool? applied;
        private IntPtr previousApplication;
        private int pendingPreset=-1;
        private readonly uint ownProcessId=(uint)Process.GetCurrentProcess().Id;
        public bool Running { get; private set; }
        public bool CapsOn { get; private set; }
        public bool ToolbarHidden { get; private set; }
        public bool OptionsOpen { get; private set; }
        internal bool LeavingDrawing {get{return escapeRequested;}}
        public bool SelectingTarget {get{return targetSwitching||targetSwitcher.IsOpen||targetSwitcher.IsVisible||targetCommands.Count>0;}}
        public ToolbarAppearance Appearance { get; private set; }
        public bool DrawingEnabled { get { return Running && CapsOn && root.FormCollection!=null&&!root.FormCollection.EntryPreparing; } }
        public bool Editing
        {
            get { return SelectingTarget || (root.Whiteboards != null && root.Whiteboards.IsEditingText) ||
                ModalWindowOpen() || OptionsOpen ||
                (root.FormOptions != null && root.FormOptions.Visible); }
        }

        private static bool ModalWindowOpen(Form except = null)
        {
            foreach(Form form in Application.OpenForms)if(form!=except&&form.Modal&&form.Visible)return true;
            return false;
        }

        public void RememberForegroundApplication()
        {
            IntPtr window = GetForegroundWindow();
            if (window != IntPtr.Zero && window != previousApplication && !OwnsWindow(window))
                previousApplication = window;
        }

        private bool OwnsWindow(IntPtr window)
        {
            return window != IntPtr.Zero && GetWindowThreadProcessId(window, out uint pid) != 0 && pid == ownProcessId;
        }

        public DrawingSession(Root root)
        {
            this.root = root;
            keyTips=new ToolbarKeyTips(root);
            modeBorder=new DrawingModeBorder(root);
            targetSwitcher=new DrawingTargetSwitcher(root);
            hookCallback = KeyboardHook;
            timer = new Timer { Interval = 250 };
            timer.Tick += delegate { if(!Running&&!disposed)RememberForegroundApplication();else ApplyMode(); };
            Appearance = new ToolbarAppearance(root);
            RememberForegroundApplication();
            timer.Start();
        }

        public void Start()
        {
            if (disposed) return;
            Running = true;
            escapeRequested=false;
            CapsOn = (GetKeyState(0x14) & 1) != 0;
            capsDown = false;
            modifiers=0;
            for(int key=0xa0;key<=0xa5;key++)if(Down(key))modifiers|=ModifierBit(key);
            if(Down(0x5b))modifiers|=64;if(Down(0x5c))modifiers|=128;
            applied = null;
            ToolbarHidden = false;
            root.Docked=!CapsOn;
            if (hook == IntPtr.Zero)
            {
                hook = SetWindowsHookEx(13, hookCallback, GetModuleHandle(null), 0);
                if (hook == IntPtr.Zero)
                {
                    Running = false;
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(), "Caps Lock 입력 전환을 시작할 수 없습니다.");
                }
            }
            Appearance.Apply();
            timer.Interval = 30;
            timer.Start();
            ApplyMode();
            Post(PrepareDrawingInput);
        }

        internal void QueueDrawingFocus() { Post(PrepareDrawingInput); }

        private void PrepareDrawingInput()
        {
            if(!DrawingEnabled||Editing||root.FingerInAction||root.FormCollection.IsToolbarDragging||root.FormCollection.ScreenContentBusy||root.FormCollection.IC.CollectingInk)return;
            root.FormCollection.ToUnThrough();
            root.FormCollection.ToTopMost();
            FocusEntryWindow();
        }

        internal void FocusEntryWindow()
        {
            if(!Running||!CapsOn)return;
            // A remembered toolbar child otherwise regains focus after ESC/reentry.
            root.FormCollection.ActiveControl=null;
            SetForegroundWindow(root.FormCollection.Handle);
            SetActiveWindow(root.FormCollection.Handle);
            SetFocus(root.FormCollection.Handle);
        }

        public void Stop()
        {
            if(disposed)return;
            Running = false;
            escapeRequested=false;
            targetSwitching=false;targetSwitchEpoch++;targetSwitcher.Cancel();
            targetCommands.Clear();targetCommitQueued=false;repairTargetHook=false;targetCancelPending=false;
            keyTips.Hide();
            sizeSidebar?.Hide();
            root.FormCollection?.HideColorToolbar();
            modeBorder.Update(false);
            timer.Interval = 250; // Remember the external app while the launcher is idle.
            timer.Start();
            consumed.Clear();
            heldReservedKeys.Clear();
            pendingScreenActions.Clear();
            pendingPreset=-1;
            if (hook != IntPtr.Zero) { UnhookWindowsHookEx(hook); hook = IntPtr.Zero; }
            applied = null;
            ToolbarHidden = false;
        }

        private IntPtr KeyboardHook(int code, IntPtr message, IntPtr data)
        {
            if (code >= 0 && Running)
            {
                int key = Marshal.ReadInt32(data);
                int eventFlags=Marshal.ReadInt32(data,8);
                bool down = message.ToInt32() == 0x100 || message.ToInt32() == 0x104;
                bool up = message.ToInt32() == 0x101 || message.ToInt32() == 0x105;
                int bit=ModifierBit(key);
                if(bit!=0) {if(down)modifiers|=bit;else if(up)modifiers&=~bit;}
                bool firstPress=true;
                if(key==9||key==0x1b||key==0x2e||key==0x5a) {if(down)firstPress=heldReservedKeys.Add(key);else if(up)heldReservedKeys.Remove(key);}
                if (key == 0x14)
                {
                    if (down && !capsDown) { if(!CapsOn)RememberForegroundApplication();CapsOn = !CapsOn; Post(ApplyMode); }
                    if (down || up) capsDown = down;
                }
                if(up&&(bit&48)!=0&&(modifiers&48)==0&&targetSwitching)
                {
                    targetSwitching=false;QueueTargetCommit();
                }
                // Always release swallowed keys, even if Caps/modifiers changed
                // before key-up. Nothing is intercepted while Caps is off.
                if (up && consumed.Remove(key)) return new IntPtr(1);
                if (down && consumed.Contains(key)) return new IntPtr(1);
                // The event context remains reliable when a modifier-up was missed.
                bool altHeld=key==9?(eventFlags&0x20)!=0:(modifiers&48)!=0;
                if(key==9&&!altHeld)modifiers&=~48;
                if(down&&firstPress&&DrawingEnabled&&key==9&&altHeld&&(modifiers&(12|192))==0&&
                    root.Snapping==0&&!root.UponTakingSnap&&!OptionsOpen&&!ModalWindowOpen()&&
                    (root.FormOptions==null||!root.FormOptions.Visible))
                {
                    bool begin=!targetSwitching,reverse=(modifiers&3)!=0;
                    if(begin)targetSwitchEpoch++;
                    targetSwitching=true;consumed.Add(key);
                    targetCommands.Enqueue(new TargetCommand {Epoch=targetSwitchEpoch,Begin=begin,Reverse=reverse});
                    Post(ProcessTargetCommands);return new IntPtr(1);
                }
                if(down&&SelectingTarget&&key==0x1b)
                {
                    int epoch=++targetSwitchEpoch;targetSwitching=false;consumed.Add(key);
                    Post(delegate {if(epoch==targetSwitchEpoch)CancelTargetSelection(true);});return new IntPtr(1);
                }
                // In pointer mode, Escape belongs to the foreground app.
                if(down&&firstPress&&key==0x1b&&modifiers==0&&(CapsOn||OwnsWindow(GetForegroundWindow()))&&
                    !OptionsOpen&&!ModalWindowOpen()&&
                    (root.FormOptions==null||!root.FormOptions.Visible)&&root.Snapping==0&&!root.UponTakingSnap)
                {
                    consumed.Add(key);Post(delegate {escapeRequested=true;ApplyMode();});return new IntPtr(1);
                }
                if (down && firstPress && DrawingEnabled && !Editing)
                {
                    // Track modifier messages in order. Querying asynchronous
                    // key state inside a low-level hook can lag the event.
                    bool ctrl = (modifiers&12)!=0, shift = (modifiers&3)!=0;
                    bool other = (modifiers&240)!=0;
                    Action action = null;
                    if (!other && !ctrl && !shift && key == 9) action = ToggleToolbar;
                    if(!other&&ctrl&&shift&&key==0x5a)
                    {
                        WhiteboardLayerForm target=root.Whiteboards==null?null:root.Whiteboards.ActiveNote;
                        action=delegate {RunContentAction(target,false,true);};
                    }
                    if (!other && key == 0x2e)
                    {
                        WhiteboardLayerForm target = root.Whiteboards == null ? null : root.Whiteboards.ActiveNote;
                        bool clear = ctrl;
                        // InkOverlay also recognizes Delete/Cut keystrokes.
                        // Retire Shift+Delete here so it cannot reach that path.
                        action = shift ? (Action)delegate { } : delegate { RunContentAction(target, clear); };
                    }
                    if(action==null && root.Settings!=null)
                    {
                        int preset=root.Settings.MatchShortcut(key,ctrl,alt:(modifiers&48)!=0,shift:shift,win:(modifiers&192)!=0);
                        if(preset>=0)action=delegate {pendingPreset=preset;ApplyMode();};
                    }
                    if (action != null) { consumed.Add(key); Post(action); return new IntPtr(1); }
                }
            }
            return CallNextHookEx(hook, code, message, data);
        }

        private static bool Down(int key) { return (GetAsyncKeyState(key) & 0x8000) != 0; }
        private static int ModifierBit(int key)
        {
            switch(key)
            {
                case 0x10:case 0xa0:return 1;case 0xa1:return 2;
                case 0x11:case 0xa2:return 4;case 0xa3:return 8;
                case 0x12:case 0xa4:return 16;case 0xa5:return 32;
                case 0x5b:return 64;case 0x5c:return 128;default:return 0;
            }
        }
        private void Post(Action action)
        {
            Control dispatch = root.FormCollection;
            if (dispatch == null || dispatch.IsDisposed || !dispatch.IsHandleCreated) return;
            dispatch.BeginInvoke(new Action(delegate { if (Running && !disposed) action(); }));
        }

        public void ApplyMode()
        {
            if (!Running || disposed || applying || root.FormCollection == null || !root.FormCollection.Visible||root.FormCollection.EntryPreparing) return;
            if(escapeRequested)
            {
                applying=true;
                try
                {
                if(OptionsOpen||ModalWindowOpen()||(root.FormOptions!=null&&root.FormOptions.Visible))escapeRequested=false;
                else
                {
                    // Commit the final IME composition before hiding the notes.
                    if(root.Whiteboards!=null)root.Whiteboards.FinishTextEditors();
                    if(root.FingerInAction||root.FormCollection.IsToolbarDragging||root.FormCollection.ScreenContentBusy||
                        root.FormCollection.IC.CollectingInk||(root.Whiteboards!=null&&root.Whiteboards.HasBusyInk))return;
                    escapeRequested=false;root.FormCollection.RetreatAndExit(true);return;
                }
                }
                finally {applying=false;}
            }
            ReconcileTargetKeys();ProcessTargetCommands();
            if(!CapsOn&&applied!=CapsOn&&root.Whiteboards!=null)root.Whiteboards.FinishMemoEditors();
            if(SelectingTarget&&(!CapsOn||OptionsOpen||ModalWindowOpen()||(root.FormOptions!=null&&root.FormOptions.Visible)))CancelTargetSelection(false);
            root.FormCollection.UpdateCompactStatus();
            bool editing=Editing;
            keyTips.Update((modifiers&48)!=0&&!editing&&root.Snapping==0);
            UpdateSizeSidebar(editing);
            RefreshDrawingTarget();
            // Stable mode needs no Ink COM polling or repeated note updates.
            if(applied==CapsOn&&root.PointerMode==!CapsOn&&pendingScreenActions.Count==0&&pendingPreset<0)
            {
                if(!CapsOn)RememberForegroundApplication();
                return;
            }
            applying = true;
            try
            {
                if (Editing || root.FingerInAction || root.FormCollection.IsToolbarDragging || root.FormCollection.ScreenContentBusy || root.FormCollection.IC.CollectingInk || (root.Whiteboards != null && root.Whiteboards.HasBusyInk)) return;
                if(!CapsOn)RememberForegroundApplication();
                bool restoreApplication=!CapsOn&&!root.PointerMode;
                if (CapsOn) { if (root.PointerMode) root.UnPointer(); }
                else { if (!root.PointerMode) root.Pointer(); }
                if (applied != CapsOn)
                {
                    root.FormCollection.SetToolbarFold(!CapsOn);
                    applied = CapsOn;
                    root.FormCollection.RefreshScreenInkCursor();
                    if (root.Whiteboards != null) root.Whiteboards.SetInputPassThrough(!CapsOn);
                    restoreApplication|=!CapsOn;
                }
                if(restoreApplication&&previousApplication!=IntPtr.Zero&&IsWindow(previousApplication))SetForegroundWindow(previousApplication);
                if (root.Whiteboards != null) root.Whiteboards.UpdateSessionDisplay();
                // Wait on the existing timer, not a self-reposting callback:
                // posted-message loops can starve the mouse-up that ends Ink.
                if(!CapsOn){pendingScreenActions.Clear();pendingPreset=-1;}
                if(CapsOn&&pendingPreset>=0)
                {
                    int preset=pendingPreset;pendingPreset=-1;
                    root.Settings.RecallPreset(preset);
                }
                while(CapsOn&&pendingScreenActions.Count>0&&!root.FormCollection.ScreenContentBusy&&!root.FormCollection.IC.CollectingInk)
                {
                    int action=pendingScreenActions.Dequeue();
                    if(action==2)root.RedoInk();else if(action==1)root.FormCollection.ClearScreenOnly();else root.UndoInk();
                }
            }
            finally { applying = false; }
        }

        internal void RefreshSizeSidebar()
        {
            UpdateSizeSidebar(Editing);
        }

        private void UpdateSizeSidebar(bool editing)
        {
            FormCollection host = root.FormCollection;
            FormInput editor = root.FormDisplay?.ScreenTextEditor;
            if (editor != null && (!editor.Visible || editor.IsDisposed)) editor = null;
            WhiteboardLayerForm note = root.Whiteboards?.ActiveNote;
            WhiteboardLayerForm memo = editor == null && note != null && note.Visible && note.IsMemoEditor ? note : null;
            bool text = editor != null || memo != null || (note == null &&
                (root.ToolSelected == Tools.txtLeftAligned || root.ToolSelected == Tools.txtRightAligned));
            bool canSize = editor == null && memo == null ? !editing :
                !OptionsOpen && !SelectingTarget && !ModalWindowOpen(editor) &&
                (root.FormOptions == null || !root.FormOptions.Visible);
            bool toolbarActive = DrawingEnabled && canSize && !root.Docked && !ToolbarHidden &&
                root.Snapping == 0 && !root.UponTakingSnap;
            host?.UpdateColorToolbar(editor, memo, text, toolbarActive);
            bool visible = toolbarActive &&
                !root.PointerMode && !root.EraserMode && !root.PanMode && !root.LassoMode &&
                (text || Array.IndexOf(DrawingSettings.PresetTools, root.ToolSelected) >= 0);
            if (!visible)
            {
                sizeEditor = null;
                sizeNote = null;
                if (sizeSidebar != null && sizeSidebar.Visible)
                {
                    sizeSidebar.Slider.Capture = false;
                    sizeSidebar.Hide();
                }
                return;
            }
            if (sizeSidebar == null || sizeSidebar.IsDisposed)
            {
                sizeSidebar = new DrawingSizeSidebar { Owner = host };
                sizeSidebar.Slider.Dragged += SetSizeFromSlider;
            }
            bool changed = sizeEditor != editor || sizeNote != memo || sizingText != text;
            if (changed) sizeSidebar.Slider.Capture = false;
            sizeEditor = editor;
            sizeNote = memo;
            sizingText = text;
            if (text || changed || !sizeSidebar.Visible) RefreshSizeSlider();
            if (!sizeSidebar.Slider.IsDragging)
            {
                var toolbar = host.gpButtons.RectangleToScreen(host.gpButtons.ClientRectangle);
                var noteToolbar = root.Whiteboards?.ToolbarSurface;
                sizeSidebar.PlaceBesideToolbar(toolbar,
                    noteToolbar != null && noteToolbar.Visible ? noteToolbar.Bounds : System.Drawing.Rectangle.Empty);
            }
            if (!sizeSidebar.Visible) sizeSidebar.Show();
            if (editor != null) sizeSidebar.EnableForTextEditor();
            if (memo != null && changed) sizeSidebar.RaiseWithoutActivation();
        }

        private void SetSizeFromSlider(int value)
        {
            if (sizeEditor != null) sizeEditor.SetLiveTextSize(value);
            else if (sizeNote != null)
            {
                if (sizeNote == root.Whiteboards.ActiveNote) sizeNote.SetLiveMemoSize(value);
            }
            else if (sizingText)
            {
                if (root.Whiteboards?.ActiveNote == null) root.FormCollection.TextSize = value;
            }
            else root.FormCollection.SetPenSizeFromSlider(value);
        }

        internal void RefreshSizeSlider()
        {
            if (sizeSidebar == null || sizeSidebar.IsDisposed || sizeSidebar.Slider.IsDragging) return;
            VerticalSizeSlider slider = sizeSidebar.Slider;
            FormCollection host = root.FormCollection;
            if (sizingText)
            {
                int? memoPoints = sizeNote?.LiveMemoSize;
                if (sizeNote != null && !memoPoints.HasValue) return;
                slider.SetMode(true, sizeEditor?.MaximumTextSize ?? VerticalSizeSlider.TextMaximum,
                    sizeNote == null ? 1 : WhiteboardLayerForm.MinimumMemoPoints);
                slider.Value = memoPoints ?? sizeEditor?.LiveTextSize ?? host.TextSize;
            }
            else
            {
                slider.SetMode(false, VerticalSizeSlider.BrushMaximum);
                slider.Value = host.WorkingPenPixels;
                slider.SampleColor = host.WorkingPenColor;
            }
        }

        internal void RaiseSizeSidebar()
        {
            sizeSidebar?.RaiseWithoutActivation();
        }

        public void ToggleCaps()
        {
            if (!Running || Editing) return;
            // Change the real Windows toggle/LED too, rather than a private
            // boolean that could disagree with the keyboard.
            keybd_event(0x14, 0x3a, 0, UIntPtr.Zero);
            keybd_event(0x14, 0x3a, 2, UIntPtr.Zero);
        }

        private void QueueTargetCommit()
        {
            if(targetCommitQueued)return;
            targetCommitQueued=true;targetCommands.Enqueue(new TargetCommand {Epoch=targetSwitchEpoch,Commit=true});Post(ProcessTargetCommands);
        }
        private void ReconcileTargetKeys()
        {
            // Poll outside the low-level hook, after Windows updates key state.
            if(!Down(9)){heldReservedKeys.Remove(9);consumed.Remove(9);}
            bool alt=Down(0x12)||Down(0xa4)||Down(0xa5);
            if(!alt)
            {
                modifiers&=~48;
                if(SelectingTarget&&!targetCommitQueued)
                {targetSwitching=false;repairTargetHook=true;QueueTargetCommit();}
            }
            if(repairTargetHook&&!SelectingTarget&&!alt&&!Down(9)&&!processingTargets)
            {
                // Windows can silently remove a timed-out hook. Recover our own
                // hook only after an unmatched release, with the gesture finished.
                repairTargetHook=false;
                if(hook!=IntPtr.Zero)UnhookWindowsHookEx(hook);
                hook=SetWindowsHookEx(13,hookCallback,GetModuleHandle(null),0);
                if(hook==IntPtr.Zero)Stop();
            }
        }
        private void ProcessTargetCommands()
        {
            if(processingTargets||targetCommands.Count==0)return;
            processingTargets=true;
            try
            {
                while(targetCommands.Count>0)
                {
                    TargetCommand command=targetCommands.Dequeue();if(command.Epoch!=targetSwitchEpoch)continue;
                    if(!DrawingEnabled||OptionsOpen||ModalWindowOpen())
                    {CancelTargetSelection(false);break;}
                    if(command.Begin&&root.Whiteboards!=null)root.Whiteboards.FinishTextEditors();
                    if(command.Commit)
                    {
                        targetCommitQueued=false;targetSwitching=false;
                        targetSwitcher.Commit();RefreshDrawingTarget();continue;
                    }
                    // Native COM calls can pump a release while opening the list.
                    // Reentrant dispatch only enqueues it; this outer loop commits
                    // after Step has completely finished constructing the window.
                    if(command.Begin&&(root.FingerInAction||root.FormCollection.IsToolbarDragging||root.FormCollection.ScreenContentBusy||
                        root.FormCollection.IC.CollectingInk||(root.Whiteboards!=null&&root.Whiteboards.HasBusyInk)))
                    {CancelTargetSelection(false);break;}
                    if(command.Epoch!=targetSwitchEpoch)continue;
                    if(command.Begin)targetSwitcher.Cancel();keyTips.Hide();targetSwitcher.Step(command.Reverse);
                    if(command.Epoch!=targetSwitchEpoch)targetSwitcher.Cancel();
                }
            }
            finally
            {
                processingTargets=false;
                if(targetCancelPending){targetCancelPending=false;targetSwitcher.Cancel();}
                if(!targetSwitching&&targetCommands.Count==0&&targetCommitQueued)targetCommitQueued=false;
            }
        }
        private void CancelTargetSelection(bool suppressKeys)
        {
            targetSwitching=false;targetSwitchEpoch++;targetCommands.Clear();targetCommitQueued=false;
            if(processingTargets)targetCancelPending=true;else targetSwitcher.Cancel();
            if(suppressKeys&&root.Whiteboards!=null)root.Whiteboards.TextEditingEnded();
            RefreshDrawingTarget();
        }
        internal void NoteLabelChanged(){targetSwitcher.RefreshLabels();}
        internal void DrawingTargetChanged()
        {
            if(SelectingTarget)CancelTargetSelection(false);
            else RefreshDrawingTarget();
        }
        internal void RefreshDrawingTarget()
        {
            bool modal=OptionsOpen||ModalWindowOpen()||(root.FormOptions!=null&&root.FormOptions.Visible);
            modeBorder.Update(DrawingEnabled&&!modal&&root.Snapping==0&&!root.UponTakingSnap);
        }

        public void ToggleToolbar()
        {
            if (!DrawingEnabled || Editing) return;
            root.FormCollection.ToggleToolbarFold();
        }

        private void RunContentAction(WhiteboardLayerForm target, bool clear,bool redo=false)
        {
            if (!DrawingEnabled || Editing) return;
            if (target != null)
            {
                if (!target.IsDisposed && root.Whiteboards != null) root.Whiteboards.QueueContentAction(target, clear,redo);
                return;
            }
            pendingScreenActions.Enqueue(redo?2:(clear?1:0));
        }

        public void ShowAppearanceOptions()
        {
            if (OptionsOpen) return;
            if(SelectingTarget)CancelTargetSelection(false);
            OptionsOpen = true;
            try { root.Settings.ShowOptions(); }
            finally
            {
                OptionsOpen = false;
                // Esc closing this dialog must not also reach the original
                // polled "stop drawing" shortcut on the same key press.
                if(root.Whiteboards!=null)root.Whiteboards.TextEditingEnded();
                ApplyMode();
            }
        }

        public void Dispose()
        {
            if(disposed)return;
            Stop();
            disposed = true;
            keyTips.Dispose();
            modeBorder.Dispose();
            targetSwitcher.Dispose();
            sizeSidebar?.Dispose();
            timer.Dispose();
        }

        private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);
        [DllImport("user32.dll", SetLastError=true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint thread);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr handle);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr handle, int code, IntPtr message, IntPtr data);
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string module);
        [DllImport("user32.dll")] private static extern short GetKeyState(int key);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint pid);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern IntPtr SetActiveWindow(IntPtr window);
        [DllImport("user32.dll")] private static extern IntPtr SetFocus(IntPtr window);
    }

    public partial class FormCollection
    {
        private int screenInputDepth;
        internal bool ScreenContentBusy { get { return screenInputDepth!=0; } }
        private void RunScreenInkWork(Action action)
        {
            screenInputDepth++;
            try {action();}
            finally {screenInputDepth--;}
        }
    }
}
