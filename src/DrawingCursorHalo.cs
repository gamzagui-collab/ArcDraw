using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace gInk
{
    internal static class DrawingCursorHalo
    {
        private const int Margin = 24;

        internal static bool AppliesTo(Root root)
        {
            return !root.PointerMode && !root.EraserMode && !root.PanMode && !root.LassoMode &&
                root.Snapping == 0 && !root.UponTakingSnap &&
                root.ToolSelected != Tools.txtLeftAligned && root.ToolSelected != Tools.txtRightAligned;
        }

        internal static Cursor Create(Cursor original)
        {
            Point hotspot = original.HotSpot;
            // FromHandle borrows the original cursor. The new cursor owns its
            // own copy of the combined pixels.
            using (Icon borrowed = Icon.FromHandle(original.Handle))
            using (Bitmap glyph = borrowed.ToBitmap())
            {
                Point origin = new Point(Math.Max(0, Margin - hotspot.X), Math.Max(0, Margin - hotspot.Y));
                Point center = new Point(origin.X + hotspot.X, origin.Y + hotspot.Y);
                int width = Math.Max(center.X + Margin, origin.X + glyph.Width);
                int height = Math.Max(center.Y + Margin, origin.Y + glyph.Height);
                using (var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    var circle = new Rectangle(center.X - 11, center.Y - 11, 22, 22);
                    using (var pen = new Pen(Color.FromArgb(38, 0, 200, 255), 10)) graphics.DrawEllipse(pen, circle);
                    using (var pen = new Pen(Color.FromArgb(85, 0, 220, 255), 6)) graphics.DrawEllipse(pen, circle);
                    using (var pen = new Pen(Color.FromArgb(175, 0, 79, 107), 3.5f)) graphics.DrawEllipse(pen, circle);
                    using (var pen = new Pen(Color.FromArgb(255, 50, 234, 255), 1.8f)) graphics.DrawEllipse(pen, circle);
                    graphics.DrawImageUnscaled(glyph, origin);
                    return FormCollection.CreateCursorFromBitmap(bitmap, center.X, center.Y);
                }
            }
        }
    }
}
