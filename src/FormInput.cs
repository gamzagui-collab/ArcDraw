using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Ink;

namespace gInk
{
    public partial class FormInput : Form
    {
        Microsoft.Ink.Stroke stroke;
        Root Root;
        string Saved_Txt;
        Font Saved_Font;
        DrawingAttributes Saved_Da;
        bool Saved_Frame;
        bool Saved_White;
        bool Saved_Black;
        private readonly bool multiline;


        public FormInput(string caption, string label, string txt, bool ML, gInk.Root rt = null, Microsoft.Ink.Stroke stk = null, bool invisible = false)
        {
            multiline = ML;
            InitializeComponent();
            
            // local
            this.btOK.Text = rt.Local.ButtonOkText;
            this.btCancel.Text = rt.Local.ButtonCancelText;
            this.FontBtn.Text = rt.Local.ButtonFontText;
            this.ColorBtn.Text = rt.Local.OptionsPensColor;
            boxingCb.Items.AddRange(rt.Local.TextFramingText.Split(';'));

            Text = caption;
            captionLbl.Text = label;
            if (ML)
            {
                InputML.CommitOnEnter = stk != null;
                if (InputML.CommitOnEnter)
                {
                    btOK.DialogResult = DialogResult.None;
                    btOK.Click += (_, __) => InputML.RequestEnter(false);
                    InputML.ConfirmRequested += (_, __) => ConfirmScreenText();
                }
                InputML.Visible = true;
                txt = txt.Replace("\r\n", "\n").Replace("\n", "\r\n"); //in order to get multiline text to be correctly editable after restore
                InputML.Text = txt;
                ActiveControl = InputML;
            }
            else
            {
                InputSL.Visible = true;
                InputSL.Text = txt;
                ActiveControl = InputSL;
            }
            Root = rt;
            stroke = stk;

            if (invisible)
            {
                if (rt.TextDlgHiddenOpacity < 1.0)
                    this.VisibleCb.Checked = true;
            }
            else
                this.VisibleCb.Visible = false;

            if (stroke == null)
            {
                FontBtn.Visible = false;
                boxingCb.Visible = false;
            }
            else
            {
                FontBtn.Visible = true;
                ColorBtn.Visible = true;
                boxingCb.Visible = true;// !stk.ExtendedProperties.Contains(Root.ISTAG_GUID);

                FontDlg.Font = new Font((string)stk.ExtendedProperties[Root.TEXTFONT_GUID].Data, (float)(double)stk.ExtendedProperties[Root.TEXTFONTSIZE_GUID].Data,
                                        (System.Drawing.FontStyle)stk.ExtendedProperties[Root.TEXTFONTSTYLE_GUID].Data);
                InputML.TextChanged += new System.EventHandler(this.InputML_TextChanged);
                int i = (stk.ExtendedProperties.Contains(Root.ISSTROKE_GUID) ? 1 : 0) +
                        (stk.ExtendedProperties.Contains(Root.ISFILLEDWHITE_GUID) ? 2 : 0) +
                        (stk.ExtendedProperties.Contains(Root.ISFILLEDBLACK_GUID) ? 4 : 0);
                boxingCb.Text = boxingCb.Items[i].ToString();

                Saved_Txt = InputML.Text;
                Saved_Da = stk.DrawingAttributes.Clone();
                Saved_Font = (Font)FontDlg.Font.Clone();
                Saved_Frame = stk.ExtendedProperties.Contains(Root.ISSTROKE_GUID);
                Saved_White = stk.ExtendedProperties.Contains(Root.ISFILLEDWHITE_GUID);
                Saved_Black = stk.ExtendedProperties.Contains(Root.ISFILLEDBLACK_GUID);
                if (ML)
                    InputML.SelectAll();
                else
                    InputSL.SelectAll();
            }
            ActiveControl = multiline ? InputML : InputSL;
        }

        public void TextIn(string txt)
        {
            if (multiline)
                InputML.Text=txt;
            else
                InputSL.Text=txt;
        }

        public string TextOut()
        {
            if (multiline)
                return InputML.Text;
            else
                return InputSL.Text;
        }

        private void FontBtn_Click(object sender, EventArgs e)
        {
            if (FontDlg.ShowDialog() == DialogResult.OK)
            {
                stroke.ExtendedProperties.Add(Root.TEXTFONT_GUID, FontDlg.Font.Name);
                stroke.ExtendedProperties.Add(Root.TEXTFONTSIZE_GUID, (double)FontDlg.Font.Size);
                stroke.ExtendedProperties.Add(Root.TEXTFONTSTYLE_GUID, FontDlg.Font.Style);
                string st=InputML.Text;
                // to run event for refresh
                InputML.Text = "";InputML.Text = st;
            }
        }

        private void ColorBtn_Click(object sender, EventArgs e)
        {
            PenModifyDlg dlg = new PenModifyDlg(Root);
            DrawingAttributes da = stroke.DrawingAttributes.Clone();
            //dlg.hideWidth();
            if (dlg.ModifyPen(ref da))
            {
                stroke.DrawingAttributes = da;
                Root.FormDisplay.ClearCanvus();
                Root.FormDisplay.DrawStrokes();
                Root.FormDisplay.UpdateFormDisplay(true);
            }
        }

        private void InputML_TextChanged(object sender, EventArgs e)
        {
            string t = ((TextBox)sender).Text;
            if (t.Length == 0) t = " ";
            stroke.ExtendedProperties.Remove(Root.TEXT_GUID);
            stroke.ExtendedProperties.Add(Root.TEXT_GUID, t);
            if(!stroke.ExtendedProperties.Contains(Root.ISTAG_GUID))
                Root.FormCollection.ComputeTextBoxSize(ref stroke);
            Root.FormDisplay.ClearCanvus();
            Root.FormDisplay.DrawStrokes();
            Root.FormDisplay.UpdateFormDisplay(true);
        }

        private void ConfirmScreenText()
        {
            string text = InputML.Text.Length == 0 ? " " : InputML.Text;
            if (!Equals(stroke.ExtendedProperties[Root.TEXT_GUID].Data, text))
                InputML_TextChanged(InputML, EventArgs.Empty);
            DialogResult = DialogResult.OK;
        }

        private void TB_CtrlAPressed(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == Convert.ToChar(1))
            {
                (sender as TextBox).SelectAll();
                e.Handled = true;
            }

        }

        private void btCancel_Click(object sender, EventArgs e)
        {
            if(stroke != null )
            {
                InputML.Text = Saved_Txt;
                stroke.ExtendedProperties.Add(Root.TEXTFONT_GUID, Saved_Font.Name);
                stroke.ExtendedProperties.Add(Root.TEXTFONTSIZE_GUID, (double)Saved_Font.Size);
                stroke.ExtendedProperties.Add(Root.TEXTFONTSTYLE_GUID, Saved_Font.Style);
                Root.FormCollection.ComputeTextBoxSize(ref stroke);
                stroke.DrawingAttributes = Saved_Da;
                if (Saved_Frame)
                    stroke.ExtendedProperties.Add(Root.ISSTROKE_GUID, true);
                else
                    try { stroke.ExtendedProperties.Remove(Root.ISSTROKE_GUID); } catch { }

                if (Saved_Black)
                {
                    try { stroke.ExtendedProperties.Remove(Root.ISFILLEDWHITE_GUID); } catch { }
                    stroke.ExtendedProperties.Add(Root.ISFILLEDBLACK_GUID, true);
                }
                else if (Saved_White)
                {
                    try { stroke.ExtendedProperties.Remove(Root.ISFILLEDBLACK_GUID); } catch { }
                stroke.ExtendedProperties.Add(Root.ISFILLEDWHITE_GUID, true);
                }
                else
                {
                    try { stroke.ExtendedProperties.Remove(Root.ISFILLEDBLACK_GUID); } catch { }
                    try { stroke.ExtendedProperties.Remove(Root.ISFILLEDWHITE_GUID); } catch { }
                }
            }
        }

        private void boxingCb_TextChanged(object sender, EventArgs e)
        {
            int i = boxingCb.Items.IndexOf(boxingCb.Text);
            if ((i&1)!=0)
                stroke.ExtendedProperties.Add(Root.ISSTROKE_GUID, true);
            else
                try { stroke.ExtendedProperties.Remove(Root.ISSTROKE_GUID); } catch { }

            if ((i & 4) != 0)
            {
                try { stroke.ExtendedProperties.Remove(Root.ISFILLEDWHITE_GUID); } catch { }
                stroke.ExtendedProperties.Add(Root.ISFILLEDBLACK_GUID, true);
            }
            else if ((i & 2) != 0)
            {
                try { stroke.ExtendedProperties.Remove(Root.ISFILLEDBLACK_GUID); } catch { }
                stroke.ExtendedProperties.Add(Root.ISFILLEDWHITE_GUID, true);
            }
            else
            {
                try { stroke.ExtendedProperties.Remove(Root.ISFILLEDBLACK_GUID); } catch { }
                try { stroke.ExtendedProperties.Remove(Root.ISFILLEDWHITE_GUID); } catch { }
            }
            Root.FormDisplay.ClearCanvus();
            Root.FormDisplay.DrawStrokes();
            Root.FormDisplay.UpdateFormDisplay(true);
        }

        private void VisibleCb_CheckedChanged(object sender, EventArgs e)
        {
            this.Opacity=VisibleCb.Checked?Root.TextDlgHiddenOpacity:1;
            ActiveControl = multiline ? InputML : InputSL;
        }
    }

    public sealed class ScreenTextBox : TextBox
    {
        private const int CompleteIme = 0x8001; // WM_APP + 1, private to this EDIT window.
        private const int ApplyEnter = 0x8002;
        internal bool CommitOnEnter { get; set; }
        internal event EventHandler ConfirmRequested;
        private bool enterPending;

        protected override bool ProcessCmdKey(ref Message message, Keys keyData)
        {
            return HandleEnter(keyData) || base.ProcessCmdKey(ref message, keyData);
        }

        protected override void WndProc(ref Message message)
        {
            const int KeyDown = 0x0100;
            const int ImeKeyDown = 0x0290;

            if (message.Msg == CompleteIme || message.Msg == ApplyEnter)
            {
                message.Result = IntPtr.Zero;
                if (!enterPending || !Visible) return;
                bool lineBreak = message.WParam != IntPtr.Zero;
                if (message.Msg == CompleteIme)
                {
                    CompleteComposition();
                    // END can precede RESULTSTR/WM_IME_CHAR. Let the native
                    // EDIT control receive those messages before using Text.
                    if (!PostMessage(Handle, ApplyEnter, message.WParam, IntPtr.Zero))
                        enterPending = false;
                    return;
                }
                enterPending = false;
                if (HasComposition()) return;
                if (lineBreak) SelectedText = Environment.NewLine;
                else ConfirmRequested?.Invoke(this, EventArgs.Empty);
                return;
            }
            // Native EDIT and IME messages can bypass WinForms key preprocessing.
            if ((message.Msg == KeyDown || message.Msg == ImeKeyDown) &&
                (Keys)message.WParam.ToInt32() == Keys.Enter && HandleEnter(Keys.Enter | ModifierKeys))
            {
                message.Result = IntPtr.Zero;
                return;
            }
            if (enterPending && message.Msg == 0x0102 && message.WParam.ToInt32() == '\r')
            {
                message.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref message);
        }

        private bool HandleEnter(Keys key)
        {
            if (!CommitOnEnter) return false;
            bool lineBreak = key == (Keys.Shift | Keys.Enter);
            if (!lineBreak && key != Keys.Enter && key != (Keys.Control | Keys.Enter)) return false;
            RequestEnter(lineBreak);
            return true;
        }

        internal void RequestEnter(bool lineBreak)
        {
            if (enterPending || !IsHandleCreated) return;
            enterPending = PostMessage(Handle, CompleteIme, lineBreak ? new IntPtr(1) : IntPtr.Zero, IntPtr.Zero);
        }

        private void CompleteComposition()
        {
            IntPtr window = Handle;
            IntPtr context = ImmGetContext(window);
            if (context == IntPtr.Zero) return;
            try
            {
                if (ImmGetCompositionString(context, 0x0008, IntPtr.Zero, 0) > 0)
                    ImmNotifyIME(context, 0x0015, 1, 0); // NI_COMPOSITIONSTR, CPS_COMPLETE.
            }
            finally { ImmReleaseContext(window, context); }
        }

        private bool HasComposition()
        {
            IntPtr window = Handle;
            IntPtr context = ImmGetContext(window);
            if (context == IntPtr.Zero) return false;
            try { return ImmGetCompositionString(context, 0x0008, IntPtr.Zero, 0) > 0; }
            finally { ImmReleaseContext(window, context); }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
        [DllImport("imm32.dll")] private static extern IntPtr ImmGetContext(IntPtr window);
        [DllImport("imm32.dll")] private static extern bool ImmReleaseContext(IntPtr window, IntPtr context);
        [DllImport("imm32.dll", CharSet = CharSet.Unicode)]
        private static extern int ImmGetCompositionString(IntPtr context, uint index, IntPtr buffer, uint size);
        [DllImport("imm32.dll")]
        private static extern bool ImmNotifyIME(IntPtr context, uint action, uint index, uint value);
    }
}
