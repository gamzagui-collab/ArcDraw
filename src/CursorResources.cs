using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace gInk
{
    public partial class FormCollection
    {
        private System.Windows.Forms.Cursor generatedPenTip;
        private System.Windows.Forms.Cursor baseInkCursor;
        private System.Windows.Forms.Cursor appliedInkCursor;
        private System.Windows.Forms.Cursor haloSource;
        private System.Windows.Forms.Cursor haloInkCursor;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr icon);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyCursor(IntPtr cursor);
        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr bitmap);

        private static readonly FieldInfo CursorOwnHandle = typeof(System.Windows.Forms.Cursor)
            .GetField("ownHandle", BindingFlags.NonPublic | BindingFlags.Instance);

        // This project targets .NET Framework 4.8. Cursor(IntPtr) borrows its
        // handle; use the same Framework ownership field as its native loader.
        // Cursor(Stream) loses the colored bitmap on this Framework version.
        private static System.Windows.Forms.Cursor CreateOwnedCursor(Bitmap bitmap, int hotX, int hotY)
        {
            if (CursorOwnHandle == null)
                throw new NotSupportedException("The .NET Framework Cursor ownership field is unavailable.");
            IntPtr iconHandle = bitmap.GetHicon();
            IntPtr cursorHandle = IntPtr.Zero;
            IconInfo info = new IconInfo();
            try
            {
                if (!GetIconInfo(iconHandle, ref info)) throw new InvalidOperationException("GetIconInfo failed.");
                info.fIcon = false;
                info.xHotspot = Math.Max(0, Math.Min(bitmap.Width - 1, hotX));
                info.yHotspot = Math.Max(0, Math.Min(bitmap.Height - 1, hotY));
                cursorHandle = CreateIconIndirect(ref info);
                if (cursorHandle == IntPtr.Zero) throw new InvalidOperationException("CreateIconIndirect failed.");
                System.Windows.Forms.Cursor cursor = new System.Windows.Forms.Cursor(cursorHandle);
                CursorOwnHandle.SetValue(cursor, true);
                cursorHandle = IntPtr.Zero; // Cursor now owns it, including finalization.
                cursor.Tag = 2;
                return cursor;
            }
            finally
            {
                if (cursorHandle != IntPtr.Zero) DestroyCursor(cursorHandle);
                if (info.hbmMask != IntPtr.Zero) DeleteObject(info.hbmMask);
                if (info.hbmColor != IntPtr.Zero) DeleteObject(info.hbmColor);
                DestroyIcon(iconHandle);
            }
        }

        private void ReplacePenTip(System.Windows.Forms.Cursor next)
        {
            System.Windows.Forms.Cursor previous = generatedPenTip;
            try { ScreenInkCursor = next; }
            catch { next.Dispose(); throw; }
            generatedPenTip = next;
            // The temporary Alt tool may still restore the previous tip.
            // It remains owned by its Cursor object while that reference lives.
            if (previous != null && previous != tempArrowCursor) previous.Dispose();
        }

        private void DisposePenTip()
        {
            haloInkCursor?.Dispose();
            haloInkCursor = null;
            haloSource = null;
            baseInkCursor = null;
            appliedInkCursor = null;
            if (generatedPenTip != null) generatedPenTip.Dispose();
            generatedPenTip = null;
        }

        private System.Windows.Forms.Cursor ScreenInkCursor
        {
            get { return baseInkCursor ?? IC.Cursor; }
            set
            {
                var previous = baseInkCursor;
                baseInkCursor = value;
                try { RefreshScreenInkCursor(); }
                catch { baseInkCursor = previous; throw; }
            }
        }

        internal void RefreshScreenInkCursor()
        {
            var original = baseInkCursor;
            if (original == null) return;
            var next = original;
            if (DrawingCursorHalo.AppliesTo(Root))
            {
                if (ReferenceEquals(haloSource, original))
                    next = haloInkCursor;
                else
                {
                    next = DrawingCursorHalo.Create(original);
                    try { IC.Cursor = next; }
                    catch { next.Dispose(); throw; }
                    var old = haloInkCursor;
                    haloSource = original;
                    haloInkCursor = appliedInkCursor = next;
                    old?.Dispose();
                    return;
                }
            }
            if (ReferenceEquals(appliedInkCursor, next)) return;
            IC.Cursor = next;
            appliedInkCursor = next;
        }
    }
}
