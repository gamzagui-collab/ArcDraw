using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Ink;
using Button = System.Windows.Forms.Button;

namespace gInk
{
    public partial class FormCollection
    {
        private Button mainColorButton, penSettingsButton, presetsButton;
        private DrawingAttributes workingPen;

        internal int WorkingPenPixels
        {
            get { return Math.Max(1, Root.HiMetricToPixel(IC.DefaultDrawingAttributes.Width)); }
        }

        internal Color WorkingPenColor { get { return IC.DefaultDrawingAttributes.Color; } }

        internal void SetPenSizeFromSlider(int pixels)
        {
            if (pixels == WorkingPenPixels || ScreenContentBusy || IC.CollectingInk ||
                (Root.Whiteboards?.HasBusyInk ?? false)) return;
            DrawingAttributes pen = IC.DefaultDrawingAttributes.Clone();
            ResizePen(pen, pixels);
            ApplyWorkingPen(pen);
        }

        private void ResizePen(DrawingAttributes pen, double pixels)
        {
            float previous = pen.Width;
            pen.Width = Math.Max(1, Root.PixelToHiMetric(pixels));
            pen.Height = Math.Max(1, pen.Height * pen.Width / previous);
        }

        private void EnsurePenToolbar()
        {
            if (mainColorButton != null) return;
            mainColorButton = AddPenToolbarButton("penMainColor", "색상", ChooseMainColor);
            penSettingsButton = AddPenToolbarButton("penSettings", "펜 설정", EditWorkingPen);
            presetsButton = AddPenToolbarButton("DrawingPresets", "프리셋", ShowPenPresets);
            RefreshPenToolbar();
        }

        private Button AddPenToolbarButton(string name, string text, Action<Button> action)
        {
            var button = new Button { Name = name, Text = text, AccessibleName = text, TabStop = false };
            button.Click += delegate
            {
                if (ToolbarMoved) { ToolbarMoved = false; return; }
                if (ScreenContentBusy || IC.CollectingInk || (Root.Whiteboards?.HasBusyInk ?? false)) return;
                action(button);
            };
            gpButtons.Controls.Add(button);
            return button;
        }

        private bool IsLegacyPenButton(Control control)
        {
            return control == btExtraPens || btPen.Contains(control);
        }

        private void PlacePenToolbarFirst(List<Control> buttons)
        {
            btExtraPens.Hide();
            foreach (Button button in btPen) if (button != null) button.Hide();
            var first = new List<Control> { mainColorButton, penSettingsButton, presetsButton };
            if (buttons.Remove(btHand)) first.Insert(1, btHand);
            foreach (Control control in first) buttons.Remove(control);
            buttons.InsertRange(0, first);
        }

        private void SetWorkingPenAttributes(DrawingAttributes pen)
        {
            IC.DefaultDrawingAttributes = pen.Clone();
            Root.GlobalPenWidth = pen.Width;
        }

        private void RememberCurrentPenSize()
        {
            workingPen = IC.DefaultDrawingAttributes.Clone();
            RefreshPenToolbar();
            Root.Whiteboards?.RefreshInkTools();
        }

        private void ApplyWorkingPen(DrawingAttributes pen)
        {
            workingPen = pen.Clone();
            SetWorkingPenAttributes(workingPen);
            if (Root.CanvasCursor == 1 && Root.ToolSelected == Tools.Hand && !Root.EraserMode)
                SetPenTipCursor();
            RefreshPenToolbar();
            Root.Whiteboards?.RefreshInkTools();
        }

        private void RefreshPenToolbar()
        {
            if (mainColorButton == null) return;
            DrawingAttributes pen = IC.DefaultDrawingAttributes;
            ReplaceButtonImage(mainColorButton, PenSample.Swatch(pen.Color));
            ReplaceButtonImage(penSettingsButton, PenSample.Line(Root, pen));
            Root.UponButtonsUpdate |= 2;
            Root.DrawingSession?.RefreshPenSizeSlider();
        }

        private void ChooseMainColor(Button anchor)
        {
            DrawingAttributes pen = IC.DefaultDrawingAttributes.Clone();
            Color? chosen = DrawingColorPicker.Choose(this, anchor, pen.Color);
            if (!chosen.HasValue) return;
            pen.Color = chosen.Value;
            ApplyWorkingPen(pen);
        }

        private void EditWorkingPen(Button anchor)
        {
            DrawingAttributes pen = IC.DefaultDrawingAttributes.Clone();
            using (var dialog = new Form
            {
                Name = "WorkingPenSettings", Text = "펜 설정", ShowInTaskbar = false,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                ClientSize = new Size(255, 195), TopMost = true, AutoScaleMode = AutoScaleMode.None
            })
            {
                var width = new NumericUpDown
                {
                    Name = "WorkingPenWidth", Minimum = 0.5m, Maximum = 200, DecimalPlaces = 1, Increment = 0.5m,
                    Value = Math.Max(0.5m, Math.Min(200, (decimal)Root.HiMetricToPixel(pen.Width)))
                };
                var opacity = new NumericUpDown
                {
                    Name = "WorkingPenOpacity", Minimum = 0, Maximum = 100,
                    Value = Math.Round((255 - pen.Transparency) * 100m / 255)
                };
                var tip = new ComboBox { Name = "WorkingPenTip", DropDownStyle = ComboBoxStyle.DropDownList };
                tip.Items.AddRange(new object[] { "원형", "사각형" });
                tip.SelectedIndex = pen.PenTip == PenTip.Rectangle ? 1 : 0;
                var line = new ComboBox { Name = "WorkingPenLine", DropDownStyle = ComboBoxStyle.DropDownList };
                var dash = new[] { DashStyle.Custom, DashStyle.Solid, DashStyle.Dash, DashStyle.Dot, DashStyle.DashDot, DashStyle.DashDotDot };
                line.Items.AddRange(new object[] { "자유선", "실선", "파선", "점선", "일점쇄선", "이점쇄선" });
                line.SelectedIndex = Math.Max(0, Array.IndexOf(dash, Root.LineStyleFromString(Root.LineStyleToString(pen.ExtendedProperties))));
                AddPenField(dialog, "굵기 (px)", width, 12);
                AddPenField(dialog, "불투명도 (%)", opacity, 47);
                AddPenField(dialog, "펜촉", tip, 82);
                AddPenField(dialog, "선 모양", line, 117);
                var apply = new Button { Text = "적용", DialogResult = DialogResult.OK, Bounds = new Rectangle(82, 155, 74, 28) };
                var cancel = new Button { Text = "취소", DialogResult = DialogResult.Cancel, Bounds = new Rectangle(166, 155, 74, 28) };
                dialog.Controls.AddRange(new Control[] { apply, cancel });
                dialog.AcceptButton = apply;
                dialog.CancelButton = cancel;
                if (ShowPenDialog(dialog, anchor) != DialogResult.OK) return;
                ResizePen(pen, (double)width.Value);
                pen.Transparency = (byte)(255 - Math.Round(opacity.Value * 255 / 100));
                pen.PenTip = tip.SelectedIndex == 0 ? PenTip.Ball : PenTip.Rectangle;
                if (pen.ExtendedProperties.Contains(Root.DASHED_LINE_GUID))
                    pen.ExtendedProperties.Remove(Root.DASHED_LINE_GUID);
                if (line.SelectedIndex > 0) pen.ExtendedProperties.Add(Root.DASHED_LINE_GUID, dash[line.SelectedIndex]);
                ApplyWorkingPen(pen);
            }
        }

        private static void AddPenField(Form dialog, string label, Control field, int y)
        {
            dialog.Controls.Add(new Label { Text = label, Bounds = new Rectangle(12, y, 100, 26), TextAlign = ContentAlignment.MiddleLeft });
            field.Bounds = new Rectangle(118, y, 122, 27);
            dialog.Controls.Add(field);
        }

        private void ShowPenPresets(Button anchor)
        {
            int chosen = -1;
            using (var dialog = new Form
            {
                Name = "DrawingPresetPicker", Text = "프리셋", ShowInTaskbar = false, TopMost = true,
                FormBorderStyle = FormBorderStyle.FixedToolWindow, AutoScaleMode = AutoScaleMode.None
            })
            {
                var rows = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
                for (int i = 0; i < Root.MaxDisplayedPens; i++)
                {
                    if (!Root.PenEnabled[i]) continue;
                    int slot = i;
                    DrawingPreset preset = Root.Settings.GetPreset(slot);
                    var row = new Button
                    {
                        Name = "DrawingPreset" + slot, TextAlign = ContentAlignment.MiddleLeft, ImageAlign = ContentAlignment.MiddleLeft,
                        TextImageRelation = TextImageRelation.ImageBeforeText, Width = 246, Height = 34, UseMnemonic = false,
                        Text = preset.Name + " · " + Math.Max(1, Root.HiMetricToPixel(preset.Attributes.Width)) + " px · " +
                            Math.Round((255 - preset.Attributes.Transparency) * 100m / 255) + "%",
                        Image = PenSample.Line(Root, preset.Attributes)
                    };
                    Image image = row.Image;
                    row.Disposed += delegate { image.Dispose(); };
                    row.Click += delegate { chosen = slot; dialog.DialogResult = DialogResult.OK; };
                    rows.Controls.Add(row);
                }
                dialog.Controls.Add(rows);
                dialog.ClientSize = new Size(273, Math.Max(40, Math.Min(402, rows.Controls.Count * 40)));
                if (ShowPenDialog(dialog, anchor) != DialogResult.OK || chosen < 0) return;
                Root.Whiteboards?.DrawingPenSelected();
                if (Root.Settings.HasPreset(chosen)) Root.Settings.RecallPreset(chosen);
                else { SelectPen(chosen); SelectTool(Tools.Hand, Filling.Empty); }
            }
        }

        private DialogResult ShowPenDialog(Form dialog, Button anchor)
        {
            DrawingColorPicker.Place(dialog, anchor);
            return dialog.ShowDialog(this);
        }
    }

    internal static class DrawingColorPicker
    {
        internal static Color? Choose(Form owner, Control anchor, Color current)
        {
            Color? chosen = null;
            Color[] colors = { Color.Black, Color.White, Color.DimGray, Color.Firebrick, Color.OrangeRed, Color.Gold,
                Color.ForestGreen, Color.Teal, Color.DeepSkyBlue, Color.RoyalBlue, Color.BlueViolet, Color.DeepPink };
            using (var dialog = new Form
            {
                Name = "DrawingColorPicker", Text = "색상", ShowInTaskbar = false, TopMost = true,
                FormBorderStyle = FormBorderStyle.FixedToolWindow, ClientSize = new Size(218, 122), AutoScaleMode = AutoScaleMode.None
            })
            {
                for (int i = 0; i < colors.Length; i++)
                {
                    Color color = colors[i];
                    var button = new Button
                    {
                        Name = "Color" + color.Name, AccessibleName = "#" + (color.ToArgb() & 0xffffff).ToString("X6"),
                        Bounds = new Rectangle(8 + (i % 6) * 34, 8 + (i / 6) * 36, 30, 30),
                        UseVisualStyleBackColor = false, BackColor = color, FlatStyle = FlatStyle.Flat
                    };
                    button.FlatAppearance.BorderColor = Color.Gray;
                    button.FlatAppearance.BorderSize = color.ToArgb() == current.ToArgb() ? 3 : 1;
                    button.Click += delegate { chosen = color; dialog.DialogResult = DialogResult.OK; };
                    dialog.Controls.Add(button);
                }
                var more = new Button { Text = "다른 색…", Bounds = new Rectangle(8, 82, 104, 30) };
                more.Click += delegate
                {
                    using (var picker = new ColorDialog { Color = current, FullOpen = true })
                        if (picker.ShowDialog(dialog) == DialogResult.OK)
                        { chosen = picker.Color; dialog.DialogResult = DialogResult.OK; }
                };
                var cancel = new Button { Text = "취소", DialogResult = DialogResult.Cancel, Bounds = new Rectangle(125, 82, 83, 30) };
                dialog.Controls.AddRange(new Control[] { more, cancel });
                dialog.CancelButton = cancel;
                Place(dialog, anchor);
                dialog.ShowDialog(owner);
            }
            return chosen;
        }

        internal static void Place(Form dialog, Control anchor)
        {
            Rectangle button = anchor.RectangleToScreen(anchor.ClientRectangle);
            Rectangle work = Screen.FromRectangle(button).WorkingArea;
            dialog.StartPosition = FormStartPosition.Manual;
            int y = button.Bottom + 4;
            if (y + dialog.Height > work.Bottom) y = button.Top - dialog.Height - 4;
            dialog.Location = new Point(
                Math.Max(work.Left, Math.Min(button.Left, work.Right - dialog.Width)),
                Math.Max(work.Top, Math.Min(y, work.Bottom - dialog.Height)));
        }
    }

    internal static class PenSample
    {
        internal static Bitmap Swatch(Color color)
        {
            var bitmap = new Bitmap(48, 48);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var brush = new SolidBrush(color))
            using (var edge = new Pen(Color.Gray, 2))
            {
                graphics.FillRectangle(brush, 4, 4, 39, 39);
                graphics.DrawRectangle(edge, 4, 4, 39, 39);
            }
            return bitmap;
        }

        internal static Bitmap Line(Root root, DrawingAttributes attributes)
        {
            var bitmap = new Bitmap(64, 32);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (var brush = new SolidBrush(Color.FromArgb(225, 230, 235)))
            using (var pen = new Pen(Color.FromArgb(255 - attributes.Transparency, attributes.Color),
                Math.Max(1, Math.Min(18, root.HiMetricToPixel(attributes.Width)))))
            {
                graphics.Clear(Color.White);
                for (int y = 0; y < 32; y += 8)
                    for (int x = (y / 8 % 2) * 8; x < 64; x += 16) graphics.FillRectangle(brush, x, y, 8, 8);
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                pen.StartCap = pen.EndCap = attributes.PenTip == PenTip.Ball ? LineCap.Round : LineCap.Square;
                DashStyle dash = root.LineStyleFromString(root.LineStyleToString(attributes.ExtendedProperties));
                if (dash != DashStyle.Custom) pen.DashStyle = dash;
                graphics.DrawLine(pen, 10, 16, 54, 16);
            }
            return bitmap;
        }
    }
}
