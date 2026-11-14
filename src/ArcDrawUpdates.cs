using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace gInk
{
    internal sealed class ArcDrawUpdates : IDisposable
    {
        internal const string Repository = "https://github.com/gamzagui-collab/ArcDraw";
        private static readonly Uri Latest = new Uri("https://api.github.com/repos/gamzagui-collab/ArcDraw/releases/latest");
        private static readonly HttpClient Client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
            { Timeout = TimeSpan.FromSeconds(30), MaxResponseContentBufferSize = 1024 * 1024 };
        private readonly Root root;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private Release offered;
        private bool busy, notification, disposed;
        internal static Version Current => Version.Parse(typeof(ArcDrawUpdates).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion);

        static ArcDrawUpdates()
        {
            Client.DefaultRequestHeaders.UserAgent.ParseAdd("ArcDraw/" + Current);
            Client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        }

        internal ArcDrawUpdates(Root root)
        {
            this.root = root;
            root.trayIcon.BalloonTipClosed += NotificationClosed;
        }

        private void NotificationClosed(object sender, EventArgs args) => notification = false;
        internal void DismissNotification() => notification = false;

        internal bool OpenNotification()
        {
            if (!notification || offered == null) return false;
            notification = false;
            if (!busy) OfferUpdate(offered);
            return true;
        }

        internal async void Check(bool manual)
        {
            if (busy || disposed) return;
            busy = true;
            Release update = null;
            try
            {
                using (HttpResponseMessage response = await Client.GetAsync(Latest, lifetime.Token))
                {
                    if (response.StatusCode != HttpStatusCode.NotFound)
                    {
                        response.EnsureSuccessStatusCode();
                        using (Stream stream = await response.Content.ReadAsStreamAsync())
                            update = FindUpdate(stream, Current);
                    }
                }
                if (disposed) return;
                if (update == null)
                {
                    if (manual) MessageBox.Show("최신 버전을 사용 중입니다. (v" + Current + ")", "ArcDraw 업데이트");
                    return;
                }
                offered = update;
                if (!manual)
                {
                    notification = true;
                    root.trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                    root.trayIcon.ShowBalloonTip(8000, "ArcDraw " + update.Tag + " 업데이트",
                        "알림을 누르거나 트레이 메뉴의 업데이트 확인을 여세요.", ToolTipIcon.Info);
                }
            }
            catch (Exception error) when (IsUpdateError(error))
            {
                if (manual && !disposed) MessageBox.Show("GitHub에서 업데이트 정보를 가져오지 못했습니다.\n" +
                    error.Message, "ArcDraw 업데이트", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { busy = false; }
            if (manual && update != null && !disposed) OfferUpdate(update);
        }

        internal static Release FindUpdate(Stream stream, Version installed)
        {
            var release = ReadJson<Release>(stream);
            if (release == null || release.Draft || release.Prerelease || !TryVersion(release.Tag, out Version latest) ||
                latest <= installed) return null;
            string name = "ArcDraw-" + release.Tag + "-windows-x86.zip";
            foreach (Asset asset in release.Assets ?? new Asset[0])
            {
                string url = Repository + "/releases/download/" + release.Tag + "/" + name;
                if (asset != null && asset.Name == name && asset.Download == url && asset.Size > 0 && asset.Size <= 128L * 1024 * 1024 &&
                    Regex.IsMatch(asset.Digest ?? "", @"\Asha256:[0-9a-fA-F]{64}\z"))
                {
                    release.Package = asset;
                    break;
                }
            }
            return release;
        }

        internal static T ReadJson<T>(Stream stream) => (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        internal static bool TryVersion(string tag, out Version version)
        {
            version = null;
            return Regex.IsMatch(tag ?? "", @"\Av\d+\.\d+(\.\d+)?\z") && Version.TryParse(tag.Substring(1), out version);
        }

        private async void OfferUpdate(Release release)
        {
            if (release.Package == null)
            {
                MessageBox.Show("GitHub에 " + release.Tag + " 릴리스가 있습니다. 검증 가능한 Windows 설치 파일이 올라오면 앱에서 업데이트할 수 있습니다.",
                    "ArcDraw 업데이트");
                return;
            }
            if (MessageBox.Show("v" + Current + " → " + release.Tag + "\n\n지금 업데이트하고 ArcDraw를 다시 시작할까요?",
                "ArcDraw 업데이트", MessageBoxButtons.YesNo, MessageBoxIcon.Information,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

            busy = true;
            string stage = null;
            bool handedOff = false;
            var status = new Form
            {
                Text = "ArcDraw 업데이트", ClientSize = new System.Drawing.Size(344, 76),
                StartPosition = FormStartPosition.CenterScreen, FormBorderStyle = FormBorderStyle.FixedDialog,
                ControlBox = false, ShowInTaskbar = false, TopMost = true
            };
            status.Controls.Add(new Label { Text = release.Tag + " 다운로드 및 검증 중…", Left = 14, Top = 11, AutoSize = true });
            status.Controls.Add(new ProgressBar { Left = 14, Top = 36, Width = 316, Height = 20, Style = ProgressBarStyle.Marquee });
            try
            {
                status.Show();
                stage = ArcDrawUpdateInstaller.CreateStage();
                string zip = Path.Combine(stage, ArcDrawUpdateInstaller.PackageName);
                await Download(release.Package, zip, lifetime.Token);
                await Task.Run(() => ArcDrawUpdateInstaller.StagePackage(stage, release.Tag, release.Package.Digest), lifetime.Token);
                if (disposed) return;
                status.Hide();
                root.ExitForUpdate(() =>
                {
                    ArcDrawUpdateInstaller.Launch(stage, release.Tag, release.Package.Digest);
                    handedOff = true;
                });
            }
            catch (Exception error) when (IsUpdateError(error))
            {
                if (!status.IsDisposed) status.Hide();
                if (!disposed) MessageBox.Show("업데이트를 적용하지 못했습니다.\n" + error.Message,
                    "ArcDraw 업데이트", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                busy = false;
                status.Dispose();
                if (!handedOff && stage != null) ArcDrawUpdateInstaller.DeleteStage(stage);
            }
        }

        private static async Task Download(Asset asset, string target, CancellationToken token)
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                deadline.CancelAfter(TimeSpan.FromMinutes(3));
                Uri url = new Uri(asset.Download);
                for (int hop = 0; hop < 4; hop++)
                {
                    using (var response = await Client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, deadline.Token))
                    {
                        int status = (int)response.StatusCode;
                        if (status >= 300 && status < 400 && response.Headers.Location != null)
                        {
                            url = new Uri(url, response.Headers.Location);
                            if (url.Scheme != Uri.UriSchemeHttps || !url.IsDefaultPort || url.UserInfo.Length != 0 ||
                                (url.Host != "release-assets.githubusercontent.com" && url.Host != "objects.githubusercontent.com"))
                                throw new InvalidDataException("다운로드 주소가 올바르지 않습니다.");
                            continue;
                        }
                        response.EnsureSuccessStatusCode();
                        using (Stream input = await response.Content.ReadAsStreamAsync())
                        using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
                        {
                            var buffer = new byte[65536];
                            int read;
                            while ((read = await input.ReadAsync(buffer, 0, buffer.Length, deadline.Token)) != 0)
                            {
                                if (output.Length + read > asset.Size) throw new InvalidDataException("다운로드 크기가 일치하지 않습니다.");
                                await output.WriteAsync(buffer, 0, read, deadline.Token);
                            }
                            if (output.Length != asset.Size) throw new InvalidDataException("다운로드가 완료되지 않았습니다.");
                        }
                        return;
                    }
                }
                throw new InvalidDataException("다운로드 주소를 확인할 수 없습니다.");
            }
        }

        internal static bool IsUpdateError(Exception error) =>
            error is HttpRequestException || error is OperationCanceledException || error is SerializationException ||
            error is IOException || error is InvalidDataException || error is UnauthorizedAccessException ||
            error is InvalidOperationException || error is Win32Exception || error is ArgumentException ||
            error is System.Security.SecurityException;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            root.trayIcon.BalloonTipClosed -= NotificationClosed;
            lifetime.Cancel();
            lifetime.Dispose();
        }

        [DataContract]
        internal sealed class Release
        {
            [DataMember(Name = "tag_name")] internal string Tag { get; set; }
            [DataMember(Name = "draft")] internal bool Draft { get; set; }
            [DataMember(Name = "prerelease")] internal bool Prerelease { get; set; }
            [DataMember(Name = "assets")] internal Asset[] Assets { get; set; }
            internal Asset Package;
        }

        [DataContract]
        internal sealed class Asset
        {
            [DataMember(Name = "name")] internal string Name { get; set; }
            [DataMember(Name = "browser_download_url")] internal string Download { get; set; }
            [DataMember(Name = "digest")] internal string Digest { get; set; }
            [DataMember(Name = "size")] internal long Size { get; set; }
        }
    }
}
