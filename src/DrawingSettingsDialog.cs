using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Microsoft.Ink;

namespace gInk
{
    internal sealed class DrawingSettingsDialog : Form
    {
        private readonly Root root;
        private readonly DrawingSettings settings;
        private readonly ComboBox slots=new ComboBox(),tools=new ComboBox(),fills=new ComboBox(),tips=new ComboBox(),lines=new ComboBox();
        private readonly TextBox presetName=new TextBox();
        private readonly NumericUpDown width=Number("PresetWidth",.5m,200,1),height=Number("PresetHeight",.5m,200,1),opacity=Number("PresetOpacity",1,100,0);
        private readonly Button color=new Button();
        private readonly Label presetStatus=new Label();
        private readonly string[] lineValues={"Stroke","Solid","Dash","Dot","DashDot","DashDotDot"};
        private DrawingPreset editing;
        private Font chosenFont;
        private Label fontPreview;
        private sealed class Slot
        {
            internal int Index;internal DrawingSettings Settings;
            public override string ToString(){return Settings.GetPreset(Index).Name+" ["+Settings.Shortcut(Index)+"]";}
        }
        internal DrawingSettingsDialog(Root root)
        {
            this.root=root;settings=root.Settings;
            Name="ArcDrawSettings";Text="ArcDraw 설정";ClientSize=new Size(600,470);
            Font=new Font("Malgun Gothic",9f);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
            StartPosition=FormStartPosition.CenterScreen;TopMost=true;
            var tabs=new TabControl {Name="SettingsTabs",Bounds=new Rectangle(12,12,576,398)};
            var penPage=new TabPage("펜 프리셋");var fontPage=new TabPage("글꼴");var themePage=new TabPage("툴바");
            tabs.TabPages.AddRange(new[]{penPage,fontPage,themePage});Controls.Add(tabs);
            BuildPenPage(penPage);BuildFontPage(fontPage);BuildThemePage(themePage);
            var close=new Button {Name="CloseSettings",Text="닫기",Bounds=new Rectangle(488,426,100,30),DialogResult=DialogResult.Cancel};
            Controls.Add(close);CancelButton=close;
            Controls.Add(new Label {Text="각 탭의 저장·적용 버튼을 눌러 반영합니다.",AutoSize=true,Location=new Point(16,434)});
        }
        private static NumericUpDown Number(string name,decimal min,decimal max,int decimals)
        {return new NumericUpDown {Name=name,Minimum=min,Maximum=max,DecimalPlaces=decimals,Increment=decimals>0?.5m:1};}
        private static void LabelAt(Control page,string text,int x,int y){page.Controls.Add(new Label {Text=text,AutoSize=true,Location=new Point(x,y+5)});}
        private static void Place(Control page,Control c,string name,int x,int y,int w=130,int h=28){c.Name=name;c.Bounds=new Rectangle(x,y,w,h);page.Controls.Add(c);}
        private static void Choice(ComboBox box,params object[] values){box.DropDownStyle=ComboBoxStyle.DropDownList;box.Items.AddRange(values);box.SelectedIndex=0;}
        private decimal Pixels(float ink){return Math.Max(.5m,Math.Min(200,(decimal)(ink*1000f/root.PixelToHiMetric(1000))));}
        private float Ink(decimal pixels){return (float)pixels*root.PixelToHiMetric(1000)/1000f;}
        private void BuildPenPage(TabPage page)
        {
            LabelAt(page,"프리셋",20,14);Place(page,slots,"PresetSlot",140,14,390);slots.DropDownStyle=ComboBoxStyle.DropDownList;
            // Present the existing digit shortcuts in familiar order without changing them.
            for(int key=1;key<=10;key++)for(int n=0;n<Root.MaxDisplayedPens;n++)if(root.PenEnabled[n]&&root.Hotkey_Pens[n].Key==(int)Keys.D0+key%10)slots.Items.Add(new Slot {Index=n,Settings=settings});
            for(int n=0;n<Root.MaxDisplayedPens;n++)if(root.PenEnabled[n])
            {bool exists=false;foreach(Slot slot in slots.Items)if(slot.Index==n)exists=true;if(!exists)slots.Items.Add(new Slot {Index=n,Settings=settings});}
            LabelAt(page,"이름",20,54);Place(page,presetName,"PresetName",140,54,390);presetName.MaxLength=40;
            LabelAt(page,"도구",20,94);Place(page,tools,"PresetTool",140,94,170);Choice(tools,"자유펜","직선","사각형","원/타원","시작 화살표","끝 화살표","연속선");
            Place(page,fills,"PresetFill",330,94,200);Choice(fills,"윤곽선","펜 색으로 채움","바깥 채움","흰색 채움","검정 채움");
            LabelAt(page,"색상",20,134);Place(page,color,"PresetColor",140,134,135);
            LabelAt(page,"불투명도 %",296,134);Place(page,opacity,"PresetOpacity",425,134,105);
            LabelAt(page,"굵기 px",20,174);Place(page,width,"PresetWidth",140,174,105);
            LabelAt(page,"촉 높이 px",296,174);Place(page,height,"PresetHeight",425,174,105);
            LabelAt(page,"펜촉",20,214);Place(page,tips,"PresetTip",140,214,135);Choice(tips,"원형","사각형");
            LabelAt(page,"선 모양",296,214);Place(page,lines,"PresetLine",425,214,105);Choice(lines,"자유선","실선","파선","점선","일점쇄선","이점쇄선");
            var current=new Button {Text="현재 도구 가져오기"};Place(page,current,"CaptureCurrentPreset",20,266,180,32);
            var save=new Button {Text="프리셋 저장"};Place(page,save,"SaveDrawingPreset",350,266,180,32);
            Place(page,presetStatus,"PresetStatus",20,310,515,50);presetStatus.Text="표시된 기존 단축키로 호출합니다. CAPS OFF·텍스트 편집 중에는 가로채지 않습니다.";
            slots.SelectedIndexChanged+=delegate {LoadPreset(settings.GetPreset(((Slot)slots.SelectedItem).Index));};
            tips.SelectedIndexChanged+=delegate {height.Enabled=tips.SelectedIndex==1;};
            tools.SelectedIndexChanged+=delegate {fills.Enabled=tools.SelectedIndex==2||tools.SelectedIndex==3||tools.SelectedIndex==6;};
            color.Click+=delegate {using(var picker=new ColorDialog {Color=color.BackColor,FullOpen=true})if(picker.ShowDialog(this)==DialogResult.OK)SetColor(picker.Color);};
            current.Click+=delegate {
                int slot=((Slot)slots.SelectedItem).Index;
                LoadPreset(new DrawingPreset {Slot=slot,Name=presetName.Text,Attributes=root.FormCollection.IC.DefaultDrawingAttributes.Clone(),
                    Tool=Array.IndexOf(DrawingSettings.PresetTools,root.ToolSelected)>=0?root.ToolSelected:Tools.Hand,Fill=Math.Max(0,Math.Min(4,root.FilledSelected))});
                presetStatus.Text="현재 색·굵기·펜촉·도구를 가져왔습니다. 저장 버튼으로 확정하세요.";
            };
            save.Click+=delegate {TrySave(delegate {
                DrawingPreset value=editing.Clone();value.Name=string.IsNullOrWhiteSpace(presetName.Text)?"프리셋 "+settings.Shortcut(value.Slot):presetName.Text.Trim();
                value.Tool=DrawingSettings.PresetTools[tools.SelectedIndex];value.Fill=fills.SelectedIndex;
                value.Attributes.Color=color.BackColor;value.Attributes.Width=Ink(width.Value);value.Attributes.Height=Ink(height.Value);
                value.Attributes.PenTip=tips.SelectedIndex==0?PenTip.Ball:PenTip.Rectangle;value.Attributes.Transparency=(byte)Math.Round(255*(100-opacity.Value)/100);
                if(value.Attributes.ExtendedProperties.Contains(Root.DASHED_LINE_GUID))value.Attributes.ExtendedProperties.Remove(Root.DASHED_LINE_GUID);
                if(lines.SelectedIndex>0)value.Attributes.ExtendedProperties.Add(Root.DASHED_LINE_GUID,root.LineStyleFromString(lineValues[lines.SelectedIndex]));
                settings.SavePreset(value);editing=value.Clone();presetStatus.Text="저장됨 · "+settings.Shortcut(value.Slot)+" 키로 불러오기";
                int selected=slots.SelectedIndex;slots.Items[selected]=slots.Items[selected];
            });};
            if(slots.Items.Count>0)slots.SelectedIndex=0;else {current.Enabled=false;save.Enabled=false;}
        }
        private void SetColor(Color value)
        {color.BackColor=Color.FromArgb(value.R,value.G,value.B);color.ForeColor=value.GetBrightness()<.5?Color.White:Color.Black;color.Text="#"+value.R.ToString("X2")+value.G.ToString("X2")+value.B.ToString("X2");}
        private void LoadPreset(DrawingPreset value)
        {
            editing=value.Clone();presetName.Text=value.Name;tools.SelectedIndex=Math.Max(0,Array.IndexOf(DrawingSettings.PresetTools,value.Tool));fills.SelectedIndex=Math.Max(0,Math.Min(4,value.Fill));
            SetColor(value.Attributes.Color);width.Value=Pixels(value.Attributes.Width);height.Value=Pixels(value.Attributes.Height);
            tips.SelectedIndex=value.Attributes.PenTip==PenTip.Rectangle?1:0;height.Enabled=tips.SelectedIndex==1;
            opacity.Value=Math.Max(1,Math.Min(100,(decimal)Math.Round((255-value.Attributes.Transparency)*100.0/255)));
            lines.SelectedIndex=Math.Max(0,Array.IndexOf(lineValues,root.LineStyleToString(value.Attributes.ExtendedProperties)));
        }
        private void BuildFontPage(TabPage page)
        {
            chosenFont=settings.CreateNoteFont();
            page.Controls.Add(new Label {Text="화면 T·배치 글자의 기본 글꼴입니다. 노트 메모는 제목 ✎에서 설정합니다.",AutoSize=true,Location=new Point(24,24)});
            fontPreview=new Label {Name="TextFontPreview",Text="한글 글꼴 미리보기  Aa 123",Bounds=new Rectangle(24,68,510,90),Font=chosenFont,BorderStyle=BorderStyle.FixedSingle,TextAlign=ContentAlignment.MiddleLeft};page.Controls.Add(fontPreview);
            var description=new Label {Name="TextFontDescription",Bounds=new Rectangle(24,166,510,28),Text=FontDescription()};page.Controls.Add(description);
            var choose=new Button {Name="ChooseTextFont",Text="글꼴 선택…",Bounds=new Rectangle(24,210,150,34)};page.Controls.Add(choose);
            WhiteboardLayerForm target=root.Whiteboards.ActiveNote;
            var existing=new CheckBox {Name="ApplyFontToNote",Text="선택한 노트의 기존 배치 글자에도 적용 (메모 제외)",Bounds=new Rectangle(24,264,470,28),Enabled=target!=null};page.Controls.Add(existing);
            var save=new Button {Name="SaveTextFont",Text="글꼴 저장",Bounds=new Rectangle(350,310,180,34)};page.Controls.Add(save);
            var status=new Label {Name="TextFontStatus",Bounds=new Rectangle(24,315,315,35)};page.Controls.Add(status);
            choose.Click+=delegate {using(var picker=new FontDialog {Font=chosenFont,MinSize=6,MaxSize=144,ShowEffects=false})if(picker.ShowDialog(this)==DialogResult.OK){Font old=chosenFont;chosenFont=(Font)picker.Font.Clone();fontPreview.Font=chosenFont;old.Dispose();description.Text=FontDescription();}};
            save.Click+=delegate {TrySave(delegate {settings.SaveFont(chosenFont);if(existing.Checked&&target!=null&&!target.IsDisposed&&!target.ApplyTextFont(chosenFont))throw new InvalidOperationException("노트 입력이 끝난 뒤 다시 적용해 주세요.");status.Text="글꼴을 저장했습니다.";});};
        }
        private string FontDescription(){return chosenFont.Name+" · "+chosenFont.SizeInPoints.ToString("0.#",CultureInfo.CurrentCulture)+"pt · "+chosenFont.Style;}
        private void BuildThemePage(TabPage page)
        {
            ToolbarAppearance appearance=root.DrawingSession.Appearance;
            var theme=new ComboBox {Name="ToolbarTheme",DropDownStyle=ComboBoxStyle.DropDownList};Place(page,theme,theme.Name,150,35,365);
            foreach(ToolbarTheme item in appearance.LoadThemes()){theme.Items.Add(item);if(item.Id==appearance.ThemeId)theme.SelectedItem=item;}
            if(theme.SelectedIndex<0)theme.SelectedIndex=0;
            var scale=Number("ToolbarScale",50,150,0);scale.Increment=10;scale.Value=appearance.ScalePercent;Place(page,scale,scale.Name,150,92,120);
            var layout=new ComboBox();Place(page,layout,"ToolbarLayout",150,149,365);Choice(layout,"가로 한 줄","가로 두 줄","세로 두 열");layout.SelectedIndex=appearance.Vertical?2:appearance.DoubleRow?1:0;
            LabelAt(page,"테마",24,35);LabelAt(page,"크기 %",24,92);LabelAt(page,"배치",24,149);
            var save=new Button {Name="ApplyAppearance",Text="툴바 적용",Bounds=new Rectangle(350,240,165,34)};page.Controls.Add(save);
            var status=new Label {Name="AppearanceStatus",Bounds=new Rectangle(24,285,490,35)};page.Controls.Add(status);
            save.Click+=delegate {TrySave(delegate {appearance.SetPreferences(((ToolbarTheme)theme.SelectedItem).Id,(int)scale.Value,layout.SelectedIndex==2?"vertical":layout.SelectedIndex==1?"horizontal2":"horizontal");status.Text="툴바 설정을 저장했습니다.";});};
        }
        private void TrySave(Action action)
        {try {action();}catch(Exception error){MessageBox.Show(this,"설정을 저장하지 못했습니다.\n"+error.Message,"ArcDraw 설정",MessageBoxButtons.OK,MessageBoxIcon.Error);}}
        protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing&&chosenFont!=null){chosenFont.Dispose();chosenFont=null;}}
    }
}
