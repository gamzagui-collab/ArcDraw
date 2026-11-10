using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace gInk
{
    public partial class FormCollection
    {
        private readonly Dictionary<Control, Image> ownedButtonImages = new Dictionary<Control, Image>();
        private Font ownedClipFont;

        private void ReplaceButtonImage(Control button, Image next)
        {
            // Build next before entering this method: Ink COM property access
            // may pump WM_PAINT and the attached old image must remain valid.
            Image previous;
            ownedButtonImages.TryGetValue(button, out previous);
            button.BackgroundImage = next;
            ownedButtonImages[button] = next;
            if (previous != null && !Object.ReferenceEquals(previous, next)) previous.Dispose();
        }

        private void ReplaceClipFont(Font next)
        {
            Font previous = ownedClipFont;
            btClipArt.Font = btClip1.Font = btClip2.Font = btClip3.Font = next;
            ownedClipFont = next;
            if (previous != null) previous.Dispose();
        }

        private void DisposeToolbarFonts()
        {
            foreach (Image image in ownedButtonImages.Values) if (image != null) image.Dispose();
            ownedButtonImages.Clear();
            if (ownedClipFont != null) ownedClipFont.Dispose();
            ownedClipFont = null;
        }
    }
}
