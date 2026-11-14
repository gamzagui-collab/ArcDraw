using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace gInk
{
    internal static class ArcDrawUpdateInstaller
    {
        internal const string WorkerArgument = "--arc-update-worker";
        internal const string PackageName = "release.zip";
        private const string Exe = "ArcDraw.exe", Manifest = "SHA256-MANIFEST.json";
        private const string Backup = "previous", Payload = "payload";
        private static readonly string UpdateFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArcDraw", "Updates");
        private static readonly HashSet<string> ResourceFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "lang", "licenses", "toolbar-themes" };
        private static readonly HashSet<string> ResourceExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".dll", ".png", ".ico", ".gif", ".apng", ".ini", ".md", ".txt", ".zip", ".w32", ".x64", ".json", ".example" };

        internal static string CreateStage()
        {
            // The parent can still be running when the update download finishes.
            // A unique folder keeps separate attempts and their backups independent.
            string stage = Path.Combine(UpdateFolder, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            return stage;
        }

        internal static void DeleteStage(string stage)
        {
            try { Directory.Delete(stage, true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        internal static void StagePackage(string stage, string tag, string digest)
        {
            string archive = Path.Combine(stage, PackageName);
            VerifyHash(archive, digest.Substring("sha256:".Length));
            string payload = Path.Combine(stage, Payload);
            Directory.CreateDirectory(payload);
            using (var zip = new ZipArchive(File.OpenRead(archive), ZipArchiveMode.Read))
            {
                string prefix = "ArcDraw-" + tag + "/";
                var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
                long size = 0;
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    if (entry.Name.Length == 0) continue;
                    if (!entry.FullName.StartsWith(prefix, StringComparison.Ordinal))
                        throw new InvalidDataException("압축 파일 경로가 올바르지 않습니다.");
                    string path = entry.FullName.Substring(prefix.Length);
                    if (!ValidPath(path) || entries.ContainsKey(path) || entries.Count >= 1000)
                        throw new InvalidDataException("압축 파일에 허용되지 않는 경로가 있습니다.");
                    size += entry.Length;
                    if (size > 256L * 1024 * 1024) throw new InvalidDataException("압축을 푼 크기가 너무 큽니다.");
                    entries.Add(path, entry);
                }
                if (!entries.TryGetValue(Manifest, out ZipArchiveEntry manifest) || manifest.Length > 256 * 1024)
                    throw new InvalidDataException("파일 검증 목록이 없습니다.");
                CopyEntry(manifest, Path.Combine(payload, Manifest));
                UpdateFile[] files = ReadManifest(payload);
                if (files.Length + 1 != entries.Count)
                    throw new InvalidDataException("압축 파일과 검증 목록이 일치하지 않습니다.");
                foreach (UpdateFile file in files)
                {
                    if (!entries.TryGetValue(file.Path, out ZipArchiveEntry entry))
                        throw new InvalidDataException("설치 파일이 누락되었습니다.");
                    string path = Path.Combine(payload, file.Path);
                    CopyEntry(entry, path);
                    VerifyHash(path, file.Hash);
                }
            }
            VerifyVersion(payload, tag);
        }

        private static bool ValidPath(string path)
        {
            if (String.IsNullOrEmpty(path) || path.Length > 160) return false;
            string[] parts = path.Split('/');
            foreach (string part in parts)
                if (part.Length == 0 || part.StartsWith(".") || part.EndsWith(".") || part.Trim() != part ||
                    part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                    Regex.IsMatch(part.Split('.')[0], @"\A(CON|PRN|AUX|NUL|COM[0-9¹²³]|LPT[0-9¹²³])\z", RegexOptions.IgnoreCase))
                    return false;
            if (parts.Length > 1 && !ResourceFolders.Contains(parts[0])) return false;
            if (parts.Length == 1 && (path == Exe || path == Exe + ".config" || path == "files_copied")) return true;
            return ResourceExtensions.Contains(Path.GetExtension(path));
        }

        private static UpdateFile[] ReadManifest(string payload)
        {
            using (Stream input = File.OpenRead(Path.Combine(payload, Manifest)))
            {
                var files = ArcDrawUpdates.ReadJson<UpdateFile[]>(input);
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (files == null || files.Length > 999 || files.Any(file => file == null || !ValidPath(file.Path) ||
                    file.Path.Equals(Manifest, StringComparison.OrdinalIgnoreCase) || !names.Add(file.Path) ||
                    !Regex.IsMatch(file.Hash ?? "", @"\A[0-9a-fA-F]{64}\z")) || !names.Contains(Exe))
                    throw new InvalidDataException("파일 검증 목록이 올바르지 않습니다.");
                return files;
            }
        }

        private static void CopyEntry(ZipArchiveEntry entry, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (Stream source = entry.Open())
            using (var target = new FileStream(path, FileMode.CreateNew))
                source.CopyTo(target);
        }

        private static void VerifyHash(string path, string expected)
        {
            using (var sha = SHA256.Create())
            using (Stream stream = File.OpenRead(path))
            {
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("SHA-256이 일치하지 않습니다. " + Path.GetFileName(path));
            }
        }

        private static void VerifyVersion(string payload, string tag)
        {
            var version = FileVersionInfo.GetVersionInfo(Path.Combine(payload, Exe));
            if (!ArcDrawUpdates.TryVersion(tag, out Version expected) || version.ProductVersion != expected.ToString())
                throw new InvalidDataException("실행 파일 버전이 GitHub 릴리스와 일치하지 않습니다.");
        }

        internal static void Launch(string stage, string tag, string digest)
        {
            string install = Path.GetFullPath(Program.ProgramFolder);
            string launcher = Path.Combine(stage, "ArcDraw.Update.exe");
            File.Copy(Path.Combine(install, Exe), launcher);
            string[] arguments = Environment.GetCommandLineArgs().Skip(1).ToArray();
            using (Process parent = Process.GetCurrentProcess())
            {
                string args = String.Join(" ", new[] { WorkerArgument, parent.Id.ToString(CultureInfo.InvariantCulture),
                    parent.StartTime.ToFileTimeUtc().ToString(CultureInfo.InvariantCulture), install, tag, digest }
                    .Concat(arguments).Select(Quote));
                using (Process worker = Process.Start(new ProcessStartInfo(launcher, args)
                    { UseShellExecute = false, WorkingDirectory = stage }))
                {
                    if (worker == null) throw new IOException("업데이트 프로그램을 시작하지 못했습니다.");
                }
            }
        }

        internal static void Run(string[] args)
        {
            string stage = AppDomain.CurrentDomain.BaseDirectory;
            if (args.Length < 6 || !int.TryParse(args[1], out int parent) || !long.TryParse(args[2], out long started) ||
                !ArcDrawUpdates.TryVersion(args[4], out Version next) || next <= ArcDrawUpdates.Current ||
                !Regex.IsMatch(args[5], @"\Asha256:[0-9a-fA-F]{64}\z")) return;
            string install = args[3];
            string exe = Path.Combine(install, Exe);
            bool canRestart = false;
            try
            {
                if (!String.Equals(Path.GetDirectoryName(stage.TrimEnd(Path.DirectorySeparatorChar)), UpdateFolder,
                        StringComparison.OrdinalIgnoreCase) ||
                    !String.Equals(FileVersionInfo.GetVersionInfo(exe).ProductVersion, ArcDrawUpdates.Current.ToString(),
                        StringComparison.Ordinal)) return;
                using (Process process = FindParent(parent))
                {
                    if (process != null && process.StartTime.ToFileTimeUtc() == started && !process.WaitForExit(15000))
                        throw new IOException("ArcDraw가 아직 종료되지 않았습니다.");
                }
                canRestart = true;
                DeleteStage(Path.Combine(stage, Payload));
                StagePackage(stage, args[4], args[5]);
                Install(stage, install, args[4]);
                DeleteStage(Path.Combine(stage, Payload));
                try { File.Delete(Path.Combine(stage, PackageName)); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            catch (Exception error) when (ArcDrawUpdates.IsUpdateError(error) || error is AggregateException)
            {
                MessageBox.Show("업데이트를 적용하지 못했습니다.\n" + error.Message,
                    "ArcDraw 업데이트", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                if (error is AggregateException) canRestart = false;
            }
            if (canRestart)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(exe, String.Join(" ", args.Skip(6).Select(Quote)))
                        { UseShellExecute = false, WorkingDirectory = install })?.Dispose();
                }
                catch (Exception error) when (ArcDrawUpdates.IsUpdateError(error))
                {
                    MessageBox.Show("ArcDraw를 다시 시작하지 못했습니다.\n" + error.Message, "ArcDraw 업데이트");
                }
            }
        }

        private static Process FindParent(int id)
        {
            try { return Process.GetProcessById(id); }
            catch (ArgumentException) { return null; }
        }

        internal static void Install(string stage, string install, string tag)
        {
            string payload = Path.Combine(stage, Payload), backup = Path.Combine(stage, Backup);
            VerifyVersion(payload, tag);
            var files = ReadManifest(payload);
            foreach (UpdateFile file in files) VerifyHash(Path.Combine(payload, file.Path), file.Hash);

            var changed = new List<string>();
            try
            {
                // Replace the executable last so a failed resource copy can leave the old app runnable.
                foreach (string name in files.Select(file => file.Path).Append(Manifest)
                    .OrderBy(name => name.Equals(Exe, StringComparison.OrdinalIgnoreCase)))
                {
                    string destination = Path.Combine(install, name);
                    CheckDestination(destination);
                    bool exists = File.Exists(destination);
                    if (exists && PreserveLocal(name)) continue;
                    string source = Path.Combine(payload, name);
                    if (exists && SameContents(source, destination)) continue;
                    if (exists)
                    {
                        string previous = Path.Combine(backup, name);
                        Directory.CreateDirectory(Path.GetDirectoryName(previous));
                        File.Copy(destination, previous);
                    }
                    ReplaceFile(source, destination);
                    changed.Add(name);
                }
            }
            catch (Exception error) when (ArcDrawUpdates.IsUpdateError(error))
            {
                try
                {
                    foreach (string name in changed.AsEnumerable().Reverse())
                    {
                        string previous = Path.Combine(backup, name);
                        string destination = Path.Combine(install, name);
                        if (File.Exists(previous)) ReplaceFile(previous, destination);
                        else File.Delete(destination);
                    }
                }
                catch (Exception rollback) when (ArcDrawUpdates.IsUpdateError(rollback))
                {
                    throw new AggregateException("이전 파일을 복원하지 못했습니다. 백업 위치: " + backup, error, rollback);
                }
                throw;
            }
        }

        private static bool PreserveLocal(string name) =>
            Path.GetExtension(name).Equals(".ini", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("files_copied", StringComparison.OrdinalIgnoreCase);

        private static void CheckDestination(string path)
        {
            for (string item = path; item != null; item = Path.GetDirectoryName(item))
                if ((File.Exists(item) || Directory.Exists(item)) && (File.GetAttributes(item) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("연결된 폴더에는 업데이트할 수 없습니다. " + item);
        }

        private static bool SameContents(string first, string second)
        {
            if (new FileInfo(first).Length != new FileInfo(second).Length) return false;
            using (var sha = SHA256.Create())
            using (Stream a = File.OpenRead(first))
            using (Stream b = File.OpenRead(second))
                return sha.ComputeHash(a).SequenceEqual(sha.ComputeHash(b));
        }

        private static void ReplaceFile(string source, string destination)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            string temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(source, temporary);
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static string Quote(string value) =>
            "\"" + Regex.Replace(value, "(\\\\*)(\"|$)", match => match.Groups[1].Value + match.Groups[1].Value +
                (match.Groups[2].Value == "\"" ? "\\\"" : "")) + "\"";

        [DataContract]
        private sealed class UpdateFile
        {
            [DataMember(Name = "path")] internal string Path { get; set; }
            [DataMember(Name = "sha256")] internal string Hash { get; set; }
        }
    }
}
