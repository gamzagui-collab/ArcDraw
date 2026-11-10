using System;
using System.Windows.Forms;
using Microsoft.Ink;

namespace gInk
{
    public sealed partial class WhiteboardLayerForm
    {
        private int inkUseDepth;
        private bool closePending;
        private bool inkWorkQueued;
        private bool syncPending;
        private bool repaintPending;

        private bool BeginInkUse(out InkOverlay ink)
        {
            ink = null;
            if (closing || IsDisposed || layerInk == null || inkUseDepth != 0) return false;
            ink = layerInk;
            inkUseDepth++;
            return true;
        }

        private void EndInkUse()
        {
            inkUseDepth--;
            if (inkUseDepth == 0 && (closePending || syncPending || repaintPending)) QueueInkWork();
        }

        private void RequestClose()
        {
            if (closing) return;
            closing = true;
            closePending = true;
            FinishEditing(false);
            Hide();
            if (inkUseDepth == 0) QueueInkWork();
        }

        private void QueueInkWork()
        {
            if (inkWorkQueued || IsDisposed || inkUseDepth != 0) return;
            Control dispatcher = manager.CurrentRoot.FormCollection;
            if (dispatcher == null || dispatcher.IsDisposed || !dispatcher.IsHandleCreated)
            {
                if (closePending) Dispose();
                return;
            }
            inkWorkQueued = true;
            dispatcher.BeginInvoke(new Action(delegate
            {
                inkWorkQueued = false;
                if (IsDisposed) return;
                // A COM call can pump this callback too. Its outer use will
                // schedule us again when it returns; never spin/requeue here.
                if (inkUseDepth != 0) return;
                if (closePending)
                {
                    closePending = false;
                    Close();
                    Dispose();
                    return;
                }
                if (syncPending)
                {
                    syncPending = false;
                    SyncInkTool(manager.CurrentRoot);
                }
                if (repaintPending && !paper.IsDisposed)
                {
                    repaintPending = false;
                    paper.Invalidate();
                }
            }));
        }
    }
}
