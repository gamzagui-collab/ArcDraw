using System;
using System.Drawing;
using Microsoft.Ink;

namespace gInk
{
    public sealed partial class WhiteboardLayerForm
    {
        private NoteState retainedSession;
        private int retainedInkCount;
        private bool sessionSuspended;
        internal void SuspendForSession()
        {
            if(closing||!FlushHistorySnapshot())return;
            InkOverlay ink;if(!BeginInkUse(out ink))return;
            try
            {
                if(ink.CollectingInk)return;
                if(historyPosition>=0)retainedSession=history[historyPosition];
                retainedInkCount=ink.Ink.Strokes.Count;
                if(ink.Enabled)ink.Enabled=false;
                sessionSuspended=true;
            }
            finally{EndInkUse();}
        }
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if(!Visible||!sessionSuspended||closing||layerInk==null)return;
            InkOverlay ink;if(!BeginInkUse(out ink))return;
            try
            {
                if(ink.Enabled)ink.Enabled=false;
                if(ink.Handle!=paper.Handle)ink.Handle=paper.Handle;
                // Keep the existing Ink object. Recover from the managed snapshot
                // only if hiding/recreating a window lost its native content.
                if(retainedSession!=null&&ink.Ink.Strokes.Count!=retainedInkCount)
                {
                    using(var restored=new Ink())
                    {
                        if(retainedSession.Ink.Length>0)restored.Load(retainedSession.Ink);
                        ink.Ink.DeleteStrokes();
                        if(retainedSession.Ink.Length>0)using(Strokes strokes=restored.Strokes)ink.Ink.AddStrokesAtRectangle(strokes,strokes.GetBoundingBox());
                    }
                }
                if(retainedSession!=null&&textItems.Count!=retainedSession.Text.Count)
                {textItems.Clear();textItems.AddRange(CopyText(retainedSession.Text));}
                sessionSuspended=false;syncPending=true;repaintPending=true;
            }
            finally{EndInkUse();}
        }
        internal bool ApplyTextFont(Font font)
        {
            FinishEditing(true);
            if(!FlushHistorySnapshot()||HasBusyInk)return false;
            foreach(NoteTextItem item in textItems){item.FontFamily=font.Name;item.FontSize=font.SizeInPoints;item.FontStyle=font.Style;}
            if(textItems.Count>0)QueueHistorySnapshot();
            paper.Invalidate();return true;
        }
    }
}
