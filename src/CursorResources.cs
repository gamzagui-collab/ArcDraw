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
            try { IC.Cursor = next; }
            catch { next.Dispose(); throw; }
            generatedPenTip = next;
            // The temporary Alt tool may still restore the previous tip.
            // It remains owned by its Cursor object while that reference lives.
            if (previous != null && previous != tempArrowCursor) previous.Dispose();
        }

        private void DisposePenTip()
        {
            if (generatedPenTip != null) generatedPenTip.Dispose();
            generatedPenTip = null;
        }
    }
}
