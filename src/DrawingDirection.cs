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
        internal static bool IsAreaTool(int tool) => tool == Tools.Rect || tool == Tools.Oval;

        internal static DrawingDirection ReadForTool(int tool) =>
            tool == Tools.Hand || tool == Tools.Line || tool == Tools.StartArrow || tool == Tools.EndArrow || IsAreaTool(tool)
                ? Read() : DrawingDirection.Free;

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

        internal static double? AreaDragAngle(Point start, Point end, bool finishing = false)
        {
            double dx = end.X - start.X, dy = end.Y - start.Y;
            // Ignore a few pixels of initial pointer jitter before locking the axis.
            double minimum = finishing ? 1 : 16;
            return dx * dx + dy * dy >= minimum ? (double?)Math.Atan2(dy, dx) : null;
        }

        internal static Point[] AreaOutline(int tool, Point start, Point end, DrawingDirection direction, bool centered,
            double? lockedAngle)
        {
            double dx = end.X - start.X, dy = end.Y - start.Y;
            if ((dx == 0 && dy == 0) || (direction == DrawingDirection.Diagonal && !lockedAngle.HasValue))
                return Array.Empty<Point>();
            double length, breadth, x, y, centerX, centerY;
            if (direction == DrawingDirection.Axis)
            {
                double side = Math.Max(Math.Abs(dx), Math.Abs(dy));
                length = breadth = centered ? 2 * side : side;
                x = 1;
                y = 0;
                centerX = start.X + (centered ? 0 : (dx < 0 ? -side : side) / 2);
                centerY = start.Y + (centered ? 0 : (dy < 0 ? -side : side) / 2);
            }
            else
            {
                x = Math.Cos(lockedAngle.Value);
                y = Math.Sin(lockedAngle.Value);
                double along = dx * x + dy * y;
                length = Math.Max(2, Math.Abs(along));
                breadth = Math.Max(2, 2 * Math.Abs(dx * y - dy * x));
                centerX = start.X + (centered ? 0 : x * along / 2);
                centerY = start.Y + (centered ? 0 : y * along / 2);
                if (centered) { length *= 2; breadth *= 2; }
            }

            Point At(double along, double across) => new Point(
                (int)Math.Round(centerX + along * x - across * y, MidpointRounding.AwayFromZero),
                (int)Math.Round(centerY + along * y + across * x, MidpointRounding.AwayFromZero));
            double halfLength = length / 2, halfBreadth = breadth / 2;
            if (tool == Tools.Rect)
                return new[] { At(-halfLength, -halfBreadth), At(halfLength, -halfBreadth),
                    At(halfLength, halfBreadth), At(-halfLength, halfBreadth), At(-halfLength, -halfBreadth) };
            var outline = new Point[73];
            for (int i = 0; i < outline.Length - 1; i++)
            {
                double angle = i * 2 * Math.PI / (outline.Length - 1);
                outline[i] = At(Math.Cos(angle) * halfLength, Math.Sin(angle) * halfBreadth);
            }
            outline[outline.Length - 1] = outline[0];
            return outline;
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
