using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Microsoft.Ink;

namespace gInk
{
    internal enum DrawingDirection
    {
        Free,
        Axis,
        EightWays
    }

    internal static class DrawingDirectionLock
    {
        internal static bool IsAreaTool(int tool) => tool == Tools.Rect || tool == Tools.Oval;
        internal static bool IsLineTool(int tool) =>
            tool == Tools.Hand || tool == Tools.Line || tool == Tools.StartArrow || tool == Tools.EndArrow;
        internal static bool ControlDown => (GetAsyncKeyState(0x11) & 0x8000) != 0;

        internal static DrawingDirection ReadForTool(int tool) =>
            IsAreaTool(tool) ? (Read() == DrawingDirection.Free ? DrawingDirection.Free : DrawingDirection.Axis)
                : IsLineTool(tool) ? Read() : DrawingDirection.Free;

        // A gesture keeps the mode it started with, even if the key is released before mouse-up.
        internal static DrawingDirection Read()
        {
            if ((GetAsyncKeyState(0x10) & 0x8000) != 0) return DrawingDirection.EightWays;
            return DrawingDirection.Free;
        }

        internal static Point Snap(Point origin, Point end, DrawingDirection direction)
        {
            if (direction == DrawingDirection.Free || origin == end) return end;
            int dx = end.X - origin.X, dy = end.Y - origin.Y;
            int ax = Math.Abs(dx), ay = Math.Abs(dy);
            if (Math.Min(ax, ay) <= Math.Max(ax, ay) * (Math.Sqrt(2) - 1))
                return Math.Abs(dx) >= Math.Abs(dy) ? new Point(end.X, origin.Y) : new Point(origin.X, end.Y);

            int distance = (ax + ay + 1) / 2;
            return new Point(origin.X + (dx < 0 ? -distance : distance), origin.Y + (dy < 0 ? -distance : distance));
        }

        internal static Point SnapWithin(Point origin, Point end, DrawingDirection direction, Rectangle bounds)
        {
            Point snapped = Snap(origin, end, direction);
            int dx = snapped.X - origin.X, dy = snapped.Y - origin.Y;
            if (dx == 0 || dy == 0) return snapped;
            int distance = Math.Min(Math.Abs(dx), Math.Min(
                dx < 0 ? origin.X - bounds.Left : bounds.Right - 1 - origin.X,
                dy < 0 ? origin.Y - bounds.Top : bounds.Bottom - 1 - origin.Y));
            return new Point(origin.X + Math.Sign(dx) * distance, origin.Y + Math.Sign(dy) * distance);
        }

        internal static void SetDashed(DrawingAttributes attributes)
        {
            ExtendedProperties properties = attributes.ExtendedProperties;
            if (properties.Contains(Root.DASHED_LINE_GUID)) properties.Remove(Root.DASHED_LINE_GUID);
            properties.Add(Root.DASHED_LINE_GUID, (int)DashStyle.Dash);
        }

        internal static void SetDashed(Stroke stroke)
        {
            DrawingAttributes attributes = stroke.DrawingAttributes.Clone();
            SetDashed(attributes);
            stroke.DrawingAttributes = attributes;
        }

        internal static Point[] AreaOutline(int tool, Point start, Point end, bool centered, Point? axisEnd)
        {
            double dx = end.X - start.X, dy = end.Y - start.Y;
            if (start == end && !axisEnd.HasValue) return Array.Empty<Point>();
            double length, breadth, x, y, centerX, centerY;
            if (!axisEnd.HasValue)
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
                double axisX = axisEnd.Value.X - start.X, axisY = axisEnd.Value.Y - start.Y;
                double initialLength = Math.Sqrt(axisX * axisX + axisY * axisY);
                if (initialLength == 0) return Array.Empty<Point>();
                x = axisX / initialLength;
                y = axisY / initialLength;
                double along = dx * x + dy * y;
                length = Math.Max(2, Math.Abs(along));
                breadth = Math.Max(2, initialLength / 4 + 2 * Math.Abs(dy * x - dx * y));
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

    internal struct AreaDragAxis
    {
        private const int GuidePadding = 7;
        private bool choosing;
        private Point? end;

        internal void SetControl(bool down, Point start, Point pointer)
        {
            if (down == choosing) return;
            choosing = down;
            end = !down && start != pointer ? (Point?)pointer : null;
        }

        internal Point? EndAt(Point pointer) => choosing ? pointer : end;

        internal static Rectangle GuideBounds(Point start, Point end) =>
            Rectangle.FromLTRB(Math.Min(start.X, end.X) - GuidePadding, Math.Min(start.Y, end.Y) - GuidePadding,
                Math.Max(start.X, end.X) + GuidePadding + 1, Math.Max(start.Y, end.Y) + GuidePadding + 1);

        internal static void DrawGuide(Graphics graphics, Point start, Point end)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Color outline = Color.FromArgb(21, 47, 64);
                Color accent = Color.FromArgb(81, 225, 255);
                using (var border = new Pen(outline, 3))
                using (var line = new Pen(accent, 2))
                using (var marker = new SolidBrush(accent))
                {
                    if (start != end)
                    {
                        graphics.DrawLine(border, start, end);
                        graphics.DrawLine(line, start, end);
                    }
                    DrawMarker(start);
                    if (end != start) DrawMarker(end);

                    void DrawMarker(Point point)
                    {
                        const int radius = 4;
                        var bounds = new Rectangle(point.X - radius, point.Y - radius, 2 * radius, 2 * radius);
                        graphics.FillEllipse(marker, bounds);
                        graphics.DrawEllipse(border, bounds);
                    }
                }
            }
            finally { graphics.Restore(state); }
        }
    }
}
