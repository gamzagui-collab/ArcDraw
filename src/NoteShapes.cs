using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Microsoft.Ink;

namespace gInk
{
    public sealed partial class WhiteboardLayerForm
    {
        private static readonly Guid NoteToolGuid=new Guid("789e3cd8-0d63-4a8d-9e57-c29eb71c92c3");
        private static readonly Guid NoteFillGuid=new Guid("281617ea-2440-452f-a3cc-249ba91bf892");
        private sealed class NotePath
        {
            internal Point[] Points;internal DrawingAttributes Pen;internal int Tool,Fill,ReplaceId=-1;
        }
        private bool noteCustomMode,noteCustomInk,noteGesture,noteCommitBlocked;
        private int noteTool=Tools.Invalid,noteGestureTool,noteGestureFill,notePolylineId=-1;
        private Point[] notePolyline;
        private Point noteStart,noteEnd;
        private MouseButtons noteGestureButton;
        private DrawingAttributes noteGesturePen;
        private DrawingAttributes noteCurrentPen;
        private int notePreviewWidth;
        private double noteArrowLength,noteGestureArrowLength;
        private readonly List<Point> noteInputPoints=new List<Point>();
        private NotePath pendingNotePath;
        private bool HasCustomNoteWork {get{return noteGesture||pendingNotePath!=null;}}

        private void InitializeNoteShapes()
        {
            paper.MouseDown+=NoteShapeMouseDown;paper.MouseMove+=NoteShapeMouseMove;paper.MouseUp+=NoteShapeMouseUp;
            paper.MouseCaptureChanged+=delegate {if(noteGesture&&!paper.Capture)CancelNoteGesture();};
        }
        private bool SyncNoteShapeTool(InkOverlay ink,Root root,DrawingAttributes pen)
        {
            if(ink.CollectingInk)return false;
            if(root.EraserMode)ResetNotePolyline();
            if(noteTool!=root.ToolSelected){ResetNotePolyline();noteTool=root.ToolSelected;}
            bool shape=Array.IndexOf(DrawingSettings.PresetTools,noteTool)>=0&&noteTool!=Tools.Hand;
            noteCustomMode=!root.EraserMode&&(shape||(noteTool==Tools.Hand&&pen.ExtendedProperties.Contains(Root.DASHED_LINE_GUID)));
            if(ink.Enabled==noteCustomMode)ink.Enabled=!noteCustomMode;
            return true;
        }
        private void ResetNotePolyline(){notePolyline=null;notePolylineId=-1;}
        private void CacheNotePen(DrawingAttributes pen,Root root)
        {
            // A different preset begins a new polyline; never recolor its predecessor.
            if(noteCurrentPen!=null)ResetNotePolyline();
            noteCurrentPen=pen.Clone();notePreviewWidth=root.HiMetricToPixel(pen.Width);
            noteArrowLength=root.ArrowLen*Math.Max(.5,Math.Pow(pen.Width/root.PenWidthNormal,.7));
        }
        private Point OnPaper(Point p){return new Point(Math.Max(0,Math.Min(paper.ClientSize.Width-1,p.X)),Math.Max(0,Math.Min(paper.ClientSize.Height-1,p.Y)));}
        private void NoteShapeMouseDown(object sender,MouseEventArgs e)
        {
            Root root=manager.CurrentRoot;
            if(closing||sessionSuspended||!noteCustomMode||noteCurrentPen==null||textMode||HasCustomNoteWork||root.EraserMode||
                (root.DrawingSession!=null&&(!root.DrawingSession.DrawingEnabled||root.DrawingSession.Editing))||
                (e.Button!=MouseButtons.Left&&e.Button!=MouseButtons.Right))return;
            if(noteTool==Tools.Poly&&e.Button==MouseButtons.Right){ResetNotePolyline();return;}
            noteGestureTool=noteTool;noteGestureFill=noteTool==Tools.Rect||noteTool==Tools.Oval||noteTool==Tools.Poly?root.FilledSelected:Filling.Empty;noteGestureButton=e.Button;
            // Initialize the entire gesture before calls that can pump messages.
            // A fast mouse-up must not be lost inside an Ink COM attribute getter.
            noteGesturePen=noteCurrentPen;noteGestureArrowLength=noteArrowLength;
            noteStart=noteGestureTool==Tools.Poly&&notePolyline!=null?notePolyline[notePolyline.Length-1]:OnPaper(e.Location);
            noteEnd=OnPaper(e.Location);noteInputPoints.Clear();noteInputPoints.Add(noteStart);
            noteGesture=true;paper.Capture=true;
            if(!FlushHistorySnapshot()&&noteGesture){CancelNoteGesture();return;}
            if(noteGesture)paper.Invalidate(PreviewBounds());
        }
        private void NoteShapeMouseMove(object sender,MouseEventArgs e)
        {
            if(!noteGesture)return;
            Point next=OnPaper(e.Location);if(next==noteEnd)return;
            Rectangle old=PreviewBounds();noteEnd=next;
            if(noteGestureTool==Tools.Hand)noteInputPoints.Add(next);
            paper.Invalidate(Rectangle.Union(old,PreviewBounds()));
        }
        private void NoteShapeMouseUp(object sender,MouseEventArgs e)
        {
            if(!noteGesture||e.Button!=noteGestureButton)return;
            noteEnd=OnPaper(e.Location);if(noteGestureTool==Tools.Hand&&noteInputPoints[noteInputPoints.Count-1]!=noteEnd)noteInputPoints.Add(noteEnd);
            Point[] points=NoteGeometry();Rectangle dirty=PreviewBounds();bool moved=false;
            foreach(Point point in points)if(point!=points[0]){moved=true;break;}
            noteCommitBlocked=false;
            if(moved)pendingNotePath=new NotePath {Points=points,Pen=noteGesturePen,Tool=noteGestureTool,Fill=noteGestureFill,
                ReplaceId=noteGestureTool==Tools.Poly?notePolylineId:-1};
            noteGesture=false;noteGesturePen=null;paper.Capture=false;noteInputPoints.Clear();paper.Invalidate(dirty);
            if(pendingNotePath!=null)QueueInkWork();
        }
        private void CancelNoteGesture()
        {
            noteGesture=false;pendingNotePath=null;noteCommitBlocked=false;noteGesturePen=null;noteInputPoints.Clear();ResetNotePolyline();
            if(paper!=null&&!paper.IsDisposed){paper.Capture=false;paper.Invalidate();}
        }
        private Point[] NoteGeometry()
        {
            if(noteGestureTool==Tools.Hand)return noteInputPoints.ToArray();
            Point a=noteStart,b=noteEnd;
            if(noteGestureTool==Tools.Poly)
            {
                var points=notePolyline==null?new List<Point>{a}:new List<Point>(notePolyline);points.Add(b);return points.ToArray();
            }
            if(noteGestureButton==MouseButtons.Right&&(noteGestureTool==Tools.Rect||noteGestureTool==Tools.Oval))a=new Point(2*a.X-b.X,2*a.Y-b.Y);
            if(noteGestureTool==Tools.Rect)return new[]{a,new Point(a.X,b.Y),b,new Point(b.X,a.Y),a};
            if(noteGestureTool==Tools.Oval)
            {
                var points=new Point[73];double cx=(a.X+b.X)/2.0,cy=(a.Y+b.Y)/2.0,rx=Math.Abs(a.X-b.X)/2.0,ry=Math.Abs(a.Y-b.Y)/2.0;
                for(int i=0;i<points.Length;i++){double angle=i*Math.PI*2/(points.Length-1);points[i]=new Point((int)Math.Round(cx+rx*Math.Cos(angle)),(int)Math.Round(cy+ry*Math.Sin(angle)));}
                return points;
            }
            if(noteGestureTool==Tools.StartArrow||noteGestureTool==Tools.EndArrow)
            {
                if((noteGestureTool==Tools.StartArrow)^(noteGestureButton==MouseButtons.Right)){Point swap=a;a=b;b=swap;}
                double angle=Math.Atan2(a.Y-b.Y,a.X-b.X);
                Root root=manager.CurrentRoot;double length=noteGestureArrowLength;
                length=Math.Min(length,Math.Sqrt((a.X-b.X)*(double)(a.X-b.X)+(a.Y-b.Y)*(double)(a.Y-b.Y))*.7);
                Point first=new Point((int)(b.X+Math.Cos(angle+root.ArrowAngle)*length),(int)(b.Y+Math.Sin(angle+root.ArrowAngle)*length));
                Point second=new Point((int)(b.X+Math.Cos(angle-root.ArrowAngle)*length),(int)(b.Y+Math.Sin(angle-root.ArrowAngle)*length));
                return new[]{a,b,first,b,second};
            }
            return new[]{a,b};
        }
        private Rectangle PreviewBounds()
        {
            Point[] points=NoteGeometry();if(points.Length==0)return Rectangle.Empty;
            int l=points[0].X,r=l,t=points[0].Y,b=t;foreach(Point p in points){l=Math.Min(l,p.X);r=Math.Max(r,p.X);t=Math.Min(t,p.Y);b=Math.Max(b,p.Y);}
            Rectangle bounds=Rectangle.FromLTRB(l,t,r+1,b+1);int margin=notePreviewWidth+4;
            bounds.Inflate(margin,margin);
            if(noteGestureFill==Filling.Outside)return paper.ClientRectangle;
            return bounds;
        }
        private void CommitPendingNotePath()
        {
            if(closing||pendingNotePath==null)return;
            InkOverlay ink;if(!BeginInkUse(out ink))return;
            bool committed=false;
            try
            {
                if(ink.CollectingInk){noteCommitBlocked=true;return;}
                NotePath path=pendingNotePath;Point[] points=(Point[])path.Points.Clone();
                using(Graphics g=paper.CreateGraphics())ink.Renderer.PixelToInkSpace(g,ref points);
                if(closing)return;
                Stroke stroke=ink.Ink.CreateStroke(points);stroke.DrawingAttributes=path.Pen.Clone();stroke.DrawingAttributes.FitToCurve=false;
                stroke.ExtendedProperties.Add(NoteToolGuid,path.Tool);stroke.ExtendedProperties.Add(NoteFillGuid,path.Fill);
                noteCustomInk=true;
                if(path.ReplaceId>=0)using(Strokes strokes=ink.Ink.Strokes)
                    foreach(Stroke previous in strokes)if(previous.Id==path.ReplaceId){ink.Ink.DeleteStroke(previous);break;}
                if(path.Tool==Tools.Poly){notePolyline=(Point[])path.Points.Clone();notePolylineId=stroke.Id;}
                pendingNotePath=null;repaintPending=true;committed=true;
            }
            finally{EndInkUse();}
            if(committed){QueueHistorySnapshot();SyncInkTool(manager.CurrentRoot);}
        }
        private void DrawNoteStrokes(Graphics g,InkOverlay ink,Strokes strokes)
        {
            if(!noteCustomInk){ink.Renderer.Draw(g,strokes);return;}
            foreach(Stroke stroke in strokes)
            {
                DrawingAttributes pen=stroke.DrawingAttributes;
                bool custom=stroke.ExtendedProperties.Contains(NoteToolGuid),dashed=pen.ExtendedProperties.Contains(Root.DASHED_LINE_GUID);
                if(custom||dashed)
                {
                    Point[] points=stroke.GetPoints();ink.Renderer.InkSpaceToPixel(g,ref points);
                    int fill=custom?Convert.ToInt32(stroke.ExtendedProperties[NoteFillGuid].Data):Filling.Empty;
                    DrawNotePath(g,points,pen,fill,dashed);
                    if(!dashed)ink.Renderer.Draw(g,stroke);
                }
                else ink.Renderer.Draw(g,stroke);
                if(closing)return;
            }
        }
        private void DrawNotePreview(Graphics g)
        {
            if(!noteGesture||noteGesturePen==null)return;
            DrawNotePath(g,NoteGeometry(),noteGesturePen,noteGestureFill,true);
        }
        private void DrawNotePath(Graphics g,Point[] points,DrawingAttributes attributes,int fill,bool outline)
        {
            if(points.Length<2)return;
            int alpha=255-attributes.Transparency;
            if(points.Length>=3&&fill>=Filling.PenColorFilled&&fill<=Filling.BlackFilled)
            {
                Color color=fill==Filling.WhiteFilled?Color.White:fill==Filling.BlackFilled?Color.Black:attributes.Color;
                using(var brush=new SolidBrush(Color.FromArgb(alpha,color)))
                {
                    if(fill==Filling.Outside)using(var path=new GraphicsPath(FillMode.Alternate)){path.AddRectangle(paper.ClientRectangle);path.AddPolygon(points);g.FillPath(brush,path);}
                    else g.FillPolygon(brush,points);
                }
            }
            if(!outline)return;
            using(var pen=new Pen(Color.FromArgb(alpha,attributes.Color),Math.Max(.5f,manager.CurrentRoot.HiMetricToPixel(attributes.Width))))
            {
                if(attributes.ExtendedProperties.Contains(Root.DASHED_LINE_GUID))
                {DashStyle dash=(DashStyle)Convert.ToInt32(attributes.ExtendedProperties[Root.DASHED_LINE_GUID].Data);if(dash!=DashStyle.Custom)pen.DashStyle=dash;}
                pen.LineJoin=LineJoin.Round;g.SmoothingMode=SmoothingMode.AntiAlias;g.DrawLines(pen,points);
            }
        }
    }
}
