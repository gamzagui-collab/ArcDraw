// ArcDraw identity and retained upstream notices. Ink handling stays in ppInk.
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace gInk
{
    internal static class ArcDrawBranding
    {
        internal const string Name = "ArcDraw";
        internal const string ToolbarCredit = "ArcDraw · ppInk 기반";
        internal static Icon CreateIcon()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ArcDraw.Icon"))
            using (Icon icon = new Icon(stream, new Size(32, 32))) return (Icon)icon.Clone();
        }
        internal static Image CreateImage()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ArcDraw.Image"))
            using (Image image = Image.FromStream(stream)) return new Bitmap(image);
        }
        internal static string Notices()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("ArcDraw.UpstreamLicense"))
            using (StreamReader reader = new StreamReader(stream))
                return "ArcDraw — ppInk / gInk 기반 사용자 수정본\r\n" +
                    "원개발: Weizhi Nai (gInk), Pubpub-ZZ (ppInk)\r\n" +
                    "원본 표기: Copyright © 2019 Weizhi Nai — © 2020-2024 Pubpub-ZZ\r\n" +
                    "수정: 화이트보드 노트·텍스트·CAPS 입력 전환·툴바 테마 및 ArcDraw 아이콘\r\n" +
                    "https://github.com/pubpub-zz/ppInk\r\nhttps://github.com/geovens/gInk\r\n\r\n" + reader.ReadToEnd();
        }
        internal static void ShowInfo(IWin32Window owner)
        {
            using (Form dialog = new Form())
            using (Icon icon = CreateIcon())
            using (Image image = CreateImage())
            using (Font font = new Font("Malgun Gothic", 9f))
            {
                dialog.Name = "ArcDrawAbout"; dialog.Text = "ArcDraw · 개발 및 라이선스";
                dialog.Icon = icon; dialog.Font = font; dialog.ClientSize = new Size(580, 430);
                dialog.StartPosition = FormStartPosition.CenterScreen; dialog.TopMost = true;
                dialog.MinimizeBox = false; dialog.MaximizeBox = false;
                var picture = new PictureBox { Image = image, SizeMode = PictureBoxSizeMode.Zoom,
                    Location = new Point(20, 16), Size = new Size(52, 52) };
                var title = new Label { Text = "ArcDraw", AutoSize = true, Location = new Point(84, 20) };
                var subtitle = new Label { Text = "ppInk / gInk 기반 · 원개발자 및 MIT 라이선스 보존",
                    AutoSize = true, Location = new Point(84, 43) };
                var notices = new TextBox { Name = "ArcDrawLicenseText", Multiline = true, ReadOnly = true,
                    ScrollBars = ScrollBars.Vertical, Text = Notices(), Location = new Point(20, 84),
                    Size = new Size(540, 286), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
                var close = new Button { Text = "닫기", DialogResult = DialogResult.OK,
                    Location = new Point(480, 386), Size = new Size(80, 28), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
                dialog.Controls.AddRange(new Control[] { picture, title, subtitle, notices, close });
                dialog.AcceptButton = close; dialog.CancelButton = close;
                dialog.ShowDialog(owner);
            }
        }
    }
}
