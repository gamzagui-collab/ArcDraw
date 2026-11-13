using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using System.Xml.Linq;
using Microsoft.Ink;

namespace gInk
{
    public sealed class DrawingPreset
    {
        public int Slot,Tool=Tools.Hand,Fill=Filling.Empty;
        public string Name;
        public DrawingAttributes Attributes;
        public DrawingPreset Clone(){return new DrawingPreset {Slot=Slot,Tool=Tool,Fill=Fill,Name=Name,Attributes=Attributes.Clone()};}
    }

    public sealed class DrawingSettings
    {
        private readonly Root root;
        private readonly Dictionary<int,DrawingPreset> presets=new Dictionary<int,DrawingPreset>();
        private readonly HashSet<string> favoriteFonts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly CultureInfo Invariant=CultureInfo.InvariantCulture;
        public static readonly int[] PresetTools={Tools.Hand,Tools.Line,Tools.Rect,Tools.Oval,Tools.StartArrow,Tools.EndArrow,Tools.Poly};
        public bool HasFont {get;private set;}
        public string FontFamily {get;private set;}="Malgun Gothic";
        public float FontPoints {get;private set;}=17f;
        public FontStyle FontStyle {get;private set;}=FontStyle.Regular;
        internal Color? ScreenTextColor { get; private set; }
        internal Color? BrushColor { get; private set; }
        internal string ScreenTextFamily { get; private set; }
        private string FileName {get{return Path.Combine(Program.RunningFolder,"drawing-settings.xml");}}
        public DrawingSettings(Root root)
        {
            this.root=root;
            if(File.Exists(FileName))
            {
                try {Load(FileName);}
                catch(Exception error) when(error is IOException || error is System.Xml.XmlException || error is ArgumentException || error is FormatException || error is OverflowException)
                {
                    ResetLoadedSettings();
                    if(File.Exists(FileName+".bak"))try {Load(FileName+".bak");}catch {ResetLoadedSettings();}
                }
            }
            foreach(DrawingPreset preset in presets.Values)root.PenAttr[preset.Slot]=preset.Attributes.Clone();
            if(HasFont)ApplyFontDefaults();
        }
        private void ResetLoadedSettings()
        {
            presets.Clear();
            favoriteFonts.Clear();
            HasFont = false;
            ScreenTextColor = null;
            BrushColor = null;
            ScreenTextFamily = null;
        }

        internal void SetToolColor(Color color, bool text)
        {
            Color? previous = text ? ScreenTextColor : BrushColor;
            if (previous?.ToArgb() == color.ToArgb()) return;
            if (text) ScreenTextColor = color;
            else BrushColor = color;
            SaveToolSettings();
        }

        internal void SetScreenTextFamily(string family)
        {
            if (ScreenTextFamily == family) return;
            ScreenTextFamily = family;
            SaveToolSettings();
        }

        internal bool IsFavoriteFont(string family)
        {
            return favoriteFonts.Contains(family);
        }

        internal void ToggleFavoriteFont(string family)
        {
            if (!favoriteFonts.Remove(family)) favoriteFonts.Add(family);
            SaveToolSettings();
        }

        private void SaveToolSettings()
        {
            try { Save(); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                root.trayIcon?.ShowBalloonTip(4500, "ArcDraw", "도구 설정을 저장하지 못했습니다.", ToolTipIcon.Warning);
            }
        }
        public bool HasPreset(int slot){return presets.ContainsKey(slot);}
        public string Shortcut(int slot)
        {
            string text=root.Hotkey_Pens[slot].ToStringInvariant();
            for(int n=0;n<=9;n++)text=text.Replace("D"+n,n.ToString(Invariant));
            return text;
        }
        public DrawingPreset GetPreset(int slot)
        {
            DrawingPreset value;
            return presets.TryGetValue(slot,out value)?value.Clone():new DrawingPreset {Slot=slot,Name="프리셋 "+Shortcut(slot),Attributes=root.PenAttr[slot].Clone()};
        }
        public void SavePreset(DrawingPreset value)
        {
            Validate(value);DrawingPreset old;presets.TryGetValue(value.Slot,out old);
            presets[value.Slot]=value.Clone();
            try {Save();}catch {if(old==null)presets.Remove(value.Slot);else presets[value.Slot]=old;throw;}
            root.PenAttr[value.Slot]=value.Attributes.Clone();
            if(root.FormCollection!=null)root.FormCollection.RefreshPresetButtons(value.Slot);
        }
        public void RecallPreset(int slot)
        {
            DrawingPreset value;if(!presets.TryGetValue(slot,out value)||!root.PenEnabled[slot])return;
            root.PenAttr[slot]=value.Attributes.Clone();
            root.FormCollection.SelectPen(slot);
            root.FormCollection.SelectTool(value.Tool,value.Fill);
        }
        public int MatchShortcut(int key,bool ctrl,bool alt,bool shift,bool win)
        {
            foreach(DrawingPreset preset in presets.Values)
            {
                int slot=preset.Slot;Hotkey hotkey=root.Hotkey_Pens[slot];
                if(!root.PenEnabled[slot]||hotkey.Key!=key||!hotkey.ModifierMatch(ctrl,alt,shift,win))continue;
                // Keep the original cycling behavior for intentionally shared pen keys.
                for(int n=0;n<Root.MaxDisplayedPens;n++)if(n!=slot&&root.PenEnabled[n]&&hotkey.ConflictWith(root.Hotkey_Pens[n]))return -1;
                return slot;
            }
            return -1;
        }
        public Font CreateNoteFont(){return new Font(FontFamily,FontPoints,FontStyle,GraphicsUnit.Point);}
        public void SaveFont(Font font)
        {
            string oldFamily=FontFamily;float oldPoints=FontPoints;FontStyle oldStyle=FontStyle;bool oldHas=HasFont;
            string oldScreenFamily = ScreenTextFamily;
            ScreenTextFamily = null;
            FontFamily=font.Name;FontPoints=Math.Max(6,Math.Min(144,font.SizeInPoints));FontStyle=font.Style&(FontStyle.Bold|FontStyle.Italic);HasFont=true;
            try {Save();}catch {FontFamily=oldFamily;FontPoints=oldPoints;FontStyle=oldStyle;HasFont=oldHas;ScreenTextFamily=oldScreenFamily;throw;}
            ApplyFontDefaults();
        }
        private void ApplyFontDefaults()
        {
            root.TextFont=FontFamily;root.TextSize=(int)Math.Round(FontPoints);root.TextBold=(FontStyle&FontStyle.Bold)!=0;root.TextItalic=(FontStyle&FontStyle.Italic)!=0;
            if(root.FormCollection!=null)
            {root.FormCollection.TextFont=root.TextFont;root.FormCollection.TextSize=root.TextSize;root.FormCollection.TextBold=root.TextBold;root.FormCollection.TextItalic=root.TextItalic;}
        }
        public void ShowOptions(){using(var dialog=new DrawingSettingsDialog(root))dialog.ShowDialog(root.FormCollection);}
        private void Validate(DrawingPreset value)
        {
            if(value==null||value.Attributes==null||value.Slot<0||value.Slot>=Root.MaxDisplayedPens ||
                Array.IndexOf(PresetTools,value.Tool)<0||value.Fill<0||value.Fill>4 ||
                value.Attributes.Width<=0||float.IsNaN(value.Attributes.Width)||float.IsInfinity(value.Attributes.Width))
                throw new ArgumentException("프리셋 값이 올바르지 않습니다.");
        }
        private static string Attr(XElement element,string name,string fallback="") {return (string)element.Attribute(name)??fallback;}
        private static int Int(XElement element,string name,int fallback=0){return int.Parse(Attr(element,name,fallback.ToString(Invariant)),Invariant);}
        private static float Number(XElement element,string name,float fallback){return float.Parse(Attr(element,name,fallback.ToString(Invariant)),Invariant);}
        private void Load(string file)
        {
            XElement document=XDocument.Load(file).Root;
            if(document==null||document.Name!="ArcDrawSettings")throw new FormatException("Settings document invalid");
            XElement font=document.Element("TextFont");
            if(font!=null)
            {FontFamily=Attr(font,"family","Malgun Gothic");FontPoints=Math.Max(6,Math.Min(144,Number(font,"points",17)));FontStyle=(FontStyle)(Int(font,"style")&3);HasFont=true;}
            XElement colors = document.Element("ToolColors");
            ScreenTextFamily = (string)document.Element("ScreenTextFamily");
            XElement favorites = document.Element("FavoriteFonts");
            if (favorites != null)
            {
                foreach (XElement entry in favorites.Elements("Font"))
                {
                    string family = entry.Value.Trim();
                    if (family.Length > 0) favoriteFonts.Add(family);
                }
            }
            if (int.TryParse((string)colors?.Attribute("text"), NumberStyles.Integer, Invariant, out int text))
                ScreenTextColor = Color.FromArgb(text);
            if (int.TryParse((string)colors?.Attribute("brush"), NumberStyles.Integer, Invariant, out int brush))
                BrushColor = Color.FromArgb(brush);
            foreach(XElement element in document.Elements("Preset"))
            {
                int slot=Int(element,"slot",-1);if(slot<0||slot>=Root.MaxDisplayedPens)continue;
                DrawingAttributes pen=root.PenAttr[slot].Clone();pen.Color=Color.FromArgb(Int(element,"color"));pen.Transparency=(byte)Math.Max(0,Math.Min(255,Int(element,"transparency")));
                pen.Width=Number(element,"width",80);pen.Height=Number(element,"height",pen.Width);pen.PenTip=(PenTip)Int(element,"tip");
                string line=Attr(element,"line","Stroke");var dash=root.LineStyleFromString(line);
                if(pen.ExtendedProperties.Contains(Root.DASHED_LINE_GUID))pen.ExtendedProperties.Remove(Root.DASHED_LINE_GUID);
                if(line!="Stroke")pen.ExtendedProperties.Add(Root.DASHED_LINE_GUID,dash);
                if(pen.ExtendedProperties.Contains(Root.FADING_PEN))pen.ExtendedProperties.Remove(Root.FADING_PEN);
                float fading=Number(element,"fading",0);if(fading>0)pen.ExtendedProperties.Add(Root.FADING_PEN,fading);
                var value=new DrawingPreset {Slot=slot,Name=Attr(element,"name","프리셋 "+Shortcut(slot)),Tool=Int(element,"tool"),Fill=Int(element,"fill"),Attributes=pen};
                Validate(value);presets[slot]=value;
            }
        }
        private void Save()
        {
            var document=new XElement("ArcDrawSettings",new XAttribute("version",1));
            if (ScreenTextFamily != null) document.Add(new XElement("ScreenTextFamily", ScreenTextFamily));
            if (favoriteFonts.Count > 0)
            {
                var favorites = new XElement("FavoriteFonts");
                foreach (string family in favoriteFonts) favorites.Add(new XElement("Font", family));
                document.Add(favorites);
            }
            if (ScreenTextColor.HasValue || BrushColor.HasValue)
                document.Add(new XElement("ToolColors",
                    ScreenTextColor.HasValue ? new XAttribute("text", ScreenTextColor.Value.ToArgb()) : null,
                    BrushColor.HasValue ? new XAttribute("brush", BrushColor.Value.ToArgb()) : null));
            if(HasFont)document.Add(new XElement("TextFont",new XAttribute("family",FontFamily),new XAttribute("points",FontPoints.ToString(Invariant)),new XAttribute("style",(int)FontStyle)));
            foreach(DrawingPreset value in presets.Values)
            {
                DrawingAttributes pen=value.Attributes;
                document.Add(new XElement("Preset",new XAttribute("slot",value.Slot),new XAttribute("name",value.Name??""),new XAttribute("tool",value.Tool),new XAttribute("fill",value.Fill),
                    new XAttribute("color",pen.Color.ToArgb()),new XAttribute("transparency",pen.Transparency),new XAttribute("width",pen.Width.ToString(Invariant)),new XAttribute("height",pen.Height.ToString(Invariant)),
                    new XAttribute("tip",(int)pen.PenTip),new XAttribute("line",root.LineStyleToString(pen.ExtendedProperties)),
                    new XAttribute("fading",pen.ExtendedProperties.Contains(Root.FADING_PEN)?Convert.ToSingle(pen.ExtendedProperties[Root.FADING_PEN].Data,Invariant).ToString(Invariant):"0")));
            }
            string temporary=FileName+".tmp";new XDocument(document).Save(temporary);
            if(File.Exists(FileName))File.Replace(temporary,FileName,FileName+".bak");else File.Move(temporary,FileName);
        }
    }
    public partial class FormCollection
    {
        internal void RefreshPresetButtons(int slot)
        {
            if(Root.CurrentPen==slot)SelectPen(slot);
            recomputePensSet(FirstPenDisplayed,Root.CurrentPen);Root.UponButtonsUpdate|=2;
            if(Root.Whiteboards!=null)Root.Whiteboards.RefreshInkTools();
        }
    }
}
