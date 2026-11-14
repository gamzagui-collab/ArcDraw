using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace gInk
{
    public partial class FormDisplay
    {
        private bool previewFrameValid, partialUploadDisabled;
        private int previewTool, previewX0, previewY0, previewX, previewY;
        private bool previewDashed;
        private MouseButtons previewButton;
        private Rectangle previewBounds, uploadedBounds;
        private Rectangle? pendingFrameDirty;
        private IntPtr dirtyUploadBuffer;
        private long previewCopiedPixels, skippedPreviewFrames, partialUploads;
        private int partialUploadError;

        private bool TryRenderCustomFrame()
        {
            FormCollection c=Root.FormCollection;
            int tool=Root.ToolSelected;
            if(Root.DirectX || Root.LassoMode || Root.PanMode || Root.EraserMode ||
                Root.Snapping!=0 || Root.UponTakingSnap || c.ZoomCapturing ||
                c.SpotLightMode || c.SpotLightTemp || MemoSpotLight ||
                Root.CursorX0==int.MinValue || Root.CursorY0==int.MinValue ||
                !(tool==Tools.Line || tool==Tools.Poly || tool==Tools.Rect || tool==Tools.Oval ||
                  tool==Tools.StartArrow || tool==Tools.EndArrow || tool==Tools.ClipArt || c.DrawingLockedBrush))
                return false;
            if(previewFrameValid && previewTool==tool && previewX0==Root.CursorX0 && previewY0==Root.CursorY0 &&
                previewX==Root.CursorX && previewY==Root.CursorY && previewButton==c.CurrentMouseButton && previewDashed==c.ScreenDashed)
            { skippedPreviewFrames++; return true; }

            int x0=Root.CursorX0,y0=Root.CursorY0,x=Root.CursorX,y=Root.CursorY;
            Point[] areaOutline=c.GetConstrainedAreaOutline();
            Point? guideEnd=c.GetAreaGuideEnd();
            bool centered=c.CurrentMouseButton==MouseButtons.Right || (int)c.CurrentMouseButton==2;
            if(centered && (tool==Tools.Rect || tool==Tools.Oval || tool==Tools.ClipArt))
            { x0=2*x0-x; y0=2*y0-y; }
            Rectangle next=Rectangle.FromLTRB(Math.Min(x0,x),Math.Min(y0,y),Math.Max(x0,x)+1,Math.Max(y0,y)+1);
            if(areaOutline!=null)
                foreach(Point point in areaOutline)next=Rectangle.Union(next,new Rectangle(point, new Size(1,1)));
            int padding=4+Math.Abs(Root.HiMetricToPixel(c.IC.DefaultDrawingAttributes.Width));
            if(tool==Tools.StartArrow || tool==Tools.EndArrow)padding+=(int)Math.Ceiling(c.ArrowVarLen());
            next.Inflate(padding,padding); next.Intersect(ClientRectangle);
            if(guideEnd.HasValue)
                next=Rectangle.Intersect(ClientRectangle,Rectangle.Union(next,
                    AreaDragAxis.GuideBounds(new Point(Root.CursorX0,Root.CursorY0),guideEnd.Value)));
            if(next.Width<=0 || next.Height<=0)return false;
            Rectangle copy=previewFrameValid?Rectangle.Intersect(ClientRectangle,Rectangle.Union(next,previewBounds)):ClientRectangle;
            BitBlt(OutcanvusDc,copy.Left,copy.Top,copy.Width,copy.Height,canvusDc,copy.Left,copy.Top,0x00CC0020);
            previewCopiedPixels+=(long)copy.Width*copy.Height;
            DrawCustomOnGraphic(gOutCanvus,Root.CursorX0,Root.CursorY0,Root.CursorX,Root.CursorY,areaOutline,guideEnd);
            previewFrameValid=true; previewBounds=next; previewTool=tool;
            previewX0=Root.CursorX0;previewY0=Root.CursorY0;previewX=Root.CursorX;previewY=Root.CursorY;
            previewButton=c.CurrentMouseButton; previewDashed=c.ScreenDashed; handFrameValid=false; pendingFrameDirty=copy;
            UpdateFormDisplay(true,true);
            return true;
        }

        internal void InvalidateAreaPreview() => previewFrameValid = false;

        private bool TryUploadDirtyFrame(IntPtr screenDc)
        {
            if(partialUploadDisabled || !pendingFrameDirty.HasValue || uploadedBounds!=Bounds || !Visible)return false;
            Rectangle r=Rectangle.Intersect(ClientRectangle,pendingFrameDirty.Value);
            if(r.Width<=0 || r.Height<=0 || r==ClientRectangle)return false;
            if(dirtyUploadBuffer==IntPtr.Zero)dirtyUploadBuffer=Marshal.AllocHGlobal(44);
            Marshal.StructureToPtr(new Point(Left,Top),dirtyUploadBuffer,false);
            Marshal.StructureToPtr(Size,IntPtr.Add(dirtyUploadBuffer,8),false);
            Marshal.StructureToPtr(Point.Empty,IntPtr.Add(dirtyUploadBuffer,16),false);
            Marshal.StructureToPtr(new BLENDFUNCTION {SourceConstantAlpha=255,AlphaFormat=AC_SRC_ALPHA},IntPtr.Add(dirtyUploadBuffer,24),false);
            Marshal.StructureToPtr(new DirtyRect {Left=r.Left,Top=r.Top,Right=r.Right,Bottom=r.Bottom},IntPtr.Add(dirtyUploadBuffer,28),false);
            var update=new LayeredUpdateInfo {
                Size=Marshal.SizeOf(typeof(LayeredUpdateInfo)),ScreenDc=screenDc,
                Position=dirtyUploadBuffer,Dimensions=IntPtr.Add(dirtyUploadBuffer,8),SourceDc=OutcanvusDc,
                SourcePosition=IntPtr.Add(dirtyUploadBuffer,16),Blend=IntPtr.Add(dirtyUploadBuffer,24),
                Flags=ULW_ALPHA|8,Dirty=IntPtr.Add(dirtyUploadBuffer,28)
            };
            if(UpdateLayeredWindowIndirect(Handle,ref update)){partialUploads++;return true;}
            // A driver/DPI combination that rejects dirty updates keeps the original
            // full-window path. Avoid retrying a rejected native call every frame.
            partialUploadError=Marshal.GetLastWin32Error(); partialUploadDisabled=true;
            return false;
        }
        private void ReleaseDirtyRendering()
        {
            if(dirtyUploadBuffer!=IntPtr.Zero){Marshal.FreeHGlobal(dirtyUploadBuffer);dirtyUploadBuffer=IntPtr.Zero;}
            uploadedBounds=Rectangle.Empty;pendingFrameDirty=null;previewFrameValid=false;
            partialUploadDisabled=false;
        }
        [StructLayout(LayoutKind.Sequential)] private struct DirtyRect {internal int Left,Top,Right,Bottom;}
        [StructLayout(LayoutKind.Sequential)] private struct LayeredUpdateInfo
        {
            internal int Size;internal IntPtr ScreenDc,Position,Dimensions,SourceDc,SourcePosition;
            internal int ColorKey;internal IntPtr Blend;internal int Flags;internal IntPtr Dirty;
        }
        [DllImport("user32.dll",SetLastError=true)] private static extern bool UpdateLayeredWindowIndirect(IntPtr window,ref LayeredUpdateInfo update);
    }
}
