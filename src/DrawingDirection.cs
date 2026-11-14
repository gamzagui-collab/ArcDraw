using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Ink;

namespace gInk
{
    internal enum DrawingDirection
    {
        Free,
        Axis,
        Diagonal
    }

    internal static class DrawingDirectionLock
    {
        // A gesture keeps the mode it started with, even if the key is released before mouse-up.
        internal static DrawingDirection Read()
        {
            if ((GetAsyncKeyState(0x11) & 0x8000) != 0) return DrawingDirection.Diagonal;
            if ((GetAsyncKeyState(0x10) & 0x8000) != 0) return DrawingDirection.Axis;
            return DrawingDirection.Free;
        }

        internal static Point Snap(Point origin, Point end, DrawingDirection direction)
        {
            if (direction == DrawingDirection.Free || origin == end) return end;
            int dx = end.X - origin.X, dy = end.Y - origin.Y;
            if (direction == DrawingDirection.Axis)
                return Math.Abs(dx) >= Math.Abs(dy) ? new Point(end.X, origin.Y) : new Point(origin.X, end.Y);

            int distance = (Math.Abs(dx) + Math.Abs(dy) + 1) / 2;
            return new Point(origin.X + (dx < 0 ? -distance : distance), origin.Y + (dy < 0 ? -distance : distance));
        }

        internal static Point SnapWithin(Point origin, Point end, DrawingDirection direction, Rectangle bounds)
        {
            Point snapped = Snap(origin, end, direction);
            if (direction != DrawingDirection.Diagonal) return snapped;
            int dx = snapped.X - origin.X, dy = snapped.Y - origin.Y;
            int distance = Math.Min(Math.Abs(dx), Math.Min(
                dx < 0 ? origin.X - bounds.Left : bounds.Right - 1 - origin.X,
                dy < 0 ? origin.Y - bounds.Top : bounds.Bottom - 1 - origin.Y));
            return new Point(origin.X + Math.Sign(dx) * distance, origin.Y + Math.Sign(dy) * distance);
        }

        internal static void Straighten(Stroke stroke, Renderer renderer, Graphics graphics, DrawingDirection direction,
            Point end, Rectangle? bounds = null)
        {
            Point[] points = stroke.GetPoints();
            if (points.Length < 2) return;
            Point first = points[0];
            renderer.InkSpaceToPixel(graphics, ref first);
            end = bounds.HasValue ? SnapWithin(first, end, direction, bounds.Value) : Snap(first, end, direction);
            renderer.PixelToInkSpace(graphics, ref end);
            Point start = points[0];
            for (int i = 1; i < points.Length; i++)
            {
                double fraction = i / (double)(points.Length - 1);
                points[i] = new Point(start.X + (int)Math.Round((end.X - start.X) * fraction),
                    start.Y + (int)Math.Round((end.Y - start.Y) * fraction));
            }
            stroke.SetPoints(points);
        }

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int key);
    }
}
