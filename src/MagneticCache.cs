using System;
using System.Collections.Generic;
using System.Drawing;
using Microsoft.Ink;

namespace gInk
{
    public partial class FormCollection
    {
        private sealed class MagneticCandidate
        {
            internal Stroke Stroke;
            internal Rectangle Bounds;
            internal bool Deleted;
        }
        private List<MagneticCandidate> magneticCandidates;
        private List<Stroke> magneticTextCandidates;
        private int magneticCandidateCount;
        // Diagnostics for the focused performance check; no persistent logging.
        private long magneticNativeQueries, magneticSnapshots;

        private bool CanCacheMagneticGeometry()
        {
            int tool=Root.ToolSelected;
            return Root.FingerInAction && !Root.PanMode && !Root.LassoMode && !Root.EraserMode &&
                (tool==Tools.Line || tool==Tools.Poly || tool==Tools.Rect || tool==Tools.Oval ||
                 tool==Tools.StartArrow || tool==Tools.EndArrow || tool==Tools.ClipArt);
        }
        private void ClearMagneticCache()
        {
            magneticCandidates=null; magneticTextCandidates=null;
        }
        private void EnsureMagneticCache()
        {
            Strokes strokes=IC.Ink.Strokes;
            int count=strokes.Count;
            if(magneticCandidates!=null && magneticCandidateCount==count)return;
            var candidates=new List<MagneticCandidate>(count);
            var texts=new List<Stroke>();
            for(int i=0;i<count;i++)
            {
                Stroke stroke=strokes[i];
                bool deleted=stroke.ExtendedProperties.Contains(Root.ISDELETION_GUID);
                candidates.Add(new MagneticCandidate {Stroke=stroke, Deleted=deleted,
                    Bounds=deleted?Rectangle.Empty:stroke.GetBoundingBox()});
                if(stroke.ExtendedProperties.Contains(Root.TEXTWIDTH_GUID))texts.Add(stroke);
            }
            magneticCandidates=candidates; magneticTextCandidates=texts;
            magneticCandidateCount=count; magneticSnapshots++;
        }
        private float NearestCachedMagneticStroke(Point point,out Stroke nearest,out float position)
        {
            EnsureMagneticCache();
            double limit=Root.PixelToHiMetric(Root.MinMagneticRadius());
            float distance=(float)limit;
            nearest=null; position=0;
            // Preserve the original reverse order, strict tie rule and exclusion
            // of the last, in-progress stroke. Bounds only reject impossible hits.
            for(int i=magneticCandidateCount-2;i>=0;i--)
            {
                MagneticCandidate candidate=magneticCandidates[i];
                if(candidate.Deleted)continue;
                Rectangle r=candidate.Bounds;
                double dx=Math.Max(0,Math.Max((double)r.Left-point.X,(double)point.X-r.Right));
                double dy=Math.Max(0,Math.Max((double)r.Top-point.Y,(double)point.Y-r.Bottom));
                if(dx*dx+dy*dy>(double)distance*distance)continue;
                float nextDistance;
                float nextPosition=candidate.Stroke.NearestPoint(point,out nextDistance);
                magneticNativeQueries++;
                if(nextDistance<distance)
                { distance=nextDistance;nearest=candidate.Stroke;position=nextPosition; }
            }
            return nearest==null?float.MaxValue:distance;
        }
        private IEnumerable<Stroke> MagneticTextStrokes()
        {
            if(CanCacheMagneticGeometry())
            {
                EnsureMagneticCache();
                foreach(Stroke stroke in magneticTextCandidates)yield return stroke;
            }
            else foreach(Stroke stroke in IC.Ink.Strokes)yield return stroke;
        }
    }
}
