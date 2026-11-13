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
        private ToolbarColorPalette colorPalette;
        private ToolbarColorHitTarget colorHitTarget;
        private FormInput colorEditor;
        private WhiteboardLayerForm colorNote;
        private bool textColorMode;
        private int? toolbarColor;

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
            mainColorButton = AddPenToolbarButton("penMainColor", "색상", ToggleColorPalette);
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

        internal void RestoreBrushColor()
        {
            if (Root.CurrentPen < 0 || !Root.Settings.BrushColor.HasValue) return;
            DrawingAttributes pen = IC.DefaultDrawingAttributes.Clone();
            pen.Color = Root.Settings.BrushColor.Value;
            ApplyWorkingPen(pen);
        }

        private void RefreshPenToolbar()
        {
            if (mainColorButton == null) return;
            DrawingAttributes pen = IC.DefaultDrawingAttributes;
            RefreshColorSwatch();
            ReplaceButtonImage(penSettingsButton, PenSample.Line(Root, pen));
            Root.UponButtonsUpdate |= 2;
            Root.DrawingSession?.RefreshSizeSlider();
        }

        private Color SelectedToolColor()
        {
            return colorEditor?.LiveTextColor ?? colorNote?.LiveMemoColor ??
                (textColorMode ? Root.Settings.ScreenTextColor ?? Color.Black : WorkingPenColor);
        }

        private void RefreshColorSwatch()
        {
            Color color = SelectedToolColor();
            if (toolbarColor == color.ToArgb()) return;
            toolbarColor = color.ToArgb();
            ReplaceButtonImage(mainColorButton, PenSample.Swatch(color));
            Root.UponButtonsUpdate |= 2;
            if (colorEditor != null && Root.FormDisplay?.ScreenTextEditor == colorEditor)
                Root.FormDisplay.RefreshScreenTextToolbar();
        }

        internal void UpdateColorToolbar(FormInput editor, WhiteboardLayerForm note, bool text, bool active)
        {
            if (mainColorButton == null) return;
            if (colorEditor != editor || colorNote != note || textColorMode != text)
            {
                colorPalette?.Hide();
                colorEditor = editor;
                colorNote = note;
                textColorMode = text;
                mainColorButton.Text = text ? "글자색" : "펜 색상";
                mainColorButton.AccessibleName = mainColorButton.Text;
                Root.UponButtonsUpdate |= 2;
                RefreshColorSwatch();
            }
            if (!active || !mainColorButton.Visible)
            {
                HideColorToolbar();
                return;
            }
            if (note != null || editor != null)
            {
                if (colorHitTarget == null)
                {
                    colorHitTarget = new ToolbarColorHitTarget { Owner = this };
                    colorHitTarget.Click += delegate { ToggleColorPalette(mainColorButton); };
                }
                Rectangle bounds = mainColorButton.RectangleToScreen(mainColorButton.ClientRectangle);
                if (colorHitTarget.Bounds != bounds) colorHitTarget.Bounds = bounds;
                if (!colorHitTarget.Visible) colorHitTarget.Show();
                if (editor != null) colorHitTarget.EnableForTextEditor();
            }
            else colorHitTarget?.Hide();
            if (colorPalette != null && colorPalette.Visible)
            {
                Rectangle anchor = mainColorButton.RectangleToScreen(mainColorButton.ClientRectangle);
                Point mouse = System.Windows.Forms.Cursor.Position;
                if ((GetAsyncKeyState(1) & 0x8000) != 0 && !anchor.Contains(mouse) &&
                    !colorPalette.Bounds.Contains(mouse))
                    colorPalette.Hide();
                else
                {
                    colorPalette.PlaceAt(mainColorButton);
                    if (editor != null) colorPalette.EnableForTextEditor();
                }
            }
            if (note != null) RefreshColorSwatch();
        }

        internal void HideColorToolbar()
        {
            colorHitTarget?.Hide();
            colorPalette?.Hide();
        }

        private void ToggleColorPalette(Button anchor)
        {
            if (colorPalette != null && colorPalette.Visible)
            {
                colorPalette.Hide();
                return;
            }
            if (colorPalette == null)
            {
                colorPalette = new ToolbarColorPalette(ApplyToolbarColor) { Owner = this };
            }
            colorPalette.SetCurrentColor(SelectedToolColor());
            colorPalette.PlaceAt(anchor);
            colorPalette.Show();
            if (colorEditor != null) colorPalette.EnableForTextEditor();
            colorPalette.RaiseWithoutActivation();
        }

        private void ApplyToolbarColor(Color color)
        {
            colorPalette.Hide();
            if (colorEditor != null)
            {
                if (Root.FormDisplay.ScreenTextEditor == colorEditor) colorEditor.SetLiveTextColor(color);
            }
            else if (colorNote != null)
            {
                if (Root.Whiteboards.ActiveNote == colorNote) colorNote.SetLiveMemoColor(color);
            }
            else if (textColorMode)
            {
                Root.Settings.SetToolColor(color, true);
            }
            else
            {
                if (ScreenContentBusy || IC.CollectingInk || (Root.Whiteboards?.HasBusyInk ?? false)) return;
                DrawingAttributes pen = IC.DefaultDrawingAttributes.Clone();
                pen.Color = color;
                ApplyWorkingPen(pen);
                Root.Settings.SetToolColor(color, false);
            }
            RefreshColorSwatch();
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
            ToolbarColorPalette.Place(dialog, anchor);
            return dialog.ShowDialog(this);
        }
    }

    internal sealed class ToolbarColorHitTarget : NonActivatingToolbarWindow
    {
        internal ToolbarColorHitTarget()
        {
            Name = "ToolbarColorHitTarget";
            Opacity = 0.01;
            Cursor = System.Windows.Forms.Cursors.Hand;
        }
    }

    internal sealed class ToolbarColorPalette : NonActivatingToolbarWindow
    {
        private readonly List<Button> swatches = new List<Button>();
        private readonly TrackBar[] channels = new TrackBar[3];
        private readonly Panel customColors;
        private readonly Button customPreview;
        private Color currentColor;
        private Rectangle lastAnchor;

        internal ToolbarColorPalette(Action<Color> select)
        {
            Name = "DrawingColorPicker";
            BackColor = Color.FromArgb(31, 34, 40);
            ClientSize = new Size(216, 112);
            Color[] colors =
            {
                Color.Black, Color.White, Color.DimGray, Color.Firebrick, Color.OrangeRed, Color.Gold,
                Color.ForestGreen, Color.Teal, Color.DeepSkyBlue, Color.RoyalBlue, Color.BlueViolet, Color.DeepPink
            };
            for (int index = 0; index < colors.Length; index++)
            {
                Color color = colors[index];
                var button = new PaletteButton
                {
                    Name = "Color" + color.Name,
                    AccessibleName = "#" + (color.ToArgb() & 0xffffff).ToString("X6"),
                    Bounds = new Rectangle(8 + index % 6 * 34, 8 + index / 6 * 34, 30, 30),
                    BackColor = color
                };
                button.Click += delegate { select(color); };
                swatches.Add(button);
                Controls.Add(button);
            }
            var more = new PaletteButton
            {
                Name = "CustomToolbarColor", Text = "다른 색…", Bounds = new Rectangle(8, 78, 96, 26),
                BackColor = Color.WhiteSmoke
            };
            more.Click += delegate { ShowCustomColors(!customColors.Visible); };
            var close = new PaletteButton
            {
                Text = "닫기", Bounds = new Rectangle(110, 78, 98, 26), BackColor = Color.WhiteSmoke
            };
            close.Click += delegate { Hide(); };
            Controls.AddRange(new Control[] { more, close });
            customColors = new Panel { Location = new Point(8, 112), Size = new Size(200, 108), Visible = false };
            customPreview = new PaletteButton
            {
                Name = "ApplyCustomToolbarColor", Text = "적용", Bounds = new Rectangle(0, 76, 200, 26)
            };
            customPreview.Click += delegate { select(customPreview.BackColor); };
            for (int index = 0; index < channels.Length; index++)
            {
                string label = "RGB"[index].ToString();
                customColors.Controls.Add(new Label
                {
                    Text = label, ForeColor = Color.White, Bounds = new Rectangle(0, index * 25, 16, 24),
                    TextAlign = ContentAlignment.MiddleCenter
                });
                var channel = new PaletteTrackBar
                {
                    AccessibleName = label, Bounds = new Rectangle(16, index * 25, 184, 24),
                    Maximum = 255, TickStyle = TickStyle.None, AutoSize = false
                };
                channels[index] = channel;
                customColors.Controls.Add(channel);
            }
            foreach (TrackBar channel in channels) channel.ValueChanged += delegate { RefreshCustomColor(); };
            customColors.Controls.Add(customPreview);
            Controls.Add(customColors);
            SizeChanged += delegate { lastAnchor = Rectangle.Empty; };
        }

        internal void SetCurrentColor(Color current)
        {
            currentColor = current;
            ShowCustomColors(false);
            foreach (Button swatch in swatches)
                swatch.FlatAppearance.BorderSize = swatch.BackColor.ToArgb() == current.ToArgb() ? 3 : 1;
        }

        private void ShowCustomColors(bool visible)
        {
            customColors.Visible = visible;
            ClientSize = new Size(216, visible ? 220 : 112);
            if (!visible) return;
            channels[0].Value = currentColor.R;
            channels[1].Value = currentColor.G;
            channels[2].Value = currentColor.B;
            RefreshCustomColor();
        }

        private void RefreshCustomColor()
        {
            Color color = Color.FromArgb(channels[0].Value, channels[1].Value, channels[2].Value);
            customPreview.BackColor = color;
            customPreview.ForeColor = color.GetBrightness() > 0.55f ? Color.Black : Color.White;
        }

        internal void PlaceAt(Control anchor)
        {
            Rectangle bounds = anchor.RectangleToScreen(anchor.ClientRectangle);
            if (Visible && bounds == lastAnchor) return;
            lastAnchor = bounds;
            Place(this, anchor);
        }

        internal static void Place(Form dialog, Control anchor)
        {
            Rectangle button = anchor.RectangleToScreen(anchor.ClientRectangle);
            Rectangle work = Screen.FromRectangle(button).WorkingArea;
            dialog.StartPosition = FormStartPosition.Manual;
            int x = Math.Max(work.Left, Math.Min(button.Left, work.Right - dialog.Width));
            int y = Math.Max(work.Top, Math.Min(button.Top, work.Bottom - dialog.Height));
            var candidates = new[]
            {
                new Point(x, button.Bottom + 4), new Point(x, button.Top - dialog.Height - 4),
                new Point(button.Left - dialog.Width - 4, y), new Point(button.Right + 4, y)
            };
            foreach (Point candidate in candidates)
                if (work.Contains(new Rectangle(candidate, dialog.Size)))
                {
                    dialog.Location = candidate;
                    return;
                }
            dialog.Location = new Point(x, y);
        }

        private sealed class PaletteButton : Button
        {
            internal PaletteButton()
            {
                SetStyle(ControlStyles.Selectable, false);
                TabStop = false;
                Cursor = System.Windows.Forms.Cursors.Hand;
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderColor = Color.Gray;
                UseVisualStyleBackColor = false;
            }
        }

        private sealed class PaletteTrackBar : TrackBar
        {
            internal PaletteTrackBar()
            {
                SetStyle(ControlStyles.Selectable, false);
                TabStop = false;
            }
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
