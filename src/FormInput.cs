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
        private Font editorFont;
        private FlowLayoutPanel textStyleBar;
        private int editorMinimumHeight;
        private Rectangle buttonRow;
        private VerticalSizeSlider textSizeSlider;


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
                if (ML) CreateTextStyleBar();
                Disposed += delegate
                {
                    editorFont?.Dispose();
                    Saved_Font?.Dispose();
                    FontDlg.Dispose();
                };
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
            UpdateTextPreview();
            ResizeTextEditor();
        }

        private void CreateTextStyleBar()
        {
            FontBtn.Hide();
            ColorBtn.Hide();
            boxingCb.Hide();
            buttonRow = btOK.Bounds;
            editorMinimumHeight = 31;
            InputML.ScrollBars = ScrollBars.Vertical;
            InputML.WordWrap = false;
            textSizeSlider = new VerticalSizeSlider
            {
                Name = "ScreenTextSizeSlider", AccessibleName = "글자 크기 (포인트)",
                Unit = "pt", Maximum = 144, Location = new Point(InputML.Left, InputML.Top)
            };
            int sliderInset = textSizeSlider.Width + 9;
            InputML.Left += sliderInset;
            InputML.Width -= sliderInset;
            Controls.Add(textSizeSlider);
            textStyleBar = new FlowLayoutPanel
            {
                Name = "ScreenTextStyle", FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false, Height = 33, Width = InputML.Width
            };
            var family = new ComboBox { Name = "LiveTextFont", Width = 180, DropDownStyle = ComboBoxStyle.DropDownList, MaxDropDownItems = 14 };
            foreach (FontFamily available in FontFamily.Families)
            {
                using (available) family.Items.Add(available.Name);
            }
            if (!family.Items.Contains(Saved_Font.Name)) family.Items.Add(Saved_Font.Name);
            family.SelectedItem = Saved_Font.Name;
            var points = new NumericUpDown
            {
                Name = "LiveTextSize", AccessibleName = "글자 크기 (pt)", Width = 66,
                Minimum = 1, Maximum = Math.Max(144, (decimal)Saved_Font.SizeInPoints), DecimalPlaces = 1,
                Value = (decimal)Saved_Font.SizeInPoints
            };
            textSizeSlider.Maximum = (int)points.Maximum;
            textSizeSlider.Value = (int)Math.Round(points.Value);
            textSizeSlider.Dragged += value => points.Value = value;
            textSizeSlider.DragFinished += _ => { InputML.Focus(); InputML.ScrollToCaret(); };
            var color = new Button
            {
                Name = "LiveTextColor", AccessibleName = "텍스트 색상", Text = "색상",
                Width = 74, Height = 26, UseVisualStyleBackColor = false
            };
            void ShowColor(Color value)
            {
                color.BackColor = value;
                color.ForeColor = value.GetBrightness() > 0.55f ? Color.Black : Color.White;
            }
            ShowColor(stroke.DrawingAttributes.Color);
            textStyleBar.Controls.AddRange(new Control[]
            {
                new Label { Text = "글꼴", AutoSize = true, Margin = new Padding(0, 7, 4, 0) }, family,
                new Label { Text = "크기", AutoSize = true, Margin = new Padding(8, 7, 4, 0) }, points, color
            });
            Controls.Add(textStyleBar);
            family.SelectedIndexChanged += delegate { ApplyLiveFont(family.Text, (float)points.Value); InputML.Focus(); };
            points.ValueChanged += delegate
            {
                textSizeSlider.Value = (int)Math.Round(points.Value);
                ApplyLiveFont(family.Text, (float)points.Value);
            };
            points.KeyDown += (sender, args) =>
            {
                if (args.KeyCode != Keys.Enter) return;
                args.SuppressKeyPress = true;
                InputML.Focus();
            };
            points.PreviewKeyDown += (sender, args) => { if (args.KeyCode == Keys.Enter) args.IsInputKey = true; };
            color.Click += delegate
            {
                Color? next = DrawingColorPicker.Choose(this, color, stroke.DrawingAttributes.Color);
                if (next.HasValue)
                {
                    stroke.DrawingAttributes.Color = next.Value;
                    ShowColor(next.Value);
                    UpdateTextPreview();
                }
                InputML.Focus();
            };
            SetEditorFont((Font)Saved_Font.Clone());
            Shown += delegate
            {
                ResizeTextEditor();
                InputML.Focus();
                UpdateTextPreview();
            };
            InputML.KeyUp += delegate { if (Visible) UpdateCaretPreview(); };
            InputML.MouseUp += delegate { if (Visible) UpdateCaretPreview(); };
            FormClosed += delegate { Root.FormDisplay.ScreenTextEditor = null; Root.UponAllDrawingUpdate = true; };
        }

        private void ApplyLiveFont(string family, float points)
        {
            Font next;
            try { next = new Font(family, points, Saved_Font.Style, GraphicsUnit.Point); }
            catch (ArgumentException) { next = new Font(family, points, FontStyle.Regular, GraphicsUnit.Point); }
            SetEditorFont(next);
            stroke.ExtendedProperties.Add(Root.TEXTFONT_GUID, next.Name);
            stroke.ExtendedProperties.Add(Root.TEXTFONTSIZE_GUID, (double)next.SizeInPoints);
            stroke.ExtendedProperties.Add(Root.TEXTFONTSTYLE_GUID, next.Style);
            UpdateTextPreview();
            ResizeTextEditor();
        }

        private void SetEditorFont(Font font)
        {
            Font previous = editorFont;
            editorFont = font;
            InputML.Font = font;
            previous?.Dispose();
        }

        private void UpdateTextPreview()
        {
            if (!stroke.ExtendedProperties.Contains(Root.ISTAG_GUID))
                Root.FormCollection.ComputeTextBoxSize(ref stroke);
            Root.FormDisplay.ClearCanvus();
            Root.FormDisplay.DrawStrokes();
            if (Visible && textStyleBar != null) Root.FormDisplay.ScreenTextEditor = this;
            Root.FormDisplay.UpdateFormDisplay(true);
        }

        private void UpdateCaretPreview()
        {
            if (Root.FormDisplay.ScreenTextEditor == this)
                Root.FormDisplay.UpdateFormDisplay(true);
        }

        internal void DrawScreenCaret(Graphics graphics, Graphics coordinates)
        {
            if (!Visible || !InputML.Focused || InputML.SelectionLength > 0 || stroke.Deleted) return;
            var properties = stroke.ExtendedProperties;
            Point anchor = new Point((int)(double)properties[Root.TEXTX_GUID].Data, (int)(double)properties[Root.TEXTY_GUID].Data);
            Root.FormCollection.IC.Renderer.InkSpaceToPixel(coordinates, ref anchor);
            string text = InputML.Text;
            int index = InputML.SelectionStart;
            int lineStart = index == 0 ? 0 : text.LastIndexOf('\n', index - 1) + 1;
            int lineEnd = text.IndexOf('\n', index);
            if (lineEnd < 0) lineEnd = text.Length;
            int row = 0;
            for (int i = 0; i < lineStart; i++) if (text[i] == '\n') row++;
            using (var format = new StringFormat(StringFormat.GenericTypographic))
            using (var dark = new Pen(Color.FromArgb(20, 25, 35), 3))
            using (var light = new Pen(Color.White, 1))
            {
                format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
                float x = coordinates.MeasureString(text.Substring(lineStart, index - lineStart), editorFont, PointF.Empty, format).Width;
                if ((StringAlignment)properties[Root.TEXTHALIGN_GUID].Data == StringAlignment.Far)
                    x -= coordinates.MeasureString(text.Substring(lineStart, lineEnd - lineStart).TrimEnd('\r'), editorFont, PointF.Empty, format).Width;
                float lineHeight = editorFont.GetHeight(coordinates);
                var state = graphics.Save();
                try
                {
                    graphics.TranslateTransform(anchor.X, anchor.Y);
                    if (properties.Contains(Root.ROTATION_GUID))
                        graphics.RotateTransform((float)(double)properties[Root.ROTATION_GUID].Data);
                    graphics.DrawLine(dark, x, row * lineHeight, x, (row + 1) * lineHeight);
                    graphics.DrawLine(light, x, row * lineHeight, x, (row + 1) * lineHeight);
                }
                finally { graphics.Restore(state); }
            }
        }

        private void ResizeTextEditor()
        {
            if (textStyleBar == null || !InputML.IsHandleCreated) return;
            Rectangle work = Screen.FromControl(this).WorkingArea;
            int lines = Math.Max(1, InputML.GetLineFromCharIndex(InputML.TextLength) + 1);
            // Native EDIT can still report the preceding line for an empty tail.
            int explicitLines = InputML.Text.Count(ch => ch == '\n') + 1;
            int available = Math.Max(editorMinimumHeight, work.Height - InputML.Top - 110);
            int desired = Math.Max(editorMinimumHeight, Math.Max(lines, explicitLines) * InputML.Font.Height + 9);
            int height = Math.Min(available, desired);
            SuspendLayout();
            try
            {
                InputML.Height = height;
                textStyleBar.Location = new Point(InputML.Left, InputML.Bottom + 6);
                btOK.Location = new Point(buttonRow.Left, textStyleBar.Bottom + 3);
                btCancel.Top = btOK.Top;
                ClientSize = new Size(ClientSize.Width, Math.Max(btOK.Bottom, textSizeSlider.Bottom) + 10);
                if (Bottom > work.Bottom) Top = Math.Max(work.Top, work.Bottom - Height);
            }
            finally { ResumeLayout(false); }
            if (InputML.Focused) InputML.ScrollToCaret();
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

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (!e.Cancel && DialogResult == DialogResult.Cancel && stroke != null && Saved_Font != null)
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
                if (lineBreak)
                {
                    SelectedText = Environment.NewLine;
                    ScrollToCaret();
                }
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
