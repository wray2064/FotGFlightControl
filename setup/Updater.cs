using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using Microsoft.Win32;

// Update check shared by Setup and Hangar Bay's Console.
//
// update.json on the product site names the latest release (version, ZIP URL, size, SHA-256)
// and, per Flight Control version, any newer game versions checked to work with it. The ZIP is
// downloaded only from the product site or the GitHub releases of the source repository, must
// match the published size and SHA-256, and is extracted under %LOCALAPPDATA%. Its Setup.exe is
// then shown in File Explorer for the player to run; Flight Control never starts it. Nothing is sent
// beyond the HTTPS request itself. FLIGHTCONTROL_UPDATE_URL points the check at a local test
// manifest (file:// or http://localhost only).
sealed class UpdateInfo {
    public string Version, Url, Sha256, Notes, Date;
    public long Size;
}

static class Updater {
    const string RegistryKey = @"Software\Manic Trash Panda\Flight Control";
    static readonly string[] TrustedDownloadPrefixes = {
        "https://manictrashpanda.dev/games/FotGFlightControl/download/",
        "https://github.com/wray2064/FotGFlightControl/releases/download/"
    };

    public static bool CheckEnabled {
        get { try { using (var k = Registry.CurrentUser.OpenSubKey(RegistryKey)) { return k == null || !(k.GetValue("CheckForUpdates") is int) || (int)k.GetValue("CheckForUpdates") != 0; } } catch { return true; } }
        set { try { using (var k = Registry.CurrentUser.CreateSubKey(RegistryKey)) { k.SetValue("CheckForUpdates", value ? 1 : 0, RegistryValueKind.DWord); } } catch { } }
    }
    // The version the player chose to skip in the console reminder; Setup still shows it.
    public static string SkippedVersion {
        get { try { using (var k = Registry.CurrentUser.OpenSubKey(RegistryKey)) { return k == null ? null : k.GetValue("SkippedVersion") as string; } } catch { return null; } }
        set { try { using (var k = Registry.CurrentUser.CreateSubKey(RegistryKey)) { k.SetValue("SkippedVersion", value ?? ""); } } catch { } }
    }

    static string ManifestUrl() {
        string test = Environment.GetEnvironmentVariable("FLIGHTCONTROL_UPDATE_URL");
        if (!String.IsNullOrEmpty(test) && IsLocalTestUrl(test)) return test;
        return BuildInfo.UpdateManifest;
    }
    static bool IsLocalTestUrl(string url) {
        Uri u;
        if (!Uri.TryCreate(url, UriKind.Absolute, out u)) return false;
        return u.IsFile || (u.Scheme == Uri.UriSchemeHttp && (u.Host == "localhost" || u.Host == "127.0.0.1"));
    }
    static bool TestMode() { string t = Environment.GetEnvironmentVariable("FLIGHTCONTROL_UPDATE_URL"); return !String.IsNullOrEmpty(t) && IsLocalTestUrl(t); }

    static string Fetch(string url, int timeoutMs) {
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        Uri u = new Uri(url);
        if (u.IsFile) return File.ReadAllText(u.LocalPath);
        var request = (HttpWebRequest)WebRequest.Create(u);
        request.Timeout = timeoutMs; request.ReadWriteTimeout = timeoutMs;
        request.UserAgent = "FlightControl/" + BuildInfo.Version;
        request.AllowAutoRedirect = true;
        using (var response = (HttpWebResponse)request.GetResponse())
        using (var reader = new StreamReader(response.GetResponseStream())) {
            if (response.ContentLength > 65536) throw new InvalidDataException("Update manifest is too large.");
            char[] buffer = new char[65537]; int read = reader.ReadBlock(buffer, 0, buffer.Length);
            if (read > 65536) throw new InvalidDataException("Update manifest is too large.");
            return new string(buffer, 0, read);
        }
    }

    static Dictionary<string, object> Manifest(int timeoutMs) {
        var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(Fetch(ManifestUrl(), timeoutMs));
        object schema;
        if (data == null || !data.TryGetValue("schema", out schema) || Convert.ToInt32(schema) != 1) throw new InvalidDataException("Unknown update manifest format.");
        return data;
    }

    public static bool IsNewer(string candidate, string current) {
        Version a, b;
        return Version.TryParse(candidate, out a) && Version.TryParse(current, out b) && a > b;
    }

    // Game versions verified online for this build by the last Check (for example a game update
    // that shipped after this build). Empty until a check succeeds.
    public static string[] LastVerifiedGameVersions = new string[0];

    // The newer release, or null when this build is current. Throws on network or format errors.
    public static UpdateInfo Check(int timeoutMs) {
        var data = Manifest(timeoutMs);
        LastVerifiedGameVersions = VerifiedGameVersions(data);
        var latest = data.ContainsKey("latest") ? data["latest"] as Dictionary<string, object> : null;
        if (latest == null) throw new InvalidDataException("Update manifest has no latest release.");
        var info = new UpdateInfo {
            Version = Convert.ToString(latest["version"]), Url = Convert.ToString(latest["url"]),
            Sha256 = Convert.ToString(latest["sha256"]).ToLowerInvariant(), Size = Convert.ToInt64(latest["size"]),
            Notes = latest.ContainsKey("notes") ? Convert.ToString(latest["notes"]) : BuildInfo.ProductPage,
            Date = latest.ContainsKey("date") ? Convert.ToString(latest["date"]) : ""
        };
        if (!IsNewer(info.Version, BuildInfo.Version)) return null;
        if (!TrustedDownload(info.Url)) throw new InvalidDataException("The update download is not on the Flight Control site or its GitHub releases.");
        if (info.Sha256.Length != 64 || info.Sha256.Any(c => !Uri.IsHexDigit(c))) throw new InvalidDataException("The update has no valid SHA-256.");
        if (info.Size <= 0 || info.Size > 200L * 1024 * 1024) throw new InvalidDataException("The update size is not valid.");
        return info;
    }
    static bool TrustedDownload(string url) {
        if (TestMode() && IsLocalTestUrl(url)) return true;
        Uri u;
        return Uri.TryCreate(url, UriKind.Absolute, out u) && u.Scheme == Uri.UriSchemeHttps
            && TrustedDownloadPrefixes.Any(p => url.StartsWith(p, StringComparison.Ordinal)) && u.AbsolutePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    static string[] VerifiedGameVersions(Dictionary<string, object> data) {
        var all = data.ContainsKey("verifiedGameVersions") ? data["verifiedGameVersions"] as Dictionary<string, object> : null;
        object mine;
        if (all == null || !all.TryGetValue(BuildInfo.Version, out mine) || !(mine is System.Collections.IEnumerable)) return new string[0];
        Version ignored;
        return ((System.Collections.IEnumerable)mine).Cast<object>().Select(Convert.ToString).Where(v => Version.TryParse(v, out ignored)).ToArray();
    }

    // Downloads and verifies the release, extracts it, and returns the path of its Setup.exe.
    public static string DownloadAndExtract(UpdateInfo info, Action<string> progress) {
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Manic Trash Panda", "Flight Control", "Updates", info.Version);
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);
        string zip = Path.Combine(folder, "FotG-Flight-Control-" + info.Version + ".zip");
        progress("Downloading Flight Control " + info.Version + "…");
        Uri u = new Uri(info.Url);
        if (u.IsFile) File.Copy(u.LocalPath, zip);
        else {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (var client = new WebClient()) { client.Headers[HttpRequestHeader.UserAgent] = "FlightControl/" + BuildInfo.Version; client.DownloadFile(u, zip); }
        }
        progress("Checking the download…");
        var file = new FileInfo(zip);
        if (file.Length != info.Size) { File.Delete(zip); throw new InvalidDataException("The download has the wrong size, so it was discarded. Try again later or download it from " + BuildInfo.ProductPage); }
        string hash;
        using (var sha = SHA256.Create()) using (var stream = File.OpenRead(zip)) hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        if (hash != info.Sha256) { File.Delete(zip); throw new InvalidDataException("The download does not match its published SHA-256, so it was discarded. Try again later or download it from " + BuildInfo.ProductPage); }
        progress("Extracting…");
        string target = Path.Combine(folder, "release");
        string targetRoot = Path.GetFullPath(target) + Path.DirectorySeparatorChar;
        using (var archive = ZipFile.OpenRead(zip)) {
            foreach (var entry in archive.Entries) {
                string name = entry.FullName.Replace('\\', '/');
                string path = Path.GetFullPath(Path.Combine(target, name.Replace('/', Path.DirectorySeparatorChar)));
                if (!path.StartsWith(targetRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The update archive contains an unsafe path.");
                if (name.EndsWith("/")) { Directory.CreateDirectory(path); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                entry.ExtractToFile(path, true);
            }
        }
        string setup = Directory.GetFiles(target, "Setup.exe", SearchOption.AllDirectories).OrderBy(p => p.Length).FirstOrDefault();
        if (setup == null) throw new InvalidDataException("The update has no Setup.exe.");
        return setup;
    }

    // Opens File Explorer with the file selected.
    public static void ShowInExplorer(string file) {
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "/select,\"" + file + "\"") { UseShellExecute = true });
    }

    public static bool SelfTest() {
        if (!IsNewer("1.1.1", "1.1.0") || IsNewer("1.1.0", "1.1.0") || IsNewer("1.0.9", "1.1.0") || IsNewer("x", "1.1.0")) return false;
        if (!TrustedDownload("https://manictrashpanda.dev/games/FotGFlightControl/download/FotG-Flight-Control-1.1.2.zip")) return false;
        if (!TrustedDownload("https://github.com/wray2064/FotGFlightControl/releases/download/v1.1.2/FotG-Flight-Control-1.1.2.zip")) return false;
        if (TrustedDownload("http://manictrashpanda.dev/games/FotGFlightControl/download/x.zip")) return false;
        if (TrustedDownload("https://evil.example/games/FotGFlightControl/download/x.zip")) return false;
        if (TrustedDownload("https://manictrashpanda.dev.evil.example/games/FotGFlightControl/download/x.zip")) return false;
        if (TrustedDownload("https://manictrashpanda.dev/games/FotGFlightControl/download/x.exe")) return false;
        if (TrustedDownload("https://github.com/someone/FotGFlightControl/releases/download/v1/x.zip")) return false;
        return true;
    }
}
