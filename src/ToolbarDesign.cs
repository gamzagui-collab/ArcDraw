using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace gInk
{
    // Paint existing controls; their original Click/drag/right-click handlers and
    // BackgroundImage selection remain the source of tool behavior and state.
    internal static class ToolbarDesign
    {
        internal static List<ToolbarTheme> BuiltInThemes()
        {
            return new List<ToolbarTheme> {
                Theme("paper","페이퍼 바","#F8FAFC","#FFFFFF","#243044","#2563EB",5,2,true),
                Theme("midnight","미드나이트","#202632","#303846","#EDF2FA","#548D71",5,3,false),
                Theme("cream","소프트 크림","#F5EEDF","#FFFAF0","#4E4134","#7361AB",16,5,false),
                Theme("mono","모노라인","#FFFFFF","#FFFFFF","#161616","#161616",0,1,true),
                Theme("studio","스튜디오 패널","#E6E9ED","#F9FAFC","#28323E","#315F9D",3,4,true),
                Theme("neon","네온 도크","#161B23","#242D3A","#E5EEFC","#21848B",10,3,false),
                Theme("rail","세로 레일","#F7F9FC","#FFFFFF","#334358","#347C74",7,2,true),
                Theme("pods","분리형 포드","#ECE9F4","#FFFFFF","#343C50","#6652A4",12,6,true),
                Theme("blue","블루 글래스","#E7F1FC","#F7FBFF","#234565","#276FAA",9,3,true),
                Theme("ribbon","라벨 리본","#EEEEE7","#FFFFFF","#41413A","#747B40",3,2,true,true)
            };
        }
        private static ToolbarTheme Theme(string id,string name,string bg,string surface,string text,string accent,int radius,int gap,bool border,bool labels=false)
        {
            return new ToolbarTheme {Id=id,Name=name,Background=ColorTranslator.FromHtml(bg),Surface=ColorTranslator.FromHtml(surface),
                Text=ColorTranslator.FromHtml(text),Accent=ColorTranslator.FromHtml(accent),Radius=radius,Gap=gap,Border=border,Labels=labels};
        }
        private sealed class Skin
        {
            public ToolbarTheme Theme;public bool Labels,Hover;public Image Original;public Bitmap Tinted;
            public Func<string> Hint;public Func<bool> Folded,Vertical;
        }
        private static readonly ConditionalWeakTable<Button,Skin> skins=new ConditionalWeakTable<Button,Skin>();
        internal static string HintFor(Button button) { Skin skin;return skins.TryGetValue(button,out skin)&&skin.Hint!=null?skin.Hint():""; }
        internal static void SetHints(Button button,Func<string> hint,Func<bool> folded=null,Func<bool> vertical=null)
        {
            Attach(button);Skin skin=skins.GetValue(button,b=>new Skin());skin.Hint=hint;skin.Folded=folded;skin.Vertical=vertical;
        }
        internal static string Shortcut(Hotkey hotkey)
        {
            if(hotkey==null||hotkey.Key<=0)return "";
            string key=((Keys)hotkey.Key).ToString();
            if(hotkey.Key>=(int)Keys.D0&&hotkey.Key<=(int)Keys.D9)key=((char)('0'+hotkey.Key-(int)Keys.D0)).ToString();
            if(key=="Escape")key="ESC";else if(key=="Delete")key="Del";else if(key=="Tab")key="TAB";
            else if(key=="Multiply")key="Num*";else if(key=="Divide")key="Num/";else if(key=="Subtract")key="Num-";
            else if(key=="Add")key="Num+";else if(key=="Decimal")key="Num.";
            else if(key.StartsWith("NumPad"))key=key.Replace("NumPad","Num");
            return (hotkey.Control?"Ctrl+":"")+(hotkey.Alt?"Alt+":"")+(hotkey.Shift?"Shift+":"")+(hotkey.Win?"Win+":"")+key;
        }
        internal static void Attach(Button button)
        {
            Skin found;if(skins.TryGetValue(button,out found))return;
            var skin=new Skin();skins.Add(button,skin);
            button.Paint+=Paint;
            button.MouseEnter+=delegate {skin.Hover=true;button.Invalidate();};
            button.MouseLeave+=delegate {skin.Hover=false;button.Invalidate();};
            button.Disposed+=delegate {if(skin.Tinted!=null)skin.Tinted.Dispose();};
        }
        internal static void SetTheme(Button button,ToolbarTheme theme,bool labels=false)
        {
            Attach(button);Skin skin=skins.GetValue(button,b=>new Skin());
            if(skin.Tinted!=null){skin.Tinted.Dispose();skin.Tinted=null;}
            skin.Original=null;skin.Theme=theme;skin.Labels=labels;
            button.UseVisualStyleBackColor=false;button.FlatStyle=FlatStyle.Flat;
            button.FlatAppearance.BorderSize=0;button.Invalidate();
        }
        private static GraphicsPath Rounded(Rectangle bounds,int radius)
        {
            GraphicsPath path=new GraphicsPath();int d=Math.Min(radius*2,Math.Min(bounds.Width,bounds.Height));
            if(d<=1){path.AddRectangle(bounds);return path;}
            path.AddArc(bounds.Left,bounds.Top,d,d,180,90);path.AddArc(bounds.Right-d,bounds.Top,d,d,270,90);
            path.AddArc(bounds.Right-d,bounds.Bottom-d,d,d,0,90);path.AddArc(bounds.Left,bounds.Bottom-d,d,d,90,90);path.CloseFigure();return path;
        }
        private static Color Blend(Color a,Color b,float amount)
        {
            return Color.FromArgb((int)(a.R+(b.R-a.R)*amount),(int)(a.G+(b.G-a.G)*amount),(int)(a.B+(b.B-a.B)*amount));
        }
        private static string Label(Button b)
        {
            if(b.Name=="penMainColor")return b.Text;
            if(b.Name.StartsWith("pen"))return b.Name.Replace("pen","펜");
            switch(b.Name)
            {
                case "btDock":return "접기";case "btHand":return "펜";case "btLine":return "직선";
                case "btRect":return "사각";case "btOval":return "원";case "btArrow":return "화살표";
                case "btNumb":return "번호";case "btText":return "텍스트";case "btEraser":return "지우개";
                case "btUndo":return "취소";case "btRedo":return "다시";case "btClear":return "삭제";case "btStop":return "종료";
                case "btPointer":return "포인터";case "btSnap":return "캡처";case "btPenWidth":return "굵기";
                case "btInkVisible":return "표시";case "btLasso":return "선택";case "btPan":return "이동";
                case "btEdit":return "편집";case "btScaleRot":return "회전";case "btSave":return "저장";
                case "btLoad":return "열기";case "btPageNext":return "다음";case "btPagePrev":return "이전";
                case "btClipArt":return "스탬프";case "btClip1":return "체크";case "btClip2":return "별표";case "btMagn":return "자석";case "btZoom":return "확대";
                default:return b.Text;
            }
        }
        private static Image Icon(Button button,Skin skin)
        {
            Image source=button.BackgroundImage;if(source==null)return null;
            if(button.Name.StartsWith("pen"))return source; // Keep actual pen colors exact.
            if(ReferenceEquals(source,skin.Original)&&skin.Tinted!=null)return skin.Tinted;
            if(skin.Tinted!=null)skin.Tinted.Dispose();
            skin.Original=source;skin.Tinted=new Bitmap(source);
            for(int y=0;y<skin.Tinted.Height;y++)for(int x=0;x<skin.Tinted.Width;x++)
            {
                Color p=skin.Tinted.GetPixel(x,y);
                if(p.A==0||Math.Max(p.R,Math.Max(p.G,p.B))-Math.Min(p.R,Math.Min(p.G,p.B))>28)continue;
                Color tint=Blend(skin.Theme.Text,skin.Theme.Surface,p.R/255f);
                skin.Tinted.SetPixel(x,y,Color.FromArgb(p.A,tint));
            }
            return skin.Tinted;
        }
        private static void Paint(object sender,PaintEventArgs e)
        {
            Button button=(Button)sender;Skin skin;
            if(!skins.TryGetValue(button,out skin)||skin.Theme==null)return;
            ToolbarTheme t=skin.Theme;Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
            Color surface=skin.Hover?Blend(t.Surface,t.Accent,.13f):button.BackColor;
            if(surface==Color.Transparent)surface=t.Surface;
            g.Clear(button.Parent==null?t.Background:button.Parent.BackColor);
            Rectangle bounds=new Rectangle(1,1,Math.Max(1,button.Width-3),Math.Max(1,button.Height-3));
            using(GraphicsPath shape=Rounded(bounds,t.Radius))
            {
                using(var brush=new SolidBrush(surface))g.FillPath(brush,shape);
                if(t.Border||skin.Hover)using(var pen=new Pen(Blend(t.Surface,t.Text,.22f)))g.DrawPath(pen,shape);
            }
            string shortcut=skin.Hint==null?"":skin.Hint();
            int bodyHeight=button.Height;
            bool fold=button.Name=="btDock",grip=button.Name=="ToolbarMoveGrip";
            Image image=fold||grip?null:Icon(button,skin);int inset=5;
            Rectangle textArea=new Rectangle(2,1,Math.Max(1,button.Width-4),Math.Max(1,bodyHeight-2));
            if(fold)
            {
                float x=button.Width/2f,y=bodyHeight/2f;
                bool closed=skin.Folded!=null&&skin.Folded(),vertical=skin.Vertical!=null&&skin.Vertical();
                using(var pen=new Pen(button.ForeColor,1.8f) {StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})
                {
                    if(vertical)g.DrawLines(pen,closed?new[]{new PointF(x-5,y-2),new PointF(x,y+3),new PointF(x+5,y-2)}:new[]{new PointF(x-5,y+2),new PointF(x,y-3),new PointF(x+5,y+2)});
                    else g.DrawLines(pen,closed?new[]{new PointF(x+2,y-5),new PointF(x-3,y),new PointF(x+2,y+5)}:new[]{new PointF(x-2,y-5),new PointF(x+3,y),new PointF(x-2,y+5)});
                }
            }
            if(grip)
            {
                using(var brush=new SolidBrush(button.ForeColor))
                    for(int row=0;row<3;row++)for(int col=0;col<2;col++)
                        g.FillEllipse(brush,button.Width/2f-4+col*5,bodyHeight/2f-6.5f+row*5,2.5f,2.5f);
            }
            if(image!=null)
            {
                bool pen=button.Name.StartsWith("pen");
                int side=Math.Max(10,Math.Min(bodyHeight-2*inset,skin.Labels?22:button.Width-2*inset));
                Rectangle icon=new Rectangle(skin.Labels?inset:(button.Width-side)/2,(bodyHeight-side)/2,side,side);
                if(pen&&t.Surface.GetBrightness()<.5f)using(var brush=new SolidBrush(Color.White))g.FillRectangle(brush,icon);
                g.DrawImage(image,icon);
                if(skin.Labels)textArea=new Rectangle(icon.Right+3,0,Math.Max(1,button.Width-icon.Right-5),bodyHeight);
            }
            if(!fold&&!grip&&(image==null||skin.Labels))
            {
                string text=image==null?button.Text:Label(button);
                // Layout dimensions are device pixels. Point fonts would be
                // scaled again by Windows and truncate compact labels.
                using(var font=new Font("Malgun Gothic",skin.Labels?11f:12f,FontStyle.Regular,GraphicsUnit.Pixel))
                    TextRenderer.DrawText(g,text,font,textArea,button.ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding|
                        (button.UseMnemonic?TextFormatFlags.Default:TextFormatFlags.NoPrefix));
            }
            if(shortcut!="")
            {
                if(button.AccessibleDescription!=shortcut)button.AccessibleDescription=shortcut;
            }
            if(t.Id=="neon")using(var pen=new Pen(t.Accent,2))g.DrawLine(pen,4,button.Height-2,button.Width-4,button.Height-2);
            if(button.Focused)ControlPaint.DrawFocusRectangle(g,Rectangle.Inflate(bounds,-2,-2),t.Text,surface);
        }
    }
}
