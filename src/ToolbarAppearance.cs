using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;

namespace gInk
{
    public sealed class ToolbarTheme
    {
        public string Id, Name;
        public Color Background=Color.FromArgb(245,247,251), Surface=Color.White;
        public Color Text=Color.FromArgb(27,40,57), Accent=Color.FromArgb(37,99,235);
        public int Radius=6, Gap=2;
        public bool Border, Labels;
        public override string ToString() { return Name; }
    }

    public sealed class ToolbarAppearance
    {
        private readonly Root root;
        public int ScalePercent { get; private set; }
        public string ThemeId { get; private set; }
        public string Layout { get; private set; }
        public bool Vertical { get { return Layout=="vertical"; } }
        public bool DoubleRow { get { return Layout=="horizontal2"; } }
        private static string ValidLayout(string value) { return value=="vertical"||value=="horizontal2"?value:"horizontal"; }
        public ToolbarTheme CurrentTheme { get; private set; }
        private string SettingsFile { get { return Path.Combine(Program.RunningFolder,"toolbar-ui.ini"); } }

        public ToolbarAppearance(Root root)
        {
            this.root=root; ScalePercent=100; ThemeId="default";Layout="horizontal";
            if(File.Exists(SettingsFile))foreach(string line in File.ReadAllLines(SettingsFile))
            {
                int equal=line.IndexOf('=');if(equal<1)continue;
                string key=line.Substring(0,equal).Trim(),value=line.Substring(equal+1).Trim();
                int percent;
                if(key=="ScalePercent"&&Int32.TryParse(value,out percent))ScalePercent=Math.Max(50,Math.Min(150,percent));
                if(key=="Theme")ThemeId=value;
                if(key=="Layout")Layout=ValidLayout(value);
            }
        }

        public List<ToolbarTheme> LoadThemes()
        {
            List<ToolbarTheme> result=new List<ToolbarTheme> {
                new ToolbarTheme {Id="default",Name="기본",Background=Color.FromArgb(root.ToolbarBGColor[1],root.ToolbarBGColor[2],root.ToolbarBGColor[3])},
                new ToolbarTheme {Id="light",Name="라이트"}
            };
            result.AddRange(ToolbarDesign.BuiltInThemes());
            string folder=Path.Combine(Program.ProgramFolder,"toolbar-themes");
            if(!Directory.Exists(folder))return result;
            foreach(string file in Directory.GetFiles(folder,"*.ini"))
            {
                string id=Path.GetFileNameWithoutExtension(file);
                if(result.Exists(t=>t.Id==id))continue;
                ToolbarTheme theme=new ToolbarTheme {Id=id,Name=id};
                try
                {
                    foreach(string line in File.ReadAllLines(file))
                    {
                        int equal=line.IndexOf('=');if(equal<1||line.TrimStart().StartsWith("#"))continue;
                        string key=line.Substring(0,equal).Trim(),value=line.Substring(equal+1).Trim();
                        if(key=="Name")theme.Name=value;
                        else if(key=="Background")theme.Background=ColorTranslator.FromHtml(value);
                        else if(key=="Surface")theme.Surface=ColorTranslator.FromHtml(value);
                        else if(key=="Text")theme.Text=ColorTranslator.FromHtml(value);
                        else if(key=="Accent")theme.Accent=ColorTranslator.FromHtml(value);
                    }
                    result.Add(theme);
                }
                catch(ArgumentException) { /* Invalid optional theme: keep usable built-ins. */ }
            }
            return result;
        }

        public void Apply()
        {
            List<ToolbarTheme> themes=LoadThemes();
            CurrentTheme=themes.Find(t=>t.Id==ThemeId)??themes[0];
            ThemeId=CurrentTheme.Id;
            root.ToolbarOrientation=Vertical?Orientation.toUp:Orientation.toLeft;
            if(root.FormCollection!=null)root.FormCollection.ApplyToolbarAppearance(CurrentTheme,ScalePercent);
            if(root.Whiteboards!=null)root.Whiteboards.ApplyToolbarAppearance(CurrentTheme,ScalePercent);
            root.UponAllDrawingUpdate=true;root.UponButtonsUpdate|=2;
        }

        public void SetPreferences(string theme,int scale,string layout)
        {
            string oldTheme=ThemeId,oldLayout=Layout;int oldScale=ScalePercent;
            ThemeId=theme;ScalePercent=Math.Max(50,Math.Min(150,scale));Layout=ValidLayout(layout);
            try
            {
                Apply();
                File.WriteAllLines(SettingsFile,new[]{"Theme="+ThemeId,"ScalePercent="+ScalePercent.ToString(CultureInfo.InvariantCulture),"Layout="+Layout});
            }
            catch
            {
                ThemeId=oldTheme;ScalePercent=oldScale;Layout=oldLayout;Apply();throw;
            }
        }

        public void ShowOptions()
        {
            using(Form dialog=new Form())
            using(Font font=new Font("Malgun Gothic",9f))
            {
                dialog.Text="툴바 테마";dialog.Name="ToolbarAppearanceOptions";
                dialog.Font=font;dialog.Size=new Size(420,305);dialog.FormBorderStyle=FormBorderStyle.FixedDialog;
                dialog.StartPosition=FormStartPosition.CenterScreen;dialog.MaximizeBox=false;dialog.MinimizeBox=false;dialog.TopMost=true;
                Label themeLabel=new Label {Text="테마",Location=new Point(24,25),AutoSize=true};
                ComboBox theme=new ComboBox {Name="ToolbarTheme",Location=new Point(135,22),Width=230,DropDownStyle=ComboBoxStyle.DropDownList};
                foreach(ToolbarTheme item in LoadThemes()) {theme.Items.Add(item);if(item.Id==ThemeId)theme.SelectedItem=item;}
                if(theme.SelectedIndex<0)theme.SelectedIndex=0;
                Label sizeLabel=new Label {Text="크기 (%)",Location=new Point(24,70),AutoSize=true};
                NumericUpDown size=new NumericUpDown {Name="ToolbarScale",Location=new Point(135,66),Width=100,Minimum=50,Maximum=150,Increment=10,Value=ScalePercent};
                Label layoutLabel=new Label {Text="배치",Location=new Point(24,112),AutoSize=true};
                ComboBox layout=new ComboBox {Name="ToolbarLayout",Location=new Point(135,108),Width=230,DropDownStyle=ComboBoxStyle.DropDownList};
                layout.Items.AddRange(new object[]{"가로 한 줄","가로 두 줄","세로 두 열"});layout.SelectedIndex=Vertical?2:(DoubleRow?1:0);
                Label hint=new Label {Text="테마와 배치를 각각 선택할 수 있습니다.",Location=new Point(24,157),AutoSize=true};
                Button apply=new Button {Name="ApplyAppearance",Text="적용",Location=new Point(205,206),Size=new Size(75,32)};
                Button cancel=new Button {Text="취소",DialogResult=DialogResult.Cancel,Location=new Point(290,206),Size=new Size(75,32)};
                apply.Click+=delegate {
                    try
                    {
                        SetPreferences(((ToolbarTheme)theme.SelectedItem).Id,(int)size.Value,layout.SelectedIndex==2?"vertical":(layout.SelectedIndex==1?"horizontal2":"horizontal"));
                        dialog.DialogResult=DialogResult.OK;dialog.Close();
                    }
                    catch(Exception error)
                    {
                        MessageBox.Show(dialog,"설정을 적용하지 못했습니다.\n"+error.Message,"툴바 모양",MessageBoxButtons.OK,MessageBoxIcon.Error);
                    }
                };
                dialog.Controls.AddRange(new Control[]{themeLabel,theme,sizeLabel,size,layoutLabel,layout,hint,apply,cancel});
                dialog.AcceptButton=apply;dialog.CancelButton=cancel;dialog.ShowDialog(root.FormCollection);
            }
        }
    }

    public partial class FormCollection
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(Point point);
        private Size appearanceBaseSize;
        private int appearanceBaseOrientation;
        private Button toolbarMoveGrip,toolbarCaps,toolbarRedo;
        private readonly List<Control> foldedTools=new List<Control>();
        private Size compactHeaderSize;
        private int headerCell,headerGrip,headerCaps,headerGap;
        private bool headerVertical;
        private bool automaticTipsSuppressed;
        private readonly Dictionary<Control,Rectangle> appearanceBaseBounds=new Dictionary<Control,Rectangle>();

        private void CaptureToolbarAppearanceBase()
        {
            // Disable every automatic hint, including explicit measurement Show calls.
            // The separate Alt-held key-tip window remains available.
            if(!automaticTipsSuppressed)
            {
                toolTip.Popup+=delegate(object sender,PopupEventArgs e){e.Cancel=true;};
                MetricToolTip.Popup+=delegate(object sender,PopupEventArgs e){e.Cancel=true;};
                automaticTipsSuppressed=true;
            }
            toolTip.Active=false;MetricToolTip.Active=false;
            Text=ArcDrawBranding.Name;
            if(toolbarMoveGrip==null||toolbarMoveGrip.IsDisposed)
            {
                toolbarMoveGrip=new Button {Name="ToolbarMoveGrip",Text="",Cursor=Cursors.SizeAll,TabStop=false};
                toolbarMoveGrip.MouseDown+=gpButtons_MouseDown;toolbarMoveGrip.MouseMove+=gpButtons_MouseMove;toolbarMoveGrip.MouseUp+=gpButtons_MouseUp;
                gpButtons.Controls.Add(toolbarMoveGrip);toolTip.SetToolTip(toolbarMoveGrip,"점 6개를 드래그하여 툴바 이동");
            }
            if(toolbarCaps==null||toolbarCaps.IsDisposed)
            {
                toolbarCaps=new Button {Name="ToolbarCapsState",Text="CAPS OFF",TabStop=false};
                toolbarCaps.Click+=delegate {if(Root.DrawingSession!=null)Root.DrawingSession.ToggleCaps();};
                gpButtons.Controls.Add(toolbarCaps);toolTip.SetToolTip(toolbarCaps,"Caps Lock · 그리기 / 일반 조작 전환");
            }
            if(toolbarRedo==null||toolbarRedo.IsDisposed)
            {
                toolbarRedo=new Button {Name="btRedo",AccessibleName="다시 실행"};
                if(btUndo.BackgroundImage!=null)
                {
                    var image=new Bitmap(btUndo.BackgroundImage);image.RotateFlip(RotateFlipType.RotateNoneFlipX);toolbarRedo.BackgroundImage=image;
                    toolbarRedo.Disposed+=delegate {image.Dispose();};
                }
                toolbarRedo.MouseDown+=gpButtons_MouseDown;toolbarRedo.MouseMove+=gpButtons_MouseMove;toolbarRedo.MouseUp+=gpButtons_MouseUp;
                toolbarRedo.Click+=delegate {if(ToolbarMoved){ToolbarMoved=false;return;}Root.RedoInk();};
                gpButtons.Controls.Add(toolbarRedo);toolTip.SetToolTip(toolbarRedo,"다시 실행 (Ctrl+Shift+Z)");
            }
            // Keep the two configured quick stamps; the full picker remains available.
            btClip3.Visible=false;
            btClip1.AccessibleName="체크";btClip2.AccessibleName="별표";
            toolbarRedo.Visible=Root.UndoEnabled;
            foldedTools.Clear();
            appearanceBaseSize=new Size(gpButtonsWidth,gpButtonsHeight);
            appearanceBaseOrientation=Root.ToolbarOrientation;
            appearanceBaseBounds.Clear();
            foreach(Control control in gpButtons.Controls)
            {
                appearanceBaseBounds[control]=control.Bounds;
                Button button=control as Button;
                if(button!=null)
                {
                    ToolbarDesign.Attach(button);
                    ToolbarDesign.SetHints(button,()=>ToolbarShortcut(button),()=>Root.Docked,()=>Root.ToolbarOrientation>Orientation.Horizontal);
                }
            }
        }

        public Size CompactDockSize { get { return compactHeaderSize; } }
        public void ToggleToolbarFold() { ToolbarMoved=false;btDock_Click(null,EventArgs.Empty); }

        private bool IsCompactHeader(Control control) {return control==btDock||control==toolbarMoveGrip||control==toolbarCaps;}
        private void RestoreFoldedTools()
        {
            foreach(Control control in foldedTools)if(!control.IsDisposed)control.Show();foldedTools.Clear();
        }
        internal void SetCompactHeaderGeometry(bool folded)
        {
            if(toolbarCaps==null)return;
            if(folded)
            {
                foreach(Control control in gpButtons.Controls)
                    if(!IsCompactHeader(control)&&control.Visible){foldedTools.Add(control);control.Hide();}
            }
            else RestoreFoldedTools();
            int x=folded||headerVertical?4:gpButtonsWidth-compactHeaderSize.Width+4;
            btDock.Bounds=new Rectangle(x,4,headerCell,headerCell);
            toolbarMoveGrip.Bounds=new Rectangle(btDock.Right+headerGap,4,headerGrip,headerCell);
            toolbarCaps.Bounds=new Rectangle(toolbarMoveGrip.Right+headerGap,4,headerCaps,headerCell);
        }
        internal void UpdateCompactStatus()
        {
            if(toolbarCaps==null||toolbarCaps.IsDisposed)return;
            bool on=Root.DrawingSession!=null&&Root.DrawingSession.CapsOn;
            string text=on?"CAPS ON":"CAPS OFF";
            ToolbarTheme theme=Root.DrawingSession==null?null:Root.DrawingSession.Appearance.CurrentTheme;
            Color color=on&&theme!=null?theme.Accent:Color.FromArgb(91,105,125);
            if(toolbarCaps.Text==text&&toolbarCaps.BackColor==color)return;
            toolbarCaps.Text=text;toolbarCaps.BackColor=color;toolbarCaps.ForeColor=Color.White;
            Root.UponButtonsUpdate|=2;
        }

        private string ToolbarShortcut(Button button)
        {
            if(button==btDock)return "TAB";
            if(button==toolbarCaps)return "CAPS";
            if(button==toolbarRedo)return "Ctrl+Shift+Z";
            Hotkey key=null;int pen;
            if(button.Name.StartsWith("pen")&&Int32.TryParse(button.Name.Substring(3),out pen)&&pen<Root.Hotkey_Pens.Length)key=Root.Hotkey_Pens[pen];
            else switch(button.Name)
            {
                case "btStop":key=Root.Hotkey_Close;break;case "btHand":key=Root.Hotkey_Hand;break;
                case "btText":key=Root.Hotkey_Text;break;case "btEraser":key=Root.Hotkey_Eraser;break;
                case "btLine":key=Root.Hotkey_Line;break;case "btRect":key=Root.Hotkey_Rect;break;
                case "btOval":key=Root.Hotkey_Oval;break;case "btArrow":key=Root.Hotkey_Arrow;break;
                case "btNumb":key=Root.Hotkey_Numb;break;case "btUndo":key=Root.Hotkey_Undo;break;
                case "btClear":key=Root.Hotkey_Clear;break;case "btPointer":key=Root.Hotkey_Pointer;break;
                case "btPan":key=Root.Hotkey_Pan;break;case "btLasso":key=Root.Hotkey_Lasso;break;
                case "btEdit":key=Root.Hotkey_Edit;break;case "btScaleRot":key=Root.Hotkey_ScaleRotate;break;
                case "btMagn":key=Root.Hotkey_Magnet;break;case "btZoom":key=Root.Hotkey_Zoom;break;
                case "btInkVisible":key=Root.Hotkey_InkVisible;break;case "btSnap":key=Root.Hotkey_Snap;break;
                case "btClipArt":key=Root.Hotkey_ClipArt;break;case "btClip1":key=Root.Hotkey_ClipArt1;break;
                case "btClip2":key=Root.Hotkey_ClipArt2;break;case "btClip3":key=Root.Hotkey_ClipArt3;break;
                case "btSave":key=Root.Hotkey_SaveStrokes;break;case "btLoad":key=Root.Hotkey_LoadStrokes;break;
                case "btPageNext":key=Root.Hotkey_PageNext;break;case "btPagePrev":key=Root.Hotkey_PagePrev;break;
                case "btVideo":key=Root.Hotkey_Video;break;
            }
            if(button==btClear&&(key==null||key.Key<=0))return "⇧Del";
            return ToolbarDesign.Shortcut(key);
        }

        internal void ApplyToolbarAppearance(ToolbarTheme theme,int percent)
        {
            if(appearanceBaseBounds.Count==0)CaptureToolbarAppearanceBase();
            RestoreFoldedTools();
            bool vertical=Root.DrawingSession!=null&&Root.DrawingSession.Appearance.Vertical;
            bool twoRows=Root.DrawingSession!=null&&Root.DrawingSession.Appearance.DoubleRow;
            Rectangle screen=Screen.FromControl(this).WorkingArea;
            List<Control> buttons=new List<Control>();
            foreach(Control control in appearanceBaseBounds.Keys)if(control.Visible&&!IsCompactHeader(control))buttons.Add(control);
            buttons.Sort(delegate(Control a,Control b) {
                Rectangle aa=appearanceBaseBounds[a],bb=appearanceBaseBounds[b];
                int major=appearanceBaseOrientation<=Orientation.Horizontal?aa.X.CompareTo(bb.X):aa.Y.CompareTo(bb.Y);
                return major!=0?major:(appearanceBaseOrientation<=Orientation.Horizontal?aa.Y.CompareTo(bb.Y):aa.X.CompareTo(bb.X));
            });
            if(buttons.Remove(toolbarRedo))buttons.Insert(Math.Max(0,buttons.IndexOf(btUndo)+1),toolbarRedo);
            int cell=Math.Max(20,(int)Math.Round(34*percent/100f)),gap=Math.Max(1,theme.Gap),pad=4;
            int rows=vertical?(buttons.Count+1)/2+1:(twoRows?2:1);
            int columns=vertical?2:(twoRows?(buttons.Count+1)/2:buttons.Count);
            int extra=theme.Labels?(int)Math.Round(36*percent/100f):0;
            if(vertical)cell=Math.Min(cell,Math.Max(20,(screen.Height-200)/Math.Max(1,rows)-gap));
            else
            {
                // Leave room for the Caps/layer controls on the same row.
                int available=Math.Max(200,screen.Width-350-2*pad);
                int desired=(columns+2)*(cell+extra+gap);
                if(desired>available) {float fit=available/(float)desired;cell=Math.Max(18,(int)(cell*fit));extra=(int)(extra*fit);}
            }
            int buttonHeight=cell;
            int[] columnWidths=new int[Math.Max(1,columns)];
            for(int i=0;i<columnWidths.Length;i++)columnWidths[i]=cell+extra;
            headerCell=cell;headerGrip=Math.Max(22,(int)(24*percent/100f));headerCaps=Math.Max(82,(int)(90*percent/100f));headerGap=gap;headerVertical=vertical;
            compactHeaderSize=new Size(cell+headerGrip+headerCaps+2*gap+2*pad,cell+2*pad);
            if(vertical)columnWidths[0]=columnWidths[1]=Math.Max(cell+extra,(compactHeaderSize.Width-2*pad+1)/2);
            int[] columnLeft=new int[columnWidths.Length];int toolsWidth=0;
            for(int i=0;i<columnWidths.Length;i++){columnLeft[i]=toolsWidth;toolsWidth+=columnWidths[i]+gap;}
            int headerWidth=compactHeaderSize.Width;
            int width=toolsWidth-gap+2*pad+(vertical?0:headerWidth);
            int height=rows*(buttonHeight+gap)-gap+2*pad;
            gpButtons.SuspendLayout();
            try
            {
                btDock.BackColor=theme.Surface;btDock.ForeColor=theme.Text;
                toolbarMoveGrip.BackColor=theme.Surface;toolbarMoveGrip.ForeColor=theme.Text;
                ToolbarDesign.SetTheme(btDock,theme);ToolbarDesign.SetTheme(toolbarMoveGrip,theme);ToolbarDesign.SetTheme(toolbarCaps,theme);
                for(int i=0;i<buttons.Count;i++)
                {
                    Control control=buttons[i];
                    int column=vertical?i%2:(twoRows?i/2:i),row=vertical?i/2+1:(twoRows?i%2:0);
                    control.Bounds=new Rectangle(pad+columnLeft[column],pad+row*(buttonHeight+gap),columnWidths[column],buttonHeight);
                    control.BackColor=theme.Surface;control.ForeColor=theme.Text;
                    Button button=control as Button;if(button!=null)ToolbarDesign.SetTheme(button,theme,theme.Labels);
                }
                gpButtons.BackColor=theme.Background;
                gpButtonsWidth=Math.Max(1,width);
                gpButtonsHeight=Math.Max(1,height);
                SetCompactHeaderGeometry(false);UpdateCompactStatus();
                gpButtons.Size=new Size(gpButtonsWidth,gpButtonsHeight);
                gpButtonsLeft=Math.Max(screen.Left-Left,Math.Min(gpButtonsLeft,screen.Right-Left-gpButtonsWidth));
                gpButtonsTop=Math.Max(screen.Top-Top,Math.Min(gpButtonsTop,screen.Bottom-Top-gpButtonsHeight));
                gpButtons.Location=new Point(gpButtonsLeft,gpButtonsTop);
                gpSubTools.Visible=false;gpPenWidth.Visible=false;
                VisibleToolbar=new Size(gpButtonsWidth,gpButtonsHeight);
                SetWindowInputRectFlag=true;
                if(Root.DrawingSession!=null&&Root.DrawingSession.Running)Root.UnDock();
            }
            finally { gpButtons.ResumeLayout(); }
        }

        internal void ClearScreenOnly() { ToolbarMoved=false;ClearScreenCore(null,EventArgs.Empty); }
        internal void ResetScreenSelectionForNote()
        {
            // A selection left on the screen must not be recolored/deleted by
            // choosing a note tool. Remove selection references, never ink.
            InprogressSelection=null;StrokesSelection.Clear();Root.UponAllDrawingUpdate=true;
        }
    }
}
