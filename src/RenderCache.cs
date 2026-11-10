using System;
using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.Ink;

namespace gInk
{
    public partial class FormDisplay
    {
        private bool toolbarMovePending, renderingToolbarMove, toolbarUnderlayValid;
        private IntPtr toolbarUnderlayDc, toolbarUnderlayBitmap, toolbarUnderlayPrevious;
        private Size toolbarUnderlayCapacity;
        private Rectangle toolbarUnderlayBounds;
        private bool handFrameValid;
        private int handStrokeId, handPackets;
        private Rectangle handFrameBounds;
        // Local diagnostics for the focused renderer check, not persistent telemetry.
        private long cachedToolbarFrames, skippedHandFrames, handCopiedPixels;

        internal void QueueToolbarMove()
        {
            toolbarMovePending = true;
            if (!CanCacheToolbarMove()) Root.UponAllDrawingUpdate = true;
        }
        private bool CanCacheToolbarMove()
        {
            FormCollection c = Root.FormCollection;
            return c != null && c.Visible && !Root.DirectX && !Root.AutoScroll &&
                !Root.FingerInAction && !c.ScreenContentBusy && !c.IC.CollectingInk &&
                (Root.ToolSelected == Tools.Hand || Root.PointerMode) &&
                !Root.EraserMode && !Root.PanMode && !Root.LassoMode && Root.StrokeHovered == null && !SelectionDrawnPreviously &&
                Root.Snapping == 0 && !Root.UponTakingSnap && !Root.UponSubPanelUpdate &&
                !c.ZoomCapturing && !Root.gpPenWidthVisible && !c.gpSubTools.Visible &&
                !c.SpotLightMode && !c.SpotLightTemp && !MemoSpotLight;
        }
        private void EnsureToolbarUnderlay(Size size)
        {
            if (toolbarUnderlayDc != IntPtr.Zero && size.Width <= toolbarUnderlayCapacity.Width && size.Height <= toolbarUnderlayCapacity.Height) return;
            ReleaseToolbarUnderlay();
            using (var pixels = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb))
                toolbarUnderlayBitmap = pixels.GetHbitmap(Color.FromArgb(0));
            toolbarUnderlayDc = CreateCompatibleDC(canvusDc);
            toolbarUnderlayPrevious = SelectObject(toolbarUnderlayDc, toolbarUnderlayBitmap);
            toolbarUnderlayCapacity = size;
        }
        private void CaptureToolbarUnderlay(Rectangle bounds)
        {
            EnsureToolbarUnderlay(bounds.Size);
            BitBlt(toolbarUnderlayDc, 0, 0, bounds.Width, bounds.Height, canvusDc, bounds.Left, bounds.Top, 0x00CC0020);
            toolbarUnderlayBounds = bounds; toolbarUnderlayValid = true;
        }
        private bool TryRenderToolbarMove()
        {
            if (!toolbarMovePending || Root.UponAllDrawingUpdate || !CanCacheToolbarMove()) return false;
            Rectangle next = Root.FormCollection.gpButtons.Bounds;
            if (next.IsEmpty || !ClientRectangle.Contains(next)) return false;
            renderingToolbarMove = true;
            try
            {
                bool reuse = toolbarUnderlayValid;
                Rectangle previous = toolbarUnderlayBounds;
                if (reuse)
                    BitBlt(canvusDc, previous.Left, previous.Top, previous.Width, previous.Height, toolbarUnderlayDc, 0, 0, 0x00CC0020);
                else { ClearCanvus(); DrawStrokes(); }
                CaptureToolbarUnderlay(next);
                DrawButtons((Root.UponButtonsUpdate & 0x2) != 0);
                if (reuse)
                {
                    // Only these two regions changed; the rest of OutCanvus is current.
                    BitBlt(OutcanvusDc, previous.Left, previous.Top, previous.Width, previous.Height, canvusDc, previous.Left, previous.Top, 0x00CC0020);
                    BitBlt(OutcanvusDc, next.Left, next.Top, next.Width, next.Height, canvusDc, next.Left, next.Top, 0x00CC0020);
                    UpdateFormDisplay(true, true); cachedToolbarFrames++;
                }
                else UpdateFormDisplay(true);
                Root.UponButtonsUpdate = 0; toolbarMovePending = false;
                return true;
            }
            finally { renderingToolbarMove = false; }
        }
        private bool PrepareHandFrame(Stroke stroke)
        {
            var c = Root.FormCollection;
            bool cache = !Root.DirectX && !Root.LassoMode && Root.Snapping == 0 &&
                !c.SpotLightMode && !c.SpotLightTemp && !MemoSpotLight &&
                !c.IC.DefaultDrawingAttributes.ExtendedProperties.Contains(Root.DASHED_LINE_GUID);
            int packets = stroke.PacketCount;
            if (cache && handFrameValid && handStrokeId == stroke.Id && handPackets == packets)
            { skippedHandFrames++; return false; }
            Rectangle bounds = ClientRectangle;
            if (cache)
            {
                Rectangle ink = stroke.GetBoundingBox();
                Point a = ink.Location, b = new Point(ink.Right, ink.Bottom);
                c.IC.Renderer.InkSpaceToPixel(gOneStrokeCanvus, ref a);
                c.IC.Renderer.InkSpaceToPixel(gOneStrokeCanvus, ref b);
                bounds = Rectangle.FromLTRB(Math.Min(a.X,b.X),Math.Min(a.Y,b.Y),Math.Max(a.X,b.X)+1,Math.Max(a.Y,b.Y)+1);
                bounds.Inflate(12, 12); bounds.Intersect(ClientRectangle);
            }
            Rectangle copy = cache && handFrameValid && handStrokeId == stroke.Id
                ? Rectangle.Intersect(ClientRectangle, Rectangle.Union(bounds, handFrameBounds)) : ClientRectangle;
            if (!copy.IsEmpty)
            {
                BitBlt(OutcanvusDc, copy.Left, copy.Top, copy.Width, copy.Height, canvusDc, copy.Left, copy.Top, 0x00CC0020);
                handCopiedPixels += (long)copy.Width * copy.Height;
            }
            DrawOneStroke(gOutCanvus, stroke, c.IC.DefaultDrawingAttributes);
            handFrameValid = cache; handStrokeId = stroke.Id; handPackets = packets; handFrameBounds = bounds;
            return true;
        }
        private void InvalidateRenderCache()
        {
            handFrameValid = false;
            if (!renderingToolbarMove) { toolbarUnderlayValid = false; toolbarMovePending = false; }
        }
        private void ReleaseToolbarUnderlay()
        {
            if (toolbarUnderlayDc != IntPtr.Zero)
            {
                if (toolbarUnderlayPrevious != IntPtr.Zero) SelectObject(toolbarUnderlayDc, toolbarUnderlayPrevious);
                DeleteDC(toolbarUnderlayDc); toolbarUnderlayDc = IntPtr.Zero;
            }
            if (toolbarUnderlayBitmap != IntPtr.Zero) { DeleteObject(toolbarUnderlayBitmap); toolbarUnderlayBitmap = IntPtr.Zero; }
            toolbarUnderlayPrevious = IntPtr.Zero; toolbarUnderlayCapacity = Size.Empty; toolbarUnderlayValid = false;
        }
    }
}
