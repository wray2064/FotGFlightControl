using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

// The FSO engine a Fate of the Galaxy install runs on. Shared by Setup and the bridge;
// The installer (Installer.cs) records the stick numbering it found in engine-axes.txt.
//
// 1. The "FSO 26.0.1-SDL3" package (FSO_2601SDL3-1.0.0) when present: FotG 1.1.0 shipped with
//    it, and Flight Control was first tested on it.
// 2. Otherwise the engine FotG's own manifest depends on (FotG 1.1.1 and 1.1.2: FSO 26.0.1,
//    folder FSO-26.0.1), or the newest FSO-* folder. Knossos installs one build per CPU
//    (SSE2, AVX, AVX2); the fastest one this CPU runs is used.
// The engine's SDL library decides how sticks are numbered, so Sdl3 is reported too.
sealed class EngineInfo {
    public string Folder;   // engine package folder
    public string Exe;      // full path of fs2_open_*.exe
    public bool Sdl3;       // SDL3.dll beside the exe (else SDL2.dll)
    public string Label;    // for messages, e.g. "FSO 26.0.1 (SDL2, AVX)"
}

// Where a Knossos game lives. Portable Knossos keeps everything under <folder>\kn_portable. A full (installed)
// Knossos keeps the library in a folder of its own (by default Documents\Freespace2-mods), with the mods and the
// engine builds directly inside it, and the pilots and settings under %APPDATA%\HardLightProductions\FreeSpaceOpen.
// "root" is the folder the player chooses: the one that holds kn_portable, or the library folder itself.
static class GameLayout {
    internal static bool Portable(string root) { return Directory.Exists(Path.Combine(root, "kn_portable")); }
    internal static bool LooksLikeLibrary(string folder) {
        try {
            return Directory.Exists(Path.Combine(folder, "bin"))
                || Directory.Exists(Path.Combine(folder, "FotG_R")) || Directory.Exists(Path.Combine(folder, "FotG_2"));
        } catch { return false; }
    }
    // A folder Setup can work with: a portable Knossos folder or a Knossos library.
    internal static bool Valid(string root) { return Portable(root) || LooksLikeLibrary(root); }
    // The library relative to root ("" when root is the library itself).
    internal static string LibraryRel(string root) {
        return Portable(root) || !LooksLikeLibrary(root) ? @"kn_portable\Library" : "";
    }
    internal static string Library(string root) { string rel = LibraryRel(root); return rel.Length == 0 ? root : Path.Combine(root, rel); }
    // A path inside the library, relative to root, for -mod arguments.
    internal static string Rel(string root, string insideLibrary) {
        string rel = LibraryRel(root); return rel.Length == 0 ? insideLibrary : rel + @"\" + insideLibrary;
    }
    // The FSO preferences folder (the one that holds data\players): inside kn_portable, or in %APPDATA% for a full Knossos.
    internal static string Prefs(string root) {
        if (Portable(root) || !LooksLikeLibrary(root)) return Path.Combine(root, "kn_portable", "HardLightProductions", "FreeSpaceOpen");
        // FSO_PREFERENCES_PATH is the engine's own override for this folder; honour it when it is set.
        string custom = Environment.GetEnvironmentVariable("FSO_PREFERENCES_PATH");
        if (!String.IsNullOrEmpty(custom)) return custom;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HardLightProductions", "FreeSpaceOpen");
    }
    // The library folder a full Knossos.NET has saved in its settings, or "".
    internal static string KnossosLibrary() {
        try {
            string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KnossosNET");
            string file = Path.Combine(data, "settings.json");
            if (!File.Exists(file) && Directory.Exists(data))
                foreach (string sub in Directory.GetDirectories(data))   // a custom launcher keeps it in a folder named for its mod
                    if (File.Exists(Path.Combine(sub, "settings.json"))) { file = Path.Combine(sub, "settings.json"); break; }
            if (!File.Exists(file)) return "";
            string text = File.ReadAllText(file);
            int key = text.IndexOf("\"base_path\"", StringComparison.Ordinal);
            if (key < 0) return "";
            int at = text.IndexOf(':', key) + 1;
            while (at > 0 && at < text.Length && Char.IsWhiteSpace(text[at])) at++;
            if (at <= 0 || at >= text.Length || text[at] != '"') return "";
            var sb = new System.Text.StringBuilder();
            for (int i = at + 1; i < text.Length && text[i] != '"'; i++) {
                if (text[i] == '\\' && i + 1 < text.Length) i++;
                sb.Append(text[i]);
            }
            return sb.ToString();
        } catch { return ""; }
    }
    // Why a folder was refused, in the player's words.
    internal static string WhatIsWrong(string root) {
        bool knossosApp = File.Exists(Path.Combine(root, "Knossos.NET.exe")) || File.Exists(Path.Combine(root, "Knossos.NET"));
        string saved = KnossosLibrary();
        return (knossosApp ? "This looks like the Knossos app folder, not the game library. " : "This folder has no kn_portable folder and does not look like a Knossos library. ")
            + "Choose the folder that holds the Fate of the Galaxy mod folders (FotG_R or FotG_2) and bin: the folder that contains kn_portable (portable Knossos), "
            + "or your Knossos library folder (in Knossos, Settings shows it; the default is Documents\\Freespace2-mods)."
            + (saved.Length > 0 ? " Knossos reports your library as " + saved + "." : "");
    }
}

// A vanilla Knossos has no way to create the MyMod that Flight Control installs into (only the Fate of the Galaxy 2
// launcher makes one). Setup creates the same minimal private mod: a mod.json that depends on FotG_2 and the SDL3
// engine, and an empty core package folder. Knossos lists it as a private mod; nothing of the game itself is touched.
static class MyModTemplate {
    const string Json = @"{
  ""mod_source"": ""nebula"",
  ""installed"": true,
  ""id"": ""MyMod"",
  ""title"": ""MyMod"",
  ""type"": ""mod"",
  ""parent"": ""FotG_2"",
  ""version"": ""1.0.0"",
  ""private"": true,
  ""stability"": null,
  ""description"": """",
  ""notes"": """",
  ""logo"": null,
  ""tile"": null,
  ""banner"": null,
  ""release_thread"": null,
  ""videos"": [],
  ""screenshots"": [],
  ""attachments"": [],
  ""first_release"": null,
  ""last_update"": null,
  ""cmdline"": """",
  ""mod_flag"": [
    ""MyMod"",
    ""FotG_2""
  ],
  ""dev_mode"": true,
  ""custom_build"": null,
  ""owners"": null,
  ""packages"": [
    {
      ""name"": ""core"",
      ""notes"": """",
      ""status"": ""required"",
      ""dependencies"": [
        {
          ""id"": ""FotG_2"",
          ""version"": "">=2.0.2"",
          ""packages"": []
        },
        {
          ""id"": ""FSO_2601SDL3"",
          ""version"": "">=26.0.1-1"",
          ""packages"": []
        }
      ],
      ""environment"": null,
      ""folder"": ""core"",
      ""is_vp"": false,
      ""files"": [],
      ""filelist"": [],
      ""executables"": [],
      ""check_notes"": null,
      ""isEnabled"": true
    }
  ]
}";

    internal static bool HasFotG2(string root) {
        return File.Exists(Path.Combine(GameLayout.Library(root), "FotG_2", "FotG_2-2.0.2", "mod.json"));
    }

    // Creates MyMod 1.0.0 when FotG 2.0.2 is installed and no MyMod exists. Returns true when it was created.
    internal static bool Ensure(string root, Action<string> log) {
        if (GameInstall.HasMyMod(root) || !HasFotG2(root)) return false;
        string mod = Path.Combine(GameLayout.Library(root), "FotG_2", "MyMod-1.0.0");
        Directory.CreateDirectory(Path.Combine(mod, "core", "data"));
        File.WriteAllText(Path.Combine(mod, "mod.json"), Json.Replace("\r\n", "\n").Replace("\n", "\r\n"), new System.Text.UTF8Encoding(false));
        log("Created MyMod 1.0.0 for Fate of the Galaxy 2 in your library (Knossos lists it as a private mod).");
        return true;
    }
}

static class GameInstall {
    const string Sdl3Package = "FSO_2601SDL3-1.0.0";
    [DllImport("kernel32.dll")] static extern bool IsProcessorFeaturePresent(uint feature);
    const uint PF_AVX = 39, PF_AVX2 = 40;
    static bool Has(uint feature) { try { return IsProcessorFeaturePresent(feature); } catch { return false; } }

    static string Bin(string root) { return Path.Combine(GameLayout.Library(root), "bin"); }

    internal static string MyModCore(string root) {
        return Path.Combine(GameLayout.Library(root), "FotG_2", "MyMod-1.0.0", "core");
    }

    internal static bool HasMyMod(string root) {
        return File.Exists(Path.Combine(MyModCore(root), "..", "mod.json"));
    }

    // FSO version FotG's manifest depends on, or null.
    static string DependencyVersion(string manifest) {
        try {
            string text = File.ReadAllText(manifest);
            var m = Regex.Match(text, @"\{[^{}]*""id""\s*:\s*""FSO""[^{}]*\}");
            if (!m.Success) return null;
            var v = Regex.Match(m.Value, @"""version""\s*:\s*""(?<v>[0-9.]+)""");
            return v.Success ? v.Groups["v"].Value : null;
        } catch { return null; }
    }

    // Rank an executable by the CPU features its build needs; -1 when this CPU cannot run it.
    static int Rank(string exe) {
        string name = Path.GetFileName(exe).ToUpperInvariant();
        if (name.Contains("AVX2")) return Has(PF_AVX2) ? 3 : -1;
        if (name.Contains("AVX")) return Has(PF_AVX) ? 2 : -1;
        return 1;
    }

    static EngineInfo FromFolder(string folder, string title) {
        if (!Directory.Exists(folder)) return null;
        string exe = Directory.GetFiles(folder, "fs2_open_*.exe", SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f).IndexOf("FASTDBG", StringComparison.OrdinalIgnoreCase) < 0)
            .Where(f => Rank(f) >= 0 && (File.Exists(Path.Combine(Path.GetDirectoryName(f), "SDL3.dll")) || File.Exists(Path.Combine(Path.GetDirectoryName(f), "SDL2.dll"))))
            .OrderByDescending(Rank).ThenBy(f => f.Length).FirstOrDefault();
        if (exe == null) return null;
        bool sdl3 = File.Exists(Path.Combine(Path.GetDirectoryName(exe), "SDL3.dll"));
        string name = Path.GetFileNameWithoutExtension(exe).ToUpperInvariant();
        string cpu = name.Contains("AVX2") ? "AVX2" : name.Contains("AVX") ? "AVX" : "SSE2";
        return new EngineInfo { Folder = folder, Exe = exe, Sdl3 = sdl3, Label = title + " (" + (sdl3 ? "SDL3" : "SDL2") + ", " + cpu + ")" };
    }

    // The engine to launch for this game folder and FotG release, or null.
    public static EngineInfo FindEngine(string root, string gameVersion) {
        try {
            if (HasMyMod(root))
                return FromFolder(Path.Combine(Bin(root), "FSO_2601SDL3-26.0.1-1"), "FSO 26.0.1-1");
            var sdl3 = FromFolder(Path.Combine(Bin(root), Sdl3Package), "FSO 26.0.1-SDL3");
            if (sdl3 != null) return sdl3;
            string manifest = Path.Combine(GameLayout.Library(root), "FotG_R", "FotG_R-" + gameVersion, "mod.json");
            string wanted = gameVersion != null ? DependencyVersion(manifest) : null;
            if (wanted != null) {
                var dep = FromFolder(Path.Combine(Bin(root), "FSO-" + wanted), "FSO " + wanted);
                if (dep != null) return dep;
            }
            if (!Directory.Exists(Bin(root))) return null;
            foreach (string folder in Directory.GetDirectories(Bin(root), "FSO-*").OrderByDescending(d => VersionOf(d))) {
                var found = FromFolder(folder, "FSO " + Path.GetFileName(folder).Substring(4));
                if (found != null) return found;
            }
        } catch (IOException) { } catch (UnauthorizedAccessException) { }
        return null;
    }
    static Version VersionOf(string folder) { Version v; return Version.TryParse(Path.GetFileName(folder).Substring(4), out v) ? v : new Version(0, 0); }

    public static bool SelfTest() {
        string temp = Path.Combine(Path.GetTempPath(), "FotGEngineSelfTest-" + Guid.NewGuid().ToString("N"));
        try {
            string bin = Path.Combine(temp, "kn_portable", "Library", "bin");
            string fotg = Path.Combine(temp, "kn_portable", "Library", "FotG_R", "FotG_R-1.1.2");
            Directory.CreateDirectory(fotg);
            File.WriteAllText(Path.Combine(fotg, "mod.json"), "{\"packages\":[{\"name\":\"FG_Core\",\"dependencies\":[{\"id\":\"FSO\",\"version\":\"26.0.1\",\"packages\":[]}]}]}");
            if (FindEngine(temp, "1.1.2") != null) return false;
            // A clean Knossos install: FSO 26.0.1 with one SDL2 build.
            string sse2 = Path.Combine(bin, "FSO-26.0.1", "windows", "x64");
            Directory.CreateDirectory(sse2);
            File.WriteAllText(Path.Combine(sse2, "fs2_open_26_0_1_x64_SSE2.exe"), "");
            File.WriteAllText(Path.Combine(sse2, "fs2_open_26_0_1_x64_SSE2-FASTDBG.exe"), "");
            File.WriteAllText(Path.Combine(sse2, "SDL2.dll"), "");
            var e = FindEngine(temp, "1.1.2");
            if (e == null || e.Sdl3 || !e.Exe.EndsWith("fs2_open_26_0_1_x64_SSE2.exe") || e.Label != "FSO 26.0.1 (SDL2, SSE2)") return false;
            // An older install that still has the SDL3 package uses it.
            string s3 = Path.Combine(bin, Sdl3Package, "windows-x64");
            Directory.CreateDirectory(s3);
            File.WriteAllText(Path.Combine(s3, "fs2_open_26_0_1_sdl3_x64_SSE2.exe"), "");
            File.WriteAllText(Path.Combine(s3, "SDL3.dll"), "");
            e = FindEngine(temp, "1.1.2");
            return e != null && e.Sdl3 && e.Exe.EndsWith("fs2_open_26_0_1_sdl3_x64_SSE2.exe");
        } catch { return false; }
        finally { try { Directory.Delete(temp, true); } catch { } }
    }
}
