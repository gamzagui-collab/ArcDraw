using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Ink;

namespace gInk
{
    public sealed partial class WhiteboardLayerForm
    {
        private sealed class NoteState
        {
            public byte[] Ink;
            public List<NoteTextItem> Text;
        }
        private readonly List<NoteState> history = new List<NoteState>();
        private int historyPosition = -1;
        private bool snapshotPending, snapshotQueued, restoringHistory, passThrough;
        internal bool HasBusyInk
        {
            get
            {
                if(closing||layerInk==null)return false;
                InkOverlay ink;
                if(!BeginInkUse(out ink))return !closing;
                try { return ink.CollectingInk; }
                finally { EndInkUse(); }
            }
        }

        private static List<NoteTextItem> CopyText(IEnumerable<NoteTextItem> items)
        {
            List<NoteTextItem> result = new List<NoteTextItem>();
            foreach (NoteTextItem item in items) result.Add(new NoteTextItem {
                Position=item.Position, Content=item.Content, Color=item.Color, FontFamily=item.FontFamily,
                FontSize=item.FontSize, FontStyle=item.FontStyle, LayoutWidth=item.LayoutWidth });
            return result;
        }

        private void QueueHistorySnapshot()
        {
            if (closing || restoringHistory) return;
            snapshotPending = true;
            if (snapshotQueued || !paper.IsHandleCreated) return;
            snapshotQueued = true;
            paper.BeginInvoke(new Action(delegate {
                snapshotQueued = false;
                if (!closing) FlushHistorySnapshot();
            }));
        }

        internal bool FlushHistorySnapshot()
        {
            if (!snapshotPending) return true;
            InkOverlay ink;
            if (!BeginInkUse(out ink)) return false;
            try
            {
                if (ink.CollectingInk) return false;
                byte[] data;
                using (Strokes strokes=ink.Ink.Strokes)
                    data=strokes.Count==0 ? new byte[0] : ink.Ink.Save();
                while(history.Count>historyPosition+1) history.RemoveAt(history.Count-1);
                history.Add(new NoteState { Ink=data, Text=CopyText(textItems) });
                historyPosition=history.Count-1;
                // Managed snapshots contain no Ink COM objects or native HWNDs.
                // Bound undo storage per note to 100 completed actions.
                if(history.Count>101) { history.RemoveAt(0); historyPosition--; }
                snapshotPending=false;
                return true;
            }
            finally { EndInkUse(); }
        }

        internal bool ExecuteContentAction(bool clear, bool redo = false)
        {
            if (closing || IsEditingText || !FlushHistorySnapshot()) return false;
            if (historyPosition<0) { snapshotPending=true; if(!FlushHistorySnapshot())return false; }
            InkOverlay ink;
            if (!BeginInkUse(out ink)) return false;
            try
            {
                if (ink.CollectingInk) return false;
                int destination=historyPosition+(redo?1:-1);
                if(!clear&&(destination<0||destination>=history.Count))return true;
                // Ink.Load cannot be used on an Ink instance already attached
                // to a collector, even after deleting its strokes. Validate a
                // detached instance before changing any visible data.
                using(Ink restored=new Ink())
                {
                NoteState state=clear?null:history[destination];
                if(state!=null&&state.Ink.Length>0)restored.Load(state.Ink);
                restoringHistory=true;
                bool enabled=ink.Enabled;
                ink.Enabled=false;
                try
                {
                    ink.Ink.DeleteStrokes();
                    textItems.Clear();
                    if(!clear)
                    {
                        if(state.Ink.Length>0)
                            using(Strokes strokes=restored.Strokes)
                                ink.Ink.AddStrokesAtRectangle(strokes,strokes.GetBoundingBox());
                        textItems.AddRange(CopyText(state.Text));
                        historyPosition=destination;
                    }
                }
                finally { if(!closing)ink.Enabled=enabled; restoringHistory=false; }
                if(clear)snapshotPending=true;
                paper.Invalidate();
                return true;
                }
            }
            finally { EndInkUse(); if(clear&&!closing)FlushHistorySnapshot(); }
        }

        internal void SetInputPassThrough(bool value)
        {
            if(closing||passThrough==value)return;
            passThrough=value;
            int style=GetWindowLong(Handle,-20);
            if(value)
            {
                SetWindowLong(Handle,-20,style|0x80000|0x20|0x08000000);
                SetLayeredWindowAttributes(Handle,0,255,2);
            }
            else SetWindowLong(Handle,-20,style&~0x20&~0x08000000);
            SyncInkTool(manager.CurrentRoot);
        }

        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr window,int index);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr window,int index,int value);
        [DllImport("user32.dll")] private static extern bool SetLayeredWindowAttributes(IntPtr window,uint color,byte alpha,uint flags);
    }
}
